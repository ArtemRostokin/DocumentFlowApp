using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class DocumentActivity
{
    [Key]
    public int ActivityId { get; set; }

    public int? DocumentId { get; set; }
    public Document? Document { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string? ActivityType { get; set; }
    public DateTime? ActivityDate { get; set; }
    public string? Details { get; set; }
}
