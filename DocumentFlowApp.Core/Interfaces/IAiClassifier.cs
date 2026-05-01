using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Core.Interfaces;

public interface IAiClassifier
{
    AiClassificationResult ClassifyIncomingDocument(string fileName, string? extractedText = null);

    IReadOnlyList<AiFieldSuggestionResult> BuildSuggestions(Document document);
}
