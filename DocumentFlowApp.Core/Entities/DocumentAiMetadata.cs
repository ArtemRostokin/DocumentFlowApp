using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class DocumentAiMetadata
{
    [Key]
    public int MetadataId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int? ModelId { get; set; }
    public AiModel? Model { get; set; }

    public string? AiSummary { get; set; }
    public string? AiTags { get; set; }
    public decimal? ConfidenceScore { get; set; }

    // extracted JSON as string
    public string? ExtractedEntities { get; set; }

    public DateTime? ProcessingDate { get; set; }
}