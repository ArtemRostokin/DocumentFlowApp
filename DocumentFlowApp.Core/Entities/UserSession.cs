using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class UserSession
{
    [Key]
    public int SessionId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string? Token { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ExpiresDate { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; }
}