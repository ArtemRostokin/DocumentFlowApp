namespace DocumentFlowApp.Core.Models;

public sealed class TextExtractionResult
{
    public bool IsSuccessful { get; init; }
    public bool IsFallback { get; init; }
    public string Provider { get; init; } = "none";
    public string ExtractedText { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public string Summary { get; init; } = string.Empty;
}
