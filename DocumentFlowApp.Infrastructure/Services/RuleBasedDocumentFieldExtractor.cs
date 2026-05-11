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
        @"\b(?<value>[Р В РЎвЂ™-Р В Р вЂЎР В Р С“][Р В Р’В°-Р РЋР РЏР РЋРІР‚В-]+\s+[Р В РЎвЂ™-Р В Р вЂЎР В Р С“][Р В Р’В°-Р РЋР РЏР РЋРІР‚В-]+\s+[Р В РЎвЂ™-Р В Р вЂЎР В Р С“][Р В Р’В°-Р РЋР РЏР РЋРІР‚В-]+)\b",
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
            DocumentType.ServiceMemo => ExtractServiceMemoFields(text),
            DocumentType.PurchaseRequest => ExtractPurchaseRequestFields(text),
            DocumentType.Act => ExtractActFields(text),
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
            TryExtractNumber(text, "contract_number", "Р В РЎСљР В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", ["Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљ Р Р†РІР‚С›РІР‚вЂњ", "Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљ n", "Р В Р вЂ¦Р В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "contract no", "contract #"]),
            TryExtractDate(text, "contract_date", "Р В РІР‚СњР В Р’В°Р РЋРІР‚С™Р В Р’В° Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", ["Р В РўвЂР В Р’В°Р РЋРІР‚С™Р В Р’В° Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљ Р В РЎвЂўР РЋРІР‚С™", "date"]),
            TryExtractLineValue(text, "counterparty", "Р В РЎв„ўР В РЎвЂўР В Р вЂ¦Р РЋРІР‚С™Р РЋР вЂљР В Р’В°Р В РЎвЂ“Р В Р’ВµР В Р вЂ¦Р РЋРІР‚С™", ["Р В РЎвЂќР В РЎвЂўР В Р вЂ¦Р РЋРІР‚С™Р РЋР вЂљР В Р’В°Р В РЎвЂ“Р В Р’ВµР В Р вЂ¦Р РЋРІР‚С™", "Р В Р’В·Р В Р’В°Р В РЎвЂќР В Р’В°Р В Р’В·Р РЋРІР‚РЋР В РЎвЂР В РЎвЂќ", "Р В РЎвЂ”Р В РЎвЂўР В РЎвЂќР РЋРЎвЂњР В РЎвЂ”Р В Р’В°Р РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР Р‰", "Р В РЎвЂР РЋР С“Р В РЎвЂ”Р В РЎвЂўР В Р’В»Р В Р вЂ¦Р В РЎвЂР РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР Р‰"]),
            TryExtractAmount(text, "amount", "Р В Р Р‹Р РЋРЎвЂњР В РЎВР В РЎВР В Р’В° Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", ["Р РЋР С“Р РЋРЎвЂњР В РЎВР В РЎВР В Р’В° Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р РЋР С“Р РЋРІР‚С™Р В РЎвЂўР В РЎвЂР В РЎВР В РЎвЂўР РЋР С“Р РЋРІР‚С™Р РЋР Р‰ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р РЋРІР‚В Р В Р’ВµР В Р вЂ¦Р В Р’В° Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р РЋР С“Р РЋРЎвЂњР В РЎВР В РЎВР В Р’В°", "total"]),
            TryExtractLineValue(text, "subject", "Р В РЎСџР РЋР вЂљР В Р’ВµР В РўвЂР В РЎВР В Р’ВµР РЋРІР‚С™ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", ["Р В РЎвЂ”Р РЋР вЂљР В Р’ВµР В РўвЂР В РЎВР В Р’ВµР РЋРІР‚С™ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р В РЎвЂ”Р РЋР вЂљР В Р’ВµР В РўвЂР В РЎВР В Р’ВµР РЋРІР‚С™", "Р В Р вЂ¦Р В Р’В°Р В РЎвЂР В РЎВР В Р’ВµР В Р вЂ¦Р В РЎвЂўР В Р вЂ Р В Р’В°Р В Р вЂ¦Р В РЎвЂР В Р’Вµ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°", "Р РЋР С“Р В РЎвЂўР В РўвЂР В Р’ВµР РЋР вЂљР В Р’В¶Р В Р’В°Р В Р вЂ¦Р В РЎвЂР В Р’Вµ Р В РўвЂР В РЎвЂўР В РЎвЂ“Р В РЎвЂўР В Р вЂ Р В РЎвЂўР РЋР вЂљР В Р’В°"], allowLongValue: true));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractInvoiceFields(string text)
    {
        return BuildFields(
            TryExtractInvoiceNumber(text),
            TryExtractDate(text, "invoice_date", "Р В РІР‚СњР В Р’В°Р РЋРІР‚С™Р В Р’В° Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р’В°", ["Р В РўвЂР В Р’В°Р РЋРІР‚С™Р В Р’В° Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р’В°", "Р В РўвЂР В Р’В°Р РЋРІР‚С™Р В Р’В° Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™Р В Р’В°", "Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™ Р В РЎвЂўР РЋРІР‚С™", "Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™ Р В РЎвЂўР РЋРІР‚С™", "invoice date"]),
            TryExtractSupplier(text),
            TryExtractInvoiceAmount(text),
            TryExtractDate(text, "payment_due", "Р В Р Р‹Р РЋР вЂљР В РЎвЂўР В РЎвЂќ Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р РЋРІР‚в„–", ["Р РЋР С“Р РЋР вЂљР В РЎвЂўР В РЎвЂќ Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р РЋРІР‚в„–", "Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р В РЎвЂР РЋРІР‚С™Р РЋР Р‰ Р В РўвЂР В РЎвЂў", "Р В РўвЂР В Р’В°Р РЋРІР‚С™Р В Р’В° Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р РЋРІР‚в„–", "payment due"], allowFallback: false));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractApplicationFields(string text)
    {
        return BuildFields(
            TryExtractName(text, "employee_name", "Р В Р’В¤Р В Р’ВР В РЎвЂє Р РЋР С“Р В РЎвЂўР РЋРІР‚С™Р РЋР вЂљР РЋРЎвЂњР В РўвЂР В Р вЂ¦Р В РЎвЂР В РЎвЂќР В Р’В°", ["Р РЋРІР‚С›Р В РЎвЂР В РЎвЂў Р РЋР С“Р В РЎвЂўР РЋРІР‚С™Р РЋР вЂљР РЋРЎвЂњР В РўвЂР В Р вЂ¦Р В РЎвЂР В РЎвЂќР В Р’В°", "Р В Р’В·Р В Р’В°Р РЋР РЏР В Р вЂ Р В РЎвЂР РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР Р‰", "Р В РЎвЂўР РЋРІР‚С™"]),
            TryExtractLineValue(text, "department", "Р В РЎСџР В РЎвЂўР В РўвЂР РЋР вЂљР В Р’В°Р В Р’В·Р В РўвЂР В Р’ВµР В Р’В»Р В Р’ВµР В Р вЂ¦Р В РЎвЂР В Р’Вµ", ["Р В РЎвЂ”Р В РЎвЂўР В РўвЂР РЋР вЂљР В Р’В°Р В Р’В·Р В РўвЂР В Р’ВµР В Р’В»Р В Р’ВµР В Р вЂ¦Р В РЎвЂР В Р’Вµ", "Р В РЎвЂўР РЋРІР‚С™Р В РўвЂР В Р’ВµР В Р’В»", "Р В РўвЂР В Р’ВµР В РЎвЂ”Р В Р’В°Р РЋР вЂљР РЋРІР‚С™Р В Р’В°Р В РЎВР В Р’ВµР В Р вЂ¦Р РЋРІР‚С™"]),
            TryExtractLineValue(text, "application_topic", "Р В РЎС›Р В Р’ВµР В РЎВР В Р’В° Р В РЎвЂўР В Р’В±Р РЋР вЂљР В Р’В°Р РЋРІР‚В°Р В Р’ВµР В Р вЂ¦Р В РЎвЂР РЋР РЏ", ["Р РЋРІР‚С™Р В Р’ВµР В РЎВР В Р’В° Р В РЎвЂўР В Р’В±Р РЋР вЂљР В Р’В°Р РЋРІР‚В°Р В Р’ВµР В Р вЂ¦Р В РЎвЂР РЋР РЏ", "Р РЋРІР‚С™Р В Р’ВµР В РЎВР В Р’В°", "Р В РЎвЂ”Р РЋР вЂљР В Р’ВµР В РўвЂР В РЎВР В Р’ВµР РЋРІР‚С™ Р В РЎвЂўР В Р’В±Р РЋР вЂљР В Р’В°Р РЋРІР‚В°Р В Р’ВµР В Р вЂ¦Р В РЎвЂР РЋР РЏ", "Р В РЎвЂў Р РЋРІР‚РЋР В Р’ВµР В РЎВ", "Р В РЎвЂў Р РЋРІР‚РЋР РЋРІР‚ВР В РЎВ"], allowLongValue: true),
            TryExtractApplicationText(text, "application_text", "Р В РЎС›Р В Р’ВµР В РЎвЂќР РЋР С“Р РЋРІР‚С™ Р В Р’В·Р В Р’В°Р РЋР РЏР В Р вЂ Р В Р’В»Р В Р’ВµР В Р вЂ¦Р В РЎвЂР РЋР РЏ"));
    }


    private static IReadOnlyList<ExtractedFieldResult> ExtractServiceMemoFields(string text)
    {
        return BuildFields(
            TryExtractNumber(text, "memo_number", "Р СњР С•Р СР ВµРЎР‚ Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", ["РЎРѓР В»РЎС“Р В¶Р ВµР В±Р Р…Р В°РЎРЏ Р В·Р В°Р С—Р С‘РЎРѓР С”Р В° РІвЂћвЂ“", "Р В·Р В°Р С—Р С‘РЎРѓР С”Р В° РІвЂћвЂ“", "Р Р…Р С•Р СР ВµРЎР‚ Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", "memo #", "memo no"]),
            TryExtractDate(text, "memo_date", "Р вЂќР В°РЎвЂљР В° Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", ["Р Т‘Р В°РЎвЂљР В° Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", "РЎРѓР В»РЎС“Р В¶Р ВµР В±Р Р…Р В°РЎРЏ Р В·Р В°Р С—Р С‘РЎРѓР С”Р В° Р С•РЎвЂљ", "memo date"]),
            TryExtractName(text, "initiator", "Р ВР Р…Р С‘РЎвЂ Р С‘Р В°РЎвЂљР С•РЎР‚", ["Р С‘Р Р…Р С‘РЎвЂ Р С‘Р В°РЎвЂљР С•РЎР‚", "Р С—Р С•Р Т‘Р С–Р С•РЎвЂљР С•Р Р†Р С‘Р В»", "Р В°Р Р†РЎвЂљР С•РЎР‚", "Р С•РЎвЂљ"]),
            TryExtractLineValue(text, "department", "Р СџР С•Р Т‘РЎР‚Р В°Р В·Р Т‘Р ВµР В»Р ВµР Р…Р С‘Р Вµ", ["Р С—Р С•Р Т‘РЎР‚Р В°Р В·Р Т‘Р ВµР В»Р ВµР Р…Р С‘Р Вµ", "Р С•РЎвЂљР Т‘Р ВµР В»", "Р Т‘Р ВµР С—Р В°РЎР‚РЎвЂљР В°Р СР ВµР Р…РЎвЂљ"]),
            TryExtractLineValue(text, "memo_topic", "Р СћР ВµР СР В° Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", ["РЎвЂљР ВµР СР В° Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘", "РЎвЂљР ВµР СР В°", "Р С—РЎР‚Р ВµР Т‘Р СР ВµРЎвЂљ"], allowLongValue: true),
            TryExtractApplicationText(text, "memo_text", "Р РЋР С•Р Т‘Р ВµРЎР‚Р В¶Р В°Р Р…Р С‘Р Вµ Р В·Р В°Р С—Р С‘РЎРѓР С”Р С‘"));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractPurchaseRequestFields(string text)
    {
        return BuildFields(
            TryExtractNumber(text, "request_number", "Р СњР С•Р СР ВµРЎР‚ Р В·Р В°РЎРЏР Р†Р С”Р С‘", ["Р В·Р В°РЎРЏР Р†Р С”Р В° Р Р…Р В° Р В·Р В°Р С”РЎС“Р С—Р С”РЎС“ РІвЂћвЂ“", "Р В·Р В°РЎРЏР Р†Р С”Р В° РІвЂћвЂ“", "Р Р…Р С•Р СР ВµРЎР‚ Р В·Р В°РЎРЏР Р†Р С”Р С‘", "purchase request #", "purchase request no"]),
            TryExtractDate(text, "request_date", "Р вЂќР В°РЎвЂљР В° Р В·Р В°РЎРЏР Р†Р С”Р С‘", ["Р Т‘Р В°РЎвЂљР В° Р В·Р В°РЎРЏР Р†Р С”Р С‘", "Р В·Р В°РЎРЏР Р†Р С”Р В° Р Р…Р В° Р В·Р В°Р С”РЎС“Р С—Р С”РЎС“ Р С•РЎвЂљ", "purchase request date"]),
            TryExtractName(text, "initiator", "Р ВР Р…Р С‘РЎвЂ Р С‘Р В°РЎвЂљР С•РЎР‚", ["Р С‘Р Р…Р С‘РЎвЂ Р С‘Р В°РЎвЂљР С•РЎР‚", "Р В·Р В°РЎРЏР Р†Р С‘РЎвЂљР ВµР В»РЎРЉ", "Р С—Р С•Р Т‘Р С–Р С•РЎвЂљР С•Р Р†Р С‘Р В»", "Р С•РЎвЂљ"]),
            TryExtractLineValue(text, "department", "Р СџР С•Р Т‘РЎР‚Р В°Р В·Р Т‘Р ВµР В»Р ВµР Р…Р С‘Р Вµ", ["Р С—Р С•Р Т‘РЎР‚Р В°Р В·Р Т‘Р ВµР В»Р ВµР Р…Р С‘Р Вµ", "Р С•РЎвЂљР Т‘Р ВµР В»", "Р Т‘Р ВµР С—Р В°РЎР‚РЎвЂљР В°Р СР ВµР Р…РЎвЂљ"]),
            TryExtractLineValue(text, "purchase_subject", "Р СџРЎР‚Р ВµР Т‘Р СР ВµРЎвЂљ Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘", ["Р С—РЎР‚Р ВµР Т‘Р СР ВµРЎвЂљ Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘", "Р С•Р В±РЎР‰Р ВµР С”РЎвЂљ Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘", "Р Р…Р В°Р С‘Р СР ВµР Р…Р С•Р Р†Р В°Р Р…Р С‘Р Вµ Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘"], allowLongValue: true),
            TryExtractAmount(text, "planned_amount", "Р СџР В»Р В°Р Р…Р С•Р Р†Р В°РЎРЏ РЎРѓРЎС“Р СР СР В°", ["Р С—Р В»Р В°Р Р…Р С•Р Р†Р В°РЎРЏ РЎРѓРЎС“Р СР СР В°", "РЎРѓРЎС“Р СР СР В° Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘", "РЎРѓРЎС“Р СР СР В°", "Р В±РЎР‹Р Т‘Р В¶Р ВµРЎвЂљ"]),
            TryExtractLineValue(text, "quantity", "Р С™Р С•Р В»Р С‘РЎвЂЎР ВµРЎРѓРЎвЂљР Р†Р С•", ["Р С”Р С•Р В»Р С‘РЎвЂЎР ВµРЎРѓРЎвЂљР Р†Р С•", "Р С•Р В±РЎР‰Р ВµР С", "Р С•Р В±РЎР‰РЎвЂР С"]),
            TryExtractApplicationText(text, "purchase_justification", "Р С›Р В±Р С•РЎРѓР Р…Р С•Р Р†Р В°Р Р…Р С‘Р Вµ Р В·Р В°Р С”РЎС“Р С—Р С”Р С‘"));
    }

    private static IReadOnlyList<ExtractedFieldResult> ExtractActFields(string text)
    {
        return BuildFields(
            TryExtractNumber(text, "act_number", "Р СњР С•Р СР ВµРЎР‚ Р В°Р С”РЎвЂљР В°", ["Р В°Р С”РЎвЂљ РІвЂћвЂ“", "Р В°Р С”РЎвЂљ Р Р†РЎвЂ№Р С—Р С•Р В»Р Р…Р ВµР Р…Р Р…РЎвЂ№РЎвЂ¦ РЎР‚Р В°Р В±Р С•РЎвЂљ РІвЂћвЂ“", "Р Р…Р С•Р СР ВµРЎР‚ Р В°Р С”РЎвЂљР В°", "act #", "act no"]),
            TryExtractDate(text, "act_date", "Р вЂќР В°РЎвЂљР В° Р В°Р С”РЎвЂљР В°", ["Р Т‘Р В°РЎвЂљР В° Р В°Р С”РЎвЂљР В°", "Р В°Р С”РЎвЂљ Р С•РЎвЂљ", "act date"]),
            TryExtractLineValue(text, "counterparty", "Р С™Р С•Р Р…РЎвЂљРЎР‚Р В°Р С–Р ВµР Р…РЎвЂљ", ["Р С”Р С•Р Р…РЎвЂљРЎР‚Р В°Р С–Р ВµР Р…РЎвЂљ", "Р В·Р В°Р С”Р В°Р В·РЎвЂЎР С‘Р С”", "Р С‘РЎРѓР С—Р С•Р В»Р Р…Р С‘РЎвЂљР ВµР В»РЎРЉ"]),
            TryExtractLineValue(text, "basis", "Р С›РЎРѓР Р…Р С•Р Р†Р В°Р Р…Р С‘Р Вµ", ["Р С•РЎРѓР Р…Р С•Р Р†Р В°Р Р…Р С‘Р Вµ", "Р С—Р С• Р Т‘Р С•Р С–Р С•Р Р†Р С•РЎР‚РЎС“", "Р Т‘Р С•Р С–Р С•Р Р†Р С•РЎР‚"], allowLongValue: true),
            TryExtractAmount(text, "amount", "Р РЋРЎС“Р СР СР В° Р В°Р С”РЎвЂљР В°", ["РЎРѓРЎС“Р СР СР В° Р В°Р С”РЎвЂљР В°", "РЎРѓРЎвЂљР С•Р С‘Р СР С•РЎРѓРЎвЂљРЎРЉ РЎР‚Р В°Р В±Р С•РЎвЂљ", "РЎРѓРЎвЂљР С•Р С‘Р СР С•РЎРѓРЎвЂљРЎРЉ РЎС“РЎРѓР В»РЎС“Р С–", "РЎРѓРЎС“Р СР СР В°"]),
            TryExtractApplicationText(text, "work_description", "Р С›Р С—Р С‘РЎРѓР В°Р Р…Р С‘Р Вµ РЎР‚Р В°Р В±Р С•РЎвЂљ"));
    }
    private static IReadOnlyList<ExtractedFieldResult> BuildFields(params ExtractedFieldResult?[] fields)
        => fields.Where(static field => field is not null).Cast<ExtractedFieldResult>().ToList();

    private static ExtractedFieldResult? TryExtractNumber(string text, string fieldKey, string label, IReadOnlyList<string> anchors)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (!ContainsAny(line, anchors))
                continue;

            var index = line.IndexOf("№", StringComparison.Ordinal);
            if (index >= 0)
            {
                var value = CleanValue(line[(index + 1)..]);
                if (!string.IsNullOrWhiteSpace(value))
                    return BuildField(fieldKey, label, TrimTo(value, 80), 0.88m);
            }

            var tokenMatch = Regex.Match(line, @"(?:Р Р†РІР‚С›РІР‚вЂњ|#|Р В Р вЂ¦Р В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ|no\.?)\s*(?<value>[A-Za-zР В РЎвЂ™-Р В Р вЂЎР В Р’В°-Р РЋР РЏР В Р С“Р РЋРІР‚В0-9/-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
            if (!line.Contains("Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("invoice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ContainsAny(line, ["Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™", "Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™", "Р В РЎвЂќР В РЎвЂўР РЋР вЂљР РЋР вЂљ", "Р В РЎвЂќР В РЎвЂўР РЋР вЂљР РЋР вЂљ.", "Р В Р’В±Р В Р’В°Р В Р вЂ¦Р В РЎвЂќ Р В РЎвЂ”Р В РЎвЂўР В Р’В»Р РЋРЎвЂњР РЋРІР‚РЋР В Р’В°Р РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР РЏ", "Р В Р’В±Р В РЎвЂР В РЎвЂќ"]) &&
                !line.Contains("Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("invoice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var explicitMatch = Regex.Match(
                line,
                @"(?:Р РЋР С“Р РЋРІР‚РЋ[Р В Р’ВµР РЋРІР‚В]Р РЋРІР‚С™(?:\s+Р В Р вЂ¦Р В Р’В°\s+Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р РЋРЎвЂњ)?|invoice)(?:\s*(?:Р Р†РІР‚С›РІР‚вЂњ|#|no\.?|Р В Р вЂ¦Р В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ)\s*)(?<value>[A-Za-zР В РЎвЂ™-Р В Р вЂЎР В Р’В°-Р РЋР РЏР В Р С“Р РЋРІР‚В0-9/-]{3,30})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (explicitMatch.Success)
            {
                var candidate = CleanValue(explicitMatch.Groups["value"].Value);
                if (!IsLikelyBankAccount(candidate) && !LooksLikeAmount(candidate))
                    return BuildField("invoice_number", "Р В РЎСљР В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р’В°", candidate, 0.88m);
            }

            foreach (var token in Regex.Matches(line, @"[A-Za-zР В РЎвЂ™-Р В Р вЂЎР В Р’В°-Р РЋР РЏР В Р С“Р РЋРІР‚В0-9/-]{3,30}").Select(match => CleanValue(match.Value)))
            {
                if (string.IsNullOrWhiteSpace(token) || IsLikelyBankAccount(token) || LooksLikeAmount(token))
                    continue;

                if (token.StartsWith("Р РЋР С“Р РЋРІР‚РЋ", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains('-', StringComparison.Ordinal) ||
                    token.Contains('/', StringComparison.Ordinal))
                {
                    return BuildField("invoice_number", "Р В РЎСљР В РЎвЂўР В РЎВР В Р’ВµР РЋР вЂљ Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р’В°", token, 0.74m);
                }
            }
        }

        return null;
    }

    private static ExtractedFieldResult? TryExtractSupplier(string text)
    {
        var field = TryExtractLineValue(text, "supplier", "Р В РЎСџР В РЎвЂўР РЋР С“Р РЋРІР‚С™Р В Р’В°Р В Р вЂ Р РЋРІР‚В°Р В РЎвЂР В РЎвЂќ", ["Р В РЎвЂ”Р В РЎвЂўР РЋР С“Р РЋРІР‚С™Р В Р’В°Р В Р вЂ Р РЋРІР‚В°Р В РЎвЂР В РЎвЂќ", "Р В РЎвЂ”Р В РЎвЂўР В Р’В»Р РЋРЎвЂњР РЋРІР‚РЋР В Р’В°Р РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР Р‰", "Р В РЎвЂР РЋР С“Р В РЎвЂ”Р В РЎвЂўР В Р’В»Р В Р вЂ¦Р В РЎвЂР РЋРІР‚С™Р В Р’ВµР В Р’В»Р РЋР Р‰", "supplier"]);
        if (field is null)
            return null;

        var value = TrimByKeywords(field.SuggestedValue, ["Р В РЎвЂР В Р вЂ¦Р В Р вЂ¦", "Р В РЎвЂќР В РЎвЂ”Р В РЎвЂ”", "Р В Р’В°Р В РўвЂР РЋР вЂљР В Р’ВµР РЋР С“", "Р РЋРІР‚С™Р В Р’ВµР В Р’В»", "Р РЋРІР‚С™Р В Р’ВµР В Р’В»Р В Р’ВµР РЋРІР‚С›Р В РЎвЂўР В Р вЂ¦", "Р РЋР вЂљ/Р РЋР С“", "Р РЋР вЂљ/c"]);
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
        var priorityAnchors = new[] { "Р В РЎвЂќ Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р В Р’Вµ", "Р В РЎвЂР РЋРІР‚С™Р В РЎвЂўР В РЎвЂ“Р В РЎвЂў", "Р В Р вЂ Р РЋР С“Р В Р’ВµР В РЎвЂ“Р В РЎвЂў Р В РЎвЂќ Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р В Р’Вµ", "Р В РЎвЂР РЋРІР‚С™Р В РЎвЂўР В РЎвЂ“Р В РЎвЂў Р В РЎвЂќ Р В РЎвЂўР В РЎвЂ”Р В Р’В»Р В Р’В°Р РЋРІР‚С™Р В Р’Вµ" };
        var fallbackAnchors = new[] { "Р РЋР С“Р РЋРЎвЂњР В РЎВР В РЎВР В Р’В°", "Р В Р вЂ Р РЋР С“Р В Р’ВµР В РЎвЂ“Р В РЎвЂў", "total", "amount" };

        foreach (var line in EnumerateLines(text))
        {
            if (ContainsAny(line, ["Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", "Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", "Р РЋР С“Р РЋРІР‚РЋ. Р Р†РІР‚С›РІР‚вЂњ", "Р В Р’В±Р В РЎвЂР В РЎвЂќ", "Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™", "Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™", "Р В РЎвЂќР В РЎвЂ”Р В РЎвЂ”", "Р В РЎвЂР В Р вЂ¦Р В Р вЂ¦"]))
                continue;

            if (!ContainsAny(line, priorityAnchors))
                continue;

            bestAmount = MaxAmountFromLine(line, bestAmount);
        }

        if (bestAmount is null)
        {
            foreach (var line in EnumerateLines(text))
            {
                if (ContainsAny(line, ["Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", "Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™ Р Р†РІР‚С›РІР‚вЂњ", "Р РЋР С“Р РЋРІР‚РЋ. Р Р†РІР‚С›РІР‚вЂњ", "Р В Р’В±Р В РЎвЂР В РЎвЂќ", "Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР В Р’ВµР РЋРІР‚С™", "Р РЋР вЂљР В Р’В°Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™Р В Р вЂ¦Р РЋРІР‚в„–Р В РІвЂћвЂ“ Р РЋР С“Р РЋРІР‚РЋР РЋРІР‚ВР РЋРІР‚С™", "Р В РЎвЂќР В РЎвЂ”Р В РЎвЂ”", "Р В РЎвЂР В Р вЂ¦Р В Р вЂ¦"]))
                    continue;

                if (!ContainsAny(line, fallbackAnchors))
                    continue;

                bestAmount = MaxAmountFromLine(line, bestAmount);
            }
        }

        if (bestAmount is null)
        {
            foreach (Match match in Regex.Matches(text, @"(?<value>\d+[.,]\d{2})\s*(?:Р РЋР вЂљР РЋРЎвЂњР В Р’В±|Р РЋР вЂљ\.|Р Р†РІР‚С™Р вЂ¦)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
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

        return BuildField("amount", "Р В Р Р‹Р РЋРЎвЂњР В РЎВР В РЎВР В Р’В°", bestAmount.Value.ToString("0.##", CultureInfo.InvariantCulture), bestAmount >= 1000m ? 0.86m : 0.62m);
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
            if (!line.Contains("Р В РЎвЂ”Р РЋР вЂљР В РЎвЂўР РЋРІвЂљВ¬Р РЋРЎвЂњ", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = CleanValue(line);
            if (!string.IsNullOrWhiteSpace(value))
                return BuildField(fieldKey, label, TrimTo(value, 260), 0.78m);
        }

        foreach (var line in EnumerateLines(text))
        {
            if (!line.Contains("Р В Р’В·Р В Р’В°Р РЋР РЏР В Р вЂ Р В Р’В»Р В Р’ВµР В Р вЂ¦Р В РЎвЂР В Р’Вµ", StringComparison.OrdinalIgnoreCase))
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

        foreach (var separator in new[] { ":", "Р Р†Р вЂљРІР‚Сњ", "-", "Р Р†Р вЂљРІР‚Сљ" })
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

        return $"{value[..Math.Max(0, maxLength - 1)].Trim()}Р Р†Р вЂљР’В¦";
    }
}
