using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class DocumentStatistic
{
    [Key]
    public int StatId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int ViewCount { get; set; }
    public int EditCount { get; set; }
    public decimal? SearchRankScore { get; set; }
    public double? AvgProcessingTime { get; set; }
    public decimal? UserEngagementScore { get; set; }
    public DateTime? LastAccessed { get; set; }
}