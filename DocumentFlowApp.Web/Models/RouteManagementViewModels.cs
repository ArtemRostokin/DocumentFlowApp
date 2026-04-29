using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class RouteTemplateOptionViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? DocumentType { get; init; }
    public string ApproverSummary { get; init; } = string.Empty;
}

public sealed class ApprovalRouteStepViewModel
{
    public int? DocumentApprovalStepId { get; init; }
    public int? RouteStepId { get; init; }
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ApproverRole { get; init; } = string.Empty;
    public string? ApproverSpecialization { get; init; }
    public string ApproverSpecializationLabel { get; init; } = string.Empty;
    public int? ApproverUserId { get; init; }
    public string ApproverDisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public string? Comment { get; init; }
}

public sealed class RouteTemplateAdminListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DocumentType? DocumentType { get; init; }
    public string? Department { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<RouteTemplateStepAdminViewModel> Steps { get; init; } = [];
}

public sealed class RouteTemplateStepAdminViewModel
{
    public int RouteStepId { get; init; }
    public int StepOrder { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ApproverRole { get; init; } = string.Empty;
    public string? ApproverSpecialization { get; init; }
    public string ApproverSpecializationLabel { get; init; } = string.Empty;
    public int? ApproverUserId { get; init; }
    public string ApproverDisplayName { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
}

public sealed class RouteApproverOptionViewModel
{
    public int UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string? ApprovalSpecialization { get; init; }
    public string ApprovalSpecializationLabel { get; init; } = string.Empty;
}

public sealed class CreateRouteTemplateAdminInputModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentType? DocumentType { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
}

public sealed class AddRouteStepAdminInputModel
{
    public int RouteTemplateId { get; set; }
    public int StepOrder { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string? ApproverSpecialization { get; set; }
    public int? ApproverUserId { get; set; }
    public bool IsRequired { get; set; } = true;
}

public sealed class UpdateRouteTemplateAdminInputModel
{
    public int RouteTemplateId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}
