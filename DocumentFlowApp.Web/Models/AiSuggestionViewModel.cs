namespace DocumentFlowApp.Web.Models;

public sealed class AiSuggestionViewModel
{
    public required string FieldKey { get; init; } // title|type|duedate|description|priority|tags
    public required string Label { get; init; }
    public required string SuggestedValue { get; init; }
    public int Confidence { get; init; }
    public bool CanApply { get; init; }
    public bool IsResolved { get; init; } = true;
    public string Source { get; init; } = "rule-based";
    public bool IsManualOverride { get; init; }
}

