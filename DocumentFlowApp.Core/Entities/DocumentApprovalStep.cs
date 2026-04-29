using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class DocumentApprovalStep
{
    [Key]
    public int DocumentApprovalStepId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int? RouteTemplateId { get; set; }
    public RouteTemplate? RouteTemplate { get; set; }

    public int? RouteStepId { get; set; }
    public RouteStep? RouteStep { get; set; }

    public int StepOrder { get; set; }
    public string Title { get; set; } = null!;
    public string ApproverRole { get; set; } = null!;
    public string? ApproverSpecialization { get; set; }
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }

    public string Status { get; set; } = "Pending";
    public bool IsCurrent { get; set; }
    public string? Comment { get; set; }
    public DateTime? ActionDate { get; set; }
    public int? ActionByUserId { get; set; }
    public User? ActionByUser { get; set; }
}
