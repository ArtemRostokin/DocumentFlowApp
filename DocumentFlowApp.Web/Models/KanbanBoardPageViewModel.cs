using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class KanbanBoardPageViewModel
{
    public string Title { get; init; } = "Документооборот";
    public string PeriodLabel { get; init; } = string.Empty;
    public string? SearchQuery { get; init; }
    public DocumentType? SelectedType { get; init; }
    public int TotalDocuments { get; init; }
    public string TotalLabel { get; init; } = "Всего документов";
    public string? Notice { get; init; }
    public string? SuccessMessage { get; init; }
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
    public required string DueLabel { get; init; }
    public required string DueClass { get; init; }
}
