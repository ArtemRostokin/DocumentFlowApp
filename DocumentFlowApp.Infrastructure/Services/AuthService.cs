using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocumentFlowApp.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(ApplicationDbContext context, IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failed("Введите email и пароль.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
            return AuthResult.Failed("Пользователь с таким email не найден.");

        if (!user.IsActive)
            return AuthResult.Failed("Пользователь деактивирован.");

        if (!IsPasswordValid(user, password))
            return AuthResult.Failed("Неверный пароль.");

        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiresMinutes);
        var token = CreateToken(user, expiresAtUtc);

        return AuthResult.Success(
            token,
            expiresAtUtc,
            user.UserId,
            user.UserName,
            user.Email,
            user.Role?.RoleName);
    }

    private bool IsPasswordValid(User user, string enteredPassword)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        if (string.Equals(user.PasswordHash, enteredPassword, StringComparison.Ordinal))
            return true;

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, enteredPassword);
        return verify is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    private string CreateToken(User user, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Role?.RoleName))
            claims.Add(new Claim(ClaimTypes.Role, user.Role.RoleName));

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
