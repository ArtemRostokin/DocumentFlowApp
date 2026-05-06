using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class KanbanBoardPageViewModel
{
    public string Title { get; init; } = "Документооборот";
    public string PeriodLabel { get; init; } = string.Empty;
    public string? SearchQuery { get; init; }
    public DocumentType? SelectedType { get; init; }
    public string? SelectedStatus { get; init; }
    public int? SelectedPriority { get; init; }
    public string? SelectedPeriod { get; init; }
    public bool HasActiveFilters { get; init; }
    public int TotalDocuments { get; init; }
    public string TotalLabel { get; init; } = "Всего документов";
    public string? Notice { get; init; }
    public string? SuccessMessage { get; init; }
    public string ViewModeLabel { get; init; } = string.Empty;
    public string ViewModeDescription { get; init; } = string.Empty;
    public int ApprovalCount { get; init; }
    public int InWorkCount { get; init; }
    public int CompletedCount { get; init; }
    public IReadOnlyList<KanbanColumnViewModel> Columns { get; init; } = [];
}

public sealed class KanbanColumnViewModel
{
    public required string Title { get; init; }
    public required string AccentClass { get; init; }
    public required DocumentStatus Status { get; init; }
    public required IReadOnlyList<KanbanCardViewModel> Documents { get; init; }
    public bool IsEmptyPlaceholder { get; init; }
}

public sealed class KanbanCardViewModel
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Author { get; init; }
    public required string TypeLabel { get; init; }
    public required string TypeClass { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusClass { get; init; }
    public required string StatusIcon { get; init; }
    public int Progress { get; init; }
    public required string ProgressLabel { get; init; }
    public required string StageHint { get; init; }
    public required string DueLabel { get; init; }
    public required string DueClass { get; init; }
}
