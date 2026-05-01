using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

public sealed class FakeDocumentFieldExtractor : IDocumentFieldExtractor
{
    public DocumentFieldExtractionResult NextResult { get; set; } = new()
    {
        DocumentType = DocumentType.Contract,
        Fields =
        [
            new ExtractedFieldResult
            {
                FieldKey = "contract_number",
                Label = "Номер договора",
                SuggestedValue = "42/2026",
                ConfidenceScore = 0.86m
            }
        ]
    };

    public DocumentFieldExtractionResult Extract(DocumentType documentType, string? extractedText, string? fileName = null)
    {
        return new DocumentFieldExtractionResult
        {
            DocumentType = documentType,
            Fields = NextResult.Fields
        };
    }
}
