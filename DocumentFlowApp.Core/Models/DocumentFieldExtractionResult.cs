using System.Collections.Generic;
using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Core.Models;

public sealed class ExtractedFieldResult
{
    public required string FieldKey { get; init; }
    public required string Label { get; init; }
    public required string SuggestedValue { get; init; }
    public required decimal ConfidenceScore { get; init; }
    public string Source { get; init; } = "rule-based";

    public int ConfidencePercent => (int)Math.Round(ConfidenceScore * 100m, MidpointRounding.AwayFromZero);
}

public sealed class DocumentFieldExtractionResult
{
    public required DocumentType DocumentType { get; init; }
    public IReadOnlyList<ExtractedFieldResult> Fields { get; init; } = [];
}

public sealed class DocumentAiSnapshot
{
    public string SuggestedType { get; init; } = DocumentType.Other.ToString();
    public string EffectiveType { get; init; } = DocumentType.Other.ToString();
    public int ConfidencePercent { get; init; }
    public bool ShouldAutoAssignType { get; init; }
    public bool RequiresManualReview { get; init; }
    public List<ExtractedFieldResult> Fields { get; init; } = [];
}
