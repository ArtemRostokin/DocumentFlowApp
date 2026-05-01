using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

public sealed class FakeAiClassifier : IAiClassifier
{
    public AiClassificationResult NextClassificationResult { get; set; } = new()
    {
        SuggestedType = DocumentType.Contract,
        ConfidenceScore = 0.91m,
        Summary = "Тестовая классификация",
        SuggestedTags = ["incoming", "ai-auto-classified"]
    };

    public IReadOnlyList<AiFieldSuggestionResult> Suggestions { get; set; } =
    [
        new AiFieldSuggestionResult
        {
            FieldKey = "type",
            Label = "Тип",
            SuggestedValue = DocumentType.Contract.ToString(),
            ConfidenceScore = 0.91m
        }
    ];

    public AiClassificationResult ClassifyIncomingDocument(string fileName, string? extractedText = null) => NextClassificationResult;

    public IReadOnlyList<AiFieldSuggestionResult> BuildSuggestions(Document document) => Suggestions;
}
