using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class RouteTemplate
{
    [Key]
    public int RouteTemplateId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? DocumentType { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedDate { get; set; }

    public ICollection<RouteStep> Steps { get; set; } = new List<RouteStep>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<DocumentApprovalStep> DocumentApprovalSteps { get; set; } = new List<DocumentApprovalStep>();
}
