using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Web.Models;

public sealed class UsersAdminPageViewModel
{
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public CreateUserAdminInputModel NewUser { get; init; } = new();
    public IReadOnlyList<UserAdminItemViewModel> Users { get; init; } = [];
    public IReadOnlyList<RoleOptionViewModel> Roles { get; init; } = [];
}

public sealed class CreateUserAdminInputModel
{
    [Required(ErrorMessage = "Введите логин пользователя.")]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите email пользователя.")]
    [EmailAddress(ErrorMessage = "Укажите корректный email.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите временный пароль.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 100 символов.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите имя пользователя.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите фамилию пользователя.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите роль.")]
    public int? RoleId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserAdminInputModel
{
    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Введите логин пользователя.")]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите email пользователя.")]
    [EmailAddress(ErrorMessage = "Укажите корректный email.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите имя пользователя.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите фамилию пользователя.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите роль.")]
    public int? RoleId { get; set; }

    public bool IsActive { get; set; }
}

public sealed class ResetUserPasswordAdminInputModel
{
    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Введите новый пароль.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 100 символов.")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UserAdminItemViewModel
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public int? RoleId { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? LastLoginUtc { get; init; }
}

public sealed class RoleOptionViewModel
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
}
