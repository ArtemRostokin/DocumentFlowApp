namespace DocumentFlowApp.Web.Models;

public sealed class AdminDashboardPageViewModel
{
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int TotalDocuments { get; init; }
    public int PendingApprovalDocuments { get; init; }
    public int InWorkDocuments { get; init; }
    public int CompletedDocuments { get; init; }
    public int AuditEventsToday { get; init; }
    public int ActiveNomenclatureCases { get; init; }
    public int ActiveNomenclatureRules { get; init; }
    public IReadOnlyList<AdminRecentActivityItemViewModel> RecentActivities { get; init; } = [];
}

public sealed class AdminRecentActivityItemViewModel
{
    public DateTime? ActivityDateUtc { get; init; }
    public string ActivityTypeLabel { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public int? DocumentId { get; init; }
}

public sealed class RoutesAdminPageViewModel
{
    public int PendingApprovalDocuments { get; init; }
    public int ApprovedDocuments { get; init; }
    public int InWorkDocuments { get; init; }
    public int CompletedDocuments { get; init; }
    public IReadOnlyList<RouteStageItemViewModel> Stages { get; init; } = [];
    public IReadOnlyList<RouteRoleResponsibilityViewModel> Roles { get; init; } = [];
    public IReadOnlyList<RouteTemplateAdminListItemViewModel> Templates { get; init; } = [];
    public IReadOnlyList<RouteApproverOptionViewModel> Approvers { get; init; } = [];
    public IReadOnlyList<ApprovalSpecializationOptionViewModel> ApprovalSpecializations { get; init; } = [];
    public CreateRouteTemplateAdminInputModel NewTemplate { get; init; } = new();
    public AddRouteStepAdminInputModel NewStep { get; init; } = new();
}

public sealed class RouteStageItemViewModel
{
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusCode { get; init; } = string.Empty;
    public string ResponsibleRole { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class RouteRoleResponsibilityViewModel
{
    public string RoleName { get; init; } = string.Empty;
    public string Responsibilities { get; init; } = string.Empty;
}
