using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Core.Models;

public sealed class AiClassificationResult
{
    public required DocumentType SuggestedType { get; init; }
    public required decimal ConfidenceScore { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string[] SuggestedTags { get; init; } = [];

    public int ConfidencePercent => (int)Math.Round(ConfidenceScore * 100m, MidpointRounding.AwayFromZero);
    public bool ShouldAutoAssignType => ConfidenceScore >= 0.85m;
    public bool RequiresManualReview => ConfidenceScore >= 0.60m && ConfidenceScore < 0.85m;
}

public sealed class AiFieldSuggestionResult
{
    public required string FieldKey { get; init; }
    public required string Label { get; init; }
    public required string SuggestedValue { get; init; }
    public required decimal ConfidenceScore { get; init; }

    public int ConfidencePercent => (int)Math.Round(ConfidenceScore * 100m, MidpointRounding.AwayFromZero);
}
