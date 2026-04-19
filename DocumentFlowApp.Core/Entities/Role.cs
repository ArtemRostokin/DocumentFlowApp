using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class Role
{
    [Key]
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}