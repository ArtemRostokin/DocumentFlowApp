using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentFlowApp.Core.Entities;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? TemplateId { get; set; }
    public Template? Template { get; set; }

    public string? DocumentType { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileHash { get; set; }
    public string? Tags { get; set; }

    // date-only stored as DateTime (можно использовать DateOnly и конвертер)
    public DateTime? DueDate { get; set; }

    public int? Priority { get; set; }
    public bool IsArchived { get; set; }

    public string? Title { get; set; }
    public string? ExtractedText { get; set; }

    public string? ExecutionComment { get; set; }
    public string? ExecutionResult { get; set; }
    public DateTime? ExecutionStartedAt { get; set; }
    public DateTime? ExecutionCompletedAt { get; set; }
    public string? ExecutionFilePath { get; set; }
    public string? ExecutionFileName { get; set; }

    public ICollection<DocumentAiMetadata> AiMetadata { get; set; } = new List<DocumentAiMetadata>();
    public ICollection<DocumentRelation> SourceRelations { get; set; } = new List<DocumentRelation>();
    public ICollection<DocumentRelation> TargetRelations { get; set; } = new List<DocumentRelation>();
    public ICollection<DocumentStatistic> Statistics { get; set; } = new List<DocumentStatistic>();
    public ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
}
