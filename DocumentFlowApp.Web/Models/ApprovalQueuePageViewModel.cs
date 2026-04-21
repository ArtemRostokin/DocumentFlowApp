namespace DocumentFlowApp.Web.Models;

public sealed class ApprovalQueuePageViewModel
{
    public int PendingCount { get; init; }
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ApprovalQueueItemViewModel> Documents { get; init; } = [];
}

public sealed class ApprovalQueueItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CreatedAtLabel { get; init; } = string.Empty;
    public string DueDateLabel { get; init; } = string.Empty;
    public string AuthorLabel { get; init; } = string.Empty;
}

public sealed class ApprovalActionInputModel
{
    public string? Decision { get; init; }
    public string? Comment { get; init; }
}