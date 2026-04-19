using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class Permission
{
    [Key]
    public int PermissionId { get; set; }

    public string PermissionName { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}