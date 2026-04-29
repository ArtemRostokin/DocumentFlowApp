using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class User
{
    [Key]
    public int UserId { get; set; }

    public int? RoleId { get; set; }
    public Role? Role { get; set; }

    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ApprovalSpecialization { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<DocumentStatistic> DocumentStatistics { get; set; } = new List<DocumentStatistic>();
    public ICollection<DocumentActivity> DocumentActivities { get; set; } = new List<DocumentActivity>();
    public ICollection<RouteStep> RouteStepsAsApprover { get; set; } = new List<RouteStep>();
    public ICollection<DocumentApprovalStep> ApprovalStepsAsApprover { get; set; } = new List<DocumentApprovalStep>();
    public ICollection<DocumentApprovalStep> ApprovalStepsAsActor { get; set; } = new List<DocumentApprovalStep>();
    public ICollection<SearchHistory> SearchHistories { get; set; } = new List<SearchHistory>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
