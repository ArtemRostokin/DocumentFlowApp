using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class Template
{
    [Key]
    public int TemplateId { get; set; }

    public string Name { get; set; } = null!;
    public string? Content { get; set; }
    public string? Category { get; set; }

    // raw JSON as string; можно заменить на JsonDocument при необходимости
    public string? AiSuggestedFields { get; set; }

    public int UsageCount { get; set; }
    public decimal? SuccessRate { get; set; }
    public DateTime? CreatedDate { get; set; }

    public ICollection<Document>? Documents { get; set; } = new List<Document>();
}