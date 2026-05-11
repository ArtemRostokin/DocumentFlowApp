using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Infrastructure.Services;

public sealed class FakeAiClassifier : IAiClassifier
{
    private static readonly ClassificationRule[] Rules =
    [
        new(DocumentType.Contract, ["договор", "dogovor", "contract", "agreement"], ["контрагент", "предмет", "сумма договора"], "Юридический документ, требуется проверка условий."),
        new(DocumentType.Invoice, ["счет", "счёт", "schet", "invoice", "bill"], ["оплата", "поставщик", "сумма счета"], "Финансовый документ, проверьте реквизиты оплаты."),
        new(DocumentType.Act, ["акт", "act"], ["выполненных работ", "прием", "оказанных услуг"], "Подтверждение выполненных работ или услуг."),
        new(DocumentType.Order, ["приказ", "order"], ["распоряжение", "назначить", "утвердить"], "Распорядительный документ по внутреннему процессу."),
        new(DocumentType.Application, ["заявление", "application", "request"], ["прошу", "заявитель", "текст заявления"], "Заявление сотрудника или внешнего заявителя."),
        new(DocumentType.ServiceMemo, ["служебная", "записка", "memo"], ["инициатор", "подразделение", "тема записки"], "Внутренняя служебная записка для пояснений и согласований."),
        new(DocumentType.PurchaseRequest, ["закупка", "заявка", "purchase request"], ["обоснование", "плановая сумма", "предмет закупки"], "Заявка на согласование закупки и планового бюджета."),
        new(DocumentType.Report, ["отчет", "отчёт", "report"], ["итоги", "показатели", "результаты"], "Отчетный документ с итогами или показателями.")
    ];

    public AiClassificationResult ClassifyIncomingDocument(string fileName, string? extractedText = null)
    {
        var name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        var source = $"{name} {(extractedText ?? string.Empty)}".Trim().ToLowerInvariant();

        var bestRule = Rules
            .Select(rule => new
            {
                Rule = rule,
                Score = CalculateScore(name, extractedText, rule)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestRule is null || bestRule.Score < 0.60m)
        {
            return new AiClassificationResult
            {
                SuggestedType = DocumentType.Other,
                ConfidenceScore = 0.42m,
                Summary = string.IsNullOrWhiteSpace(source)
                    ? "Р СњР ВµР Т‘Р С•РЎРѓРЎвЂљР В°РЎвЂљР С•РЎвЂЎР Р…Р С• Р Т‘Р В°Р Р…Р Р…РЎвЂ№РЎвЂ¦ Р Т‘Р В»РЎРЏ РЎС“Р Р†Р ВµРЎР‚Р ВµР Р…Р Р…Р С•Р в„– Р С”Р В»Р В°РЎРѓРЎРѓР С‘РЎвЂћР С‘Р С”Р В°РЎвЂ Р С‘Р С‘."
                    : "Р СњР В°Р в„–Р Т‘Р ВµР Р…РЎвЂ№ РЎРѓР В»Р С‘РЎв‚¬Р С”Р С•Р С РЎРѓР В»Р В°Р В±РЎвЂ№Р Вµ Р С—РЎР‚Р С‘Р В·Р Р…Р В°Р С”Р С‘, РЎвЂљР С‘Р С— Р Р…РЎС“Р В¶Р Р…Р С• Р С—Р С•Р Т‘РЎвЂљР Р†Р ВµРЎР‚Р Т‘Р С‘РЎвЂљРЎРЉ Р Р†РЎР‚РЎС“РЎвЂЎР Р…РЎС“РЎР‹.",
                SuggestedTags = ["incoming", "needs-review", "ai-low-confidence"]
            };
        }

        var confidence = bestRule.Score > 0.97m ? 0.97m : bestRule.Score;
        var confidenceTag = confidence >= 0.85m ? "ai-auto-classified" : "ai-needs-review";

        return new AiClassificationResult
        {
            SuggestedType = bestRule.Rule.Type,
            ConfidenceScore = confidence,
            Summary = bestRule.Rule.Summary,
            SuggestedTags = ["incoming", confidenceTag, bestRule.Rule.Type.ToString().ToLowerInvariant()]
        };
    }

    public IReadOnlyList<AiFieldSuggestionResult> BuildSuggestions(Document document)
    {
        var classification = ClassifyIncomingDocument(document.Title ?? string.Empty, document.ExtractedText);
        var seed = BuildSeed(document);
        var rng = new Random(seed);

        decimal Confidence(decimal baseValue, int varianceMax = 6)
        {
            var variance = rng.Next(0, varianceMax + 1) / 100m;
            var value = Math.Clamp(baseValue - variance, 0.45m, 0.98m);
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        var title = string.IsNullOrWhiteSpace(document.Title)
            ? $"Р вЂќР С•Р С”РЎС“Р СР ВµР Р…РЎвЂљ #{document.DocumentId}"
            : document.Title!;
        var description = string.IsNullOrWhiteSpace(document.ExtractedText)
            ? classification.Summary
            : document.ExtractedText!;
        var dueDate = document.DueDate?.ToString("yyyy-MM-dd")
            ?? DateTime.UtcNow.Date.AddDays(classification.ShouldAutoAssignType ? 5 : 10).ToString("yyyy-MM-dd");
        var priority = document.Priority?.ToString()
            ?? (classification.SuggestedType is DocumentType.Invoice or DocumentType.Contract ? "3" : "2");
        var tags = string.IsNullOrWhiteSpace(document.Tags)
            ? string.Join(',', classification.SuggestedTags)
            : document.Tags!;

        return
        [
            new AiFieldSuggestionResult
            {
                FieldKey = "type",
                Label = "Р СћР С‘Р С—",
                SuggestedValue = classification.SuggestedType.ToString(),
                ConfidenceScore = classification.ConfidenceScore
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "duedate",
                Label = "Р РЋРЎР‚Р С•Р С” Р С‘РЎРѓР С—Р С•Р В»Р Р…Р ВµР Р…Р С‘РЎРЏ",
                SuggestedValue = dueDate,
                ConfidenceScore = Confidence(classification.ShouldAutoAssignType ? 0.84m : 0.67m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "title",
                Label = "Р СњР В°Р В·Р Р†Р В°Р Р…Р С‘Р Вµ",
                SuggestedValue = title,
                ConfidenceScore = Confidence(0.88m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "description",
                Label = "Р С›Р С—Р С‘РЎРѓР В°Р Р…Р С‘Р Вµ",
                SuggestedValue = description,
                ConfidenceScore = Confidence(string.IsNullOrWhiteSpace(document.ExtractedText) ? 0.63m : 0.86m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "priority",
                Label = "Р СџРЎР‚Р С‘Р С•РЎР‚Р С‘РЎвЂљР ВµРЎвЂљ",
                SuggestedValue = priority,
                ConfidenceScore = Confidence(classification.SuggestedType == DocumentType.Invoice ? 0.79m : 0.68m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "tags",
                Label = "Р СћР ВµР С–Р С‘",
                SuggestedValue = tags,
                ConfidenceScore = Confidence(0.72m)
            }
        ];
    }

    private static decimal CalculateScore(string fileName, string? extractedText, ClassificationRule rule)
    {
        var lowerName = (fileName ?? string.Empty).ToLowerInvariant();
        var lowerText = (extractedText ?? string.Empty).ToLowerInvariant();

        decimal score = 0m;

        foreach (var keyword in rule.PrimaryKeywords)
        {
            if (lowerName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 0.92m);

            if (lowerText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 0.87m);
        }

        foreach (var keyword in rule.SecondaryKeywords)
        {
            if (lowerText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, score >= 0.85m ? score : 0.68m);
        }

        if (score == 0m && !string.IsNullOrWhiteSpace(lowerName))
            score = 0.42m;

        return score;
    }

    private static int BuildSeed(Document document)
    {
        var seed = document.DocumentId;

        unchecked
        {
            foreach (var ch in document.FileHash ?? string.Empty)
                seed = (seed * 31) + ch;

            foreach (var ch in document.Title ?? string.Empty)
                seed = (seed * 17) + ch;
        }

        return seed;
    }

    private sealed record ClassificationRule(
        DocumentType Type,
        string[] PrimaryKeywords,
        string[] SecondaryKeywords,
        string Summary);
}
