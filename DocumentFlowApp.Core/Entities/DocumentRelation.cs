using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class DocumentRelation
{
    [Key]
    public int RelationId { get; set; }

    public int SourceDocumentId { get; set; }
    public Document? SourceDocument { get; set; }

    public int TargetDocumentId { get; set; }
    public Document? TargetDocument { get; set; }

    public string? RelationType { get; set; }
    public decimal? AiConfidenceScore { get; set; }
    public DateTime? CreatedDate { get; set; }
}