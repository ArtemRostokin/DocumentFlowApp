using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class RouteStep
{
    [Key]
    public int RouteStepId { get; set; }

    public int RouteTemplateId { get; set; }
    public RouteTemplate? RouteTemplate { get; set; }

    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string ApproverRole { get; set; } = null!;
    public string? ApproverSpecialization { get; set; }
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }
    public bool IsRequired { get; set; }

    public ICollection<DocumentApprovalStep> DocumentApprovalSteps { get; set; } = new List<DocumentApprovalStep>();
}
