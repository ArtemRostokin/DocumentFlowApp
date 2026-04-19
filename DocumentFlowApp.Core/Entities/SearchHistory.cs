using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class SearchHistory
{
    [Key]
    public int SearchId { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string? QueryText { get; set; }
    public string? SearchFilters { get; set; } // json
    public int ResultCount { get; set; }
    public DateTime? SearchDate { get; set; }
    public int? SuccessRating { get; set; }
    public string? ClickedDocuments { get; set; } // serialized list
}