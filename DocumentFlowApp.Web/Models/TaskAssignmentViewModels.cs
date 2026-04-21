namespace DocumentFlowApp.Web.Models;

public sealed class ExecutorOptionViewModel
{
    public int UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class AssignmentPanelViewModel
{
    public bool CanAssign { get; init; }
    public int? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public IReadOnlyList<ExecutorOptionViewModel> Executors { get; init; } = [];
}

public sealed class MyTasksPageViewModel
{
    public int TotalTasks { get; init; }
    public int ApprovedTasks { get; init; }
    public int InWorkTasks { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<MyTaskCardViewModel> Tasks { get; init; } = [];
}

public sealed class MyTaskCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string DueDateLabel { get; init; } = string.Empty;
    public bool CanStartWork { get; init; }
    public bool CanComplete { get; init; }
}