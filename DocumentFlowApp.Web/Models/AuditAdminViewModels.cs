namespace DocumentFlowApp.Web.Models;

public sealed class AuditAdminPageViewModel
{
    public string? SelectedActivityType { get; init; }
    public int? SelectedDocumentId { get; init; }
    public int TotalCount { get; init; }
    public int TodayCount { get; init; }
    public int DistinctDocumentsCount { get; init; }
    public IReadOnlyList<AuditActivityTypeOptionViewModel> ActivityTypes { get; init; } = [];
    public IReadOnlyList<AuditEntryItemViewModel> Entries { get; init; } = [];
}

public sealed class AuditActivityTypeOptionViewModel
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class AuditEntryItemViewModel
{
    public int Id { get; init; }
    public DateTime? ActivityDateUtc { get; init; }
    public string ActivityType { get; init; } = string.Empty;
    public string ActivityTypeLabel { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
