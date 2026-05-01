using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Infrastructure.Services;

public sealed class RuleBasedDocumentFieldExtractor : IDocumentFieldExtractor
{
    private static readonly Regex DateRegex = new(
        @"\b(?<value>(?:0?[1-9]|[12][0-9]|3[01])[./-](?:0?[1-9]|1[0-2])[./-](?:20\d{2}|\d{2})|(?:20\d{2})[./-](?:0?[1-9]|1[0-2])[./-](?:0?[1-9]|[12][0-9]|3[01]))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AmountRegex = new(
        @"(?<value>\d+(?:[\s\u00A0]\d{3})*(?:[.,]\d{2})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NameRegex = new(
        @"\b(?<value>[А-ЯЁ][а-яё-]+\s+[А-ЯЁ][а-яё-]+\s+[А-ЯЁ][а-яё-]+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DocumentFieldExtractionResult Extract(DocumentType documentType, string? extractedText, string? fileName = null)
    {
        var text = NormalizeText(extractedText, fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentFieldExtractionResult
            {
                DocumentType = documentType,
                Fields = []
            };
        }

        var fields = documentType switch
        {
            DocumentType.Contract => ExtractContractFields(text),
            DocumentType.Invoice => ExtractInvoiceFields(text),
            DocumentType.Application => ExtractApplicationFields(text),
            _ => []
        };

        return new DocumentFieldExtractionResult
        {
            DocumentType = documentType,
            Fields = fields
        };
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractContractFields(string text)
    {
        return BuildFields(
            TryExtractNumber(text, "contract_number", "Номер договора", ["договор №", "договор n", "номер договора", "contract no", "contract #"]),
            TryExtractDate(text, "contract_date", "Дата договора", ["дата договора", "договор от", "date"]),
            TryExtractLineValue(text, "counterparty", "Контрагент", ["контрагент", "заказчик", "покупатель", "исполнитель"]),
            TryExtractAmount(text, "amount", "Сумма договора", ["сумма договора", "стоимость договора", "цена договора", "сумма", "total"]),
            TryExtractLineValue(text, "subject", "Предмет договора", ["предмет договора", "предмет", "наименование договора", "содержание договора"], allowLongValue: true));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractInvoiceFields(string text)
    {
        return BuildFields(
            TryExtractInvoiceNumber(text),
            TryExtractDate(text, "invoice_date", "Дата счета", ["дата счета", "дата счёта", "счет от", "счёт от", "invoice date"]),
            TryExtractSupplier(text),
            TryExtractInvoiceAmount(text),
            TryExtractDate(text, "payment_due", "Срок оплаты", ["срок оплаты", "оплатить до", "дата оплаты", "payment due"], allowFallback: false));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractApplicationFields(string text)
    {
        return BuildFields(
            TryExtractName(text, "employee_name", "ФИО сотрудника", ["фио сотрудника", "заявитель", "от"]),
            TryExtractLineValue(text, "department", "Подразделение", ["подразделение", "отдел", "департамент"]),
            TryExtractLineValue(text, "application_topic", "Тема обращения", ["тема обращения", "тема", "предмет обращения", "о чем", "о чём"], allowLongValue: true),
            TryExtractApplicationText(text, "application_text", "Текст заявления"));
    }

    private static IReadOnlyList<ExtractedFieldResult> BuildFields(params ExtractedFieldResult?[] fields)
        => fields.Where(static field => field is not null).Cast<ExtractedFieldResult>().ToList();

    private static ExtractedFieldResult? TryExtractNumber(string text, string fieldKey, string label, IReadOnlyList<string> anchors)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!ContainsAny(line, anchors))
                continue;

            var index = line.IndexOf('№');
            if (index >= 0)
            {
                var value = CleanValue(line[(index + 1)..]);
                if (!string.IsNullOrWhiteSpace(value))
                    return BuildField(fieldKey, label, TrimTo(value, 80), 0.88m);
            }

            var tokenMatch = Regex.Match(line, @"(?:№|#|номер|no\.?)\s*(?<value>[A-Za-zА-Яа-яЁё0-9/-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (tokenMatch.Success)
                return BuildField(fieldKey, label, CleanValue(tokenMatch.Groups["value"].Value), 0.84m);
        }

        return null;
    }

    private static ExtractedFieldResult? TryExtractDate(string text, string fieldKey, string label, IReadOnlyList<string> anchors, bool allowFallback = true)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!ContainsAny(line, anchors))
                continue;

            var match = DateRegex.Match(line);
            if (match.Success)
                return BuildField(fieldKey, label, NormalizeDate(match.Groups["value"].Value), 0.82m);
        }

        if (!allowFallback)
            return null;

        var fallbackMatch = DateRegex.Match(text);
        return fallbackMatch.Success
            ? BuildField(fieldKey, label, NormalizeDate(fallbackMatch.Groups["value"].Value), 0.68m)
            : null;
    }

    private static ExtractedFieldResult? TryExtractAmount(string text, string fieldKey, string label, IReadOnlyList<string> anchors)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!ContainsAny(line, anchors))
                continue;

            var matches = AmountRegex.Matches(line);
            if (matches.Count == 0)
                continue;

            var best = matches[^1].Groups["value"].Value;
            return BuildField(fieldKey, label, NormalizeAmount(best), 0.84m);
        }

        return null;
    }

    private static ExtractedFieldResult? TryExtractInvoiceNumber(string text)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!line.Contains("счет", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("счёт", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("invoice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ContainsAny(line, ["расчетный счет", "расчётный счёт", "корр", "корр.", "банк получателя", "бик"]) &&
                !line.Contains("счет №", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("счёт №", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("invoice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var explicitMatch = Regex.Match(
                line,
                @"(?:сч[её]т(?:\s+на\s+оплату)?|invoice)(?:\s*(?:№|#|no\.?|номер)\s*)(?<value>[A-Za-zА-Яа-яЁё0-9/-]{3,30})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (explicitMatch.Success)
            {
                var candidate = CleanValue(explicitMatch.Groups["value"].Value);
                if (!IsLikelyBankAccount(candidate) && !LooksLikeAmount(candidate))
                    return BuildField("invoice_number", "Номер счета", candidate, 0.88m);
            }

            foreach (var token in Regex.Matches(line, @"[A-Za-zА-Яа-яЁё0-9/-]{3,30}").Select(match => CleanValue(match.Value)))
            {
                if (string.IsNullOrWhiteSpace(token) || IsLikelyBankAccount(token) || LooksLikeAmount(token))
                    continue;

                if (token.StartsWith("сч", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains('-', StringComparison.Ordinal) ||
                    token.Contains('/', StringComparison.Ordinal))
                {
                    return BuildField("invoice_number", "Номер счета", token, 0.74m);
                }
            }
        }

        return null;
    }

    private static ExtractedFieldResult? TryExtractSupplier(string text)
    {
        var field = TryExtractLineValue(text, "supplier", "Поставщик", ["поставщик", "получатель", "исполнитель", "supplier"]);
        if (field is null)
            return null;

        var value = TrimByKeywords(field.SuggestedValue, ["инн", "кпп", "адрес", "тел", "телефон", "р/с", "р/c"]);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new ExtractedFieldResult
            {
                FieldKey = field.FieldKey,
                Label = field.Label,
                SuggestedValue = value,
                ConfidenceScore = 0.82m,
                Source = field.Source
            };
    }

    private static ExtractedFieldResult? TryExtractInvoiceAmount(string text)
    {
        decimal? bestAmount = null;
        var priorityAnchors = new[] { "к оплате", "итого", "всего к оплате", "итого к оплате" };
        var fallbackAnchors = new[] { "сумма", "всего", "total", "amount" };

        foreach (var line in EnumerateLines(text))
        {
            if (ContainsAny(line, ["счет №", "счёт №", "сч. №", "бик", "расчетный счет", "расчётный счёт", "кпп", "инн"]))
                continue;

            if (!ContainsAny(line, priorityAnchors))
                continue;

            bestAmount = MaxAmountFromLine(line, bestAmount);
        }

        if (bestAmount is null)
        {
            foreach (var line in EnumerateLines(text))
            {
                if (ContainsAny(line, ["счет №", "счёт №", "сч. №", "бик", "расчетный счет", "расчётный счёт", "кпп", "инн"]))
                    continue;

                if (!ContainsAny(line, fallbackAnchors))
                    continue;

                bestAmount = MaxAmountFromLine(line, bestAmount);
            }
        }

        if (bestAmount is null)
        {
            foreach (Match match in Regex.Matches(text, @"(?<value>\d+[.,]\d{2})\s*(?:руб|р\.|₽)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (!TryParseAmount(match.Groups["value"].Value, out var parsed))
                    continue;

                if (parsed <= 0m)
                    continue;

                if (bestAmount is null || parsed > bestAmount.Value)
                    bestAmount = parsed;
            }
        }

        if (bestAmount is null || bestAmount <= 0m)
            return null;

        return BuildField("amount", "Сумма", bestAmount.Value.ToString("0.##", CultureInfo.InvariantCulture), bestAmount >= 1000m ? 0.86m : 0.62m);
    }

    private static decimal? MaxAmountFromLine(string line, decimal? currentBest)
    {
        foreach (Match match in AmountRegex.Matches(line))
        {
            var raw = CleanValue(match.Groups["value"].Value);
            if (!TryParseAmount(raw, out var parsed))
                continue;

            if (parsed <= 0m)
                continue;

            if (parsed >= 100000000000000000m)
                continue;

            if (currentBest is null || parsed > currentBest.Value)
                currentBest = parsed;
        }

        return currentBest;
    }

    private static ExtractedFieldResult? TryExtractLineValue(string text, string fieldKey, string label, IReadOnlyList<string> anchors, bool allowLongValue = false)
    {
        foreach (var line in EnumerateLines(text))
        {
            var matchedAnchor = anchors.FirstOrDefault(anchor => line.Contains(anchor, StringComparison.OrdinalIgnoreCase));
            if (matchedAnchor is null)
                continue;

            var value = TrySplitBySeparators(line, matchedAnchor);
            value = string.IsNullOrWhiteSpace(value) ? CleanValue(line) : value;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            return BuildField(fieldKey, label, allowLongValue ? TrimTo(value, 220) : TrimTo(value, 120), allowLongValue ? 0.72m : 0.78m);
        }

        return null;
    }

    private static ExtractedFieldResult? TryExtractName(string text, string fieldKey, string label, IReadOnlyList<string> anchors)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!ContainsAny(line, anchors))
                continue;

            var explicitValue = TrySplitBySeparators(line);
            if (!string.IsNullOrWhiteSpace(explicitValue) && NameRegex.IsMatch(explicitValue))
                return BuildField(fieldKey, label, NameRegex.Match(explicitValue).Groups["value"].Value, 0.86m);

            var match = NameRegex.Match(line);
            if (match.Success)
                return BuildField(fieldKey, label, match.Groups["value"].Value, 0.82m);
        }

        var fallback = NameRegex.Match(text);
        return fallback.Success
            ? BuildField(fieldKey, label, fallback.Groups["value"].Value, 0.63m)
            : null;
    }

    private static ExtractedFieldResult? TryExtractApplicationText(string text, string fieldKey, string label)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!line.Contains("прошу", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = CleanValue(line);
            if (!string.IsNullOrWhiteSpace(value))
                return BuildField(fieldKey, label, TrimTo(value, 260), 0.78m);
        }

        foreach (var line in EnumerateLines(text))
        {
            if (!line.Contains("заявление", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = CleanValue(line);
            if (value.Length < 20)
                continue;

            if (!string.IsNullOrWhiteSpace(value))
                return BuildField(fieldKey, label, TrimTo(value, 260), 0.70m);
        }

        var snippet = TrimTo(CleanValue(text), 260);
        return string.IsNullOrWhiteSpace(snippet)
            ? null
            : BuildField(fieldKey, label, snippet, 0.58m);
    }

    private static ExtractedFieldResult BuildField(string fieldKey, string label, string value, decimal confidenceScore)
        => new()
        {
            FieldKey = fieldKey,
            Label = label,
            SuggestedValue = value,
            ConfidenceScore = confidenceScore,
            Source = "rule-based"
        };

    private static string NormalizeText(string? extractedText, string? fileName)
    {
        var combined = $"{extractedText ?? string.Empty}\n{Path.GetFileNameWithoutExtension(fileName ?? string.Empty)}";
        return combined
            .Replace('\u00A0', ' ')
            .Replace('\t', ' ')
            .Trim();
    }

    private static IEnumerable<string> EnumerateLines(string text)
        => text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanValue)
            .Where(static line => !string.IsNullOrWhiteSpace(line));

    private static bool ContainsAny(string text, IEnumerable<string> anchors)
        => anchors.Any(anchor => text.Contains(anchor, StringComparison.OrdinalIgnoreCase));

    private static string? TrySplitBySeparators(string line, string? anchor = null)
    {
        var value = line;
        if (!string.IsNullOrWhiteSpace(anchor))
        {
            var index = line.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                value = line[(index + anchor.Length)..];
        }

        foreach (var separator in new[] { ":", "—", "-", "–" })
        {
            var separatorIndex = value.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex < 0)
                continue;

            var candidate = CleanValue(value[(separatorIndex + separator.Length)..]);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static string NormalizeDate(string raw)
    {
        var normalized = raw.Trim().Replace('/', '.').Replace('-', '.');
        var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy.MM.dd", "dd.MM.yy", "d.M.yy" };
        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return raw.Trim();
    }

    private static string NormalizeAmount(string raw)
    {
        var value = raw.Replace('\u00A0', ' ').Trim();
        if (TryParseAmount(value, out var amount))
            return amount.ToString("0.##", CultureInfo.InvariantCulture);

        return value;
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        var normalized = raw.Replace('\u00A0', ' ').Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static string TrimByKeywords(string value, IReadOnlyList<string> stopKeywords)
    {
        var bestIndex = value.Length;
        foreach (var keyword in stopKeywords)
        {
            var index = value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index < bestIndex)
                bestIndex = index;
        }

        return CleanValue(value[..bestIndex]);
    }

    private static bool IsLikelyBankAccount(string value)
    {
        var compact = value.Replace(" ", string.Empty);
        if (compact.Length >= 18 && compact.All(char.IsDigit))
            return true;

        return compact.StartsWith("3010", StringComparison.OrdinalIgnoreCase) ||
               compact.StartsWith("4070", StringComparison.OrdinalIgnoreCase) ||
               compact.StartsWith("4080", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAmount(string value)
        => TryParseAmount(value, out _);

    private static string CleanValue(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ");
        return normalized.Trim(' ', ':', ';', '.', ',', '—', '-', '–');
    }

    private static string TrimTo(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return $"{value[..Math.Max(0, maxLength - 1)].Trim()}…";
    }
}
