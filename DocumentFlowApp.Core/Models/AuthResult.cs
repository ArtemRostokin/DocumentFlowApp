namespace DocumentFlowApp.Core.Models;

public class AuthResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Token { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }

    public static AuthResult Failed(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };

    public static AuthResult Success(
        string token,
        DateTime expiresAtUtc,
        int userId,
        string userName,
        string email,
        string? role) => new()
    {
        IsSuccess = true,
        Token = token,
        ExpiresAtUtc = expiresAtUtc,
        UserId = userId,
        UserName = userName,
        Email = email,
        Role = role
    };
}
