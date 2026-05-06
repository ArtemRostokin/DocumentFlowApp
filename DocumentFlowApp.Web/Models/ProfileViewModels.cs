using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Web.Models;

public sealed class ProfilePageViewModel
{
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public ProfileSummaryViewModel Summary { get; init; } = new();
    public UpdateOwnProfileInputModel Profile { get; init; } = new();
    public ChangeOwnPasswordInputModel Password { get; init; } = new();
}

public sealed class ProfileSummaryViewModel
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string ApprovalSpecializationLabel { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? LastLoginUtc { get; init; }
}

public sealed class UpdateOwnProfileInputModel
{
    [Required(ErrorMessage = "Введите имя.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите фамилию.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите email.")]
    [EmailAddress(ErrorMessage = "Укажите корректный email.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ChangeOwnPasswordInputModel
{
    [Required(ErrorMessage = "Введите текущий пароль.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите новый пароль.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Новый пароль должен содержать от 8 до 100 символов.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Подтвердите новый пароль.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Подтверждение пароля не совпадает.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
