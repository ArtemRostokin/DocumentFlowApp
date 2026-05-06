using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Web.Controllers;

public class AccountController : Controller
{
    private const string AuthCookieName = "df_auth_token";
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AccountController> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountController(
        IAuthService authService,
        IAuditService auditService,
        ApplicationDbContext dbContext,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _auditService = auditService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleHome();

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var authResult = await _authService.LoginAsync(model.Email, model.Password, cancellationToken);
        if (!authResult.IsSuccess || string.IsNullOrWhiteSpace(authResult.Token) || !authResult.ExpiresAtUtc.HasValue)
        {
            ModelState.AddModelError(string.Empty, authResult.ErrorMessage ?? "Не удалось выполнить вход.");
            return View(model);
        }

        Response.Cookies.Append(AuthCookieName, authResult.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = model.RememberMe ? authResult.ExpiresAtUtc : null
        });

        await _auditService.LogSystemActivityAsync(
            authResult.UserId,
            AuditActivityTypes.UserLogin,
            $"Пользователь {authResult.UserName} выполнил вход в систему.",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToRoleHome(authResult.Role);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToAction(nameof(Login));

        return View(await BuildProfilePageAsync(user, cancellationToken));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] UpdateOwnProfileInputModel input, CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, profileInput: input));

        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(x => x.UserId != user.UserId && x.Email.ToLower() == normalizedEmail, cancellationToken))
        {
            ModelState.AddModelError("Profile.Email", "Этот email уже используется другим пользователем.");
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, profileInput: input));
        }

        try
        {
            var previousEmail = user.Email;
            var previousFullName = BuildFullName(user.FirstName, user.LastName);

            user.FirstName = input.FirstName.Trim();
            user.LastName = input.LastName.Trim();
            user.Email = input.Email.Trim();

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                user.UserId,
                AuditActivityTypes.UserUpdated,
                $"Пользователь обновил свой профиль: ФИО {previousFullName} -> {BuildFullName(user.FirstName, user.LastName)}, email {previousEmail} -> {user.Email}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Профиль обновлен.";
            return RedirectToAction(nameof(Profile));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить профиль пользователя {UserId}", user.UserId);
            ModelState.AddModelError(string.Empty, "Не удалось сохранить изменения профиля.");
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, profileInput: input));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangeOwnPasswordInputModel input, CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, passwordInput: input));

        if (!IsPasswordValid(user, input.CurrentPassword))
        {
            ModelState.AddModelError("Password.CurrentPassword", "Текущий пароль указан неверно.");
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, passwordInput: input));
        }

        if (string.Equals(input.CurrentPassword, input.NewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError("Password.NewPassword", "Новый пароль должен отличаться от текущего.");
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, passwordInput: input));
        }

        try
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, input.NewPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                user.UserId,
                AuditActivityTypes.UserPasswordChanged,
                $"Пользователь {user.UserName} изменил свой пароль.",
                cancellationToken);

            TempData["SuccessMessage"] = "Пароль обновлен.";
            return RedirectToAction(nameof(Profile));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось изменить пароль пользователя {UserId}", user.UserId);
            ModelState.AddModelError(string.Empty, "Не удалось изменить пароль.");
            return View("Profile", await BuildProfilePageAsync(user, cancellationToken, passwordInput: input));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserName = User.Identity?.Name ?? "Неизвестный пользователь";

        Response.Cookies.Delete(AuthCookieName);

        await _auditService.LogSystemActivityAsync(
            currentUserId,
            AuditActivityTypes.UserLogout,
            $"Пользователь {currentUserName} вышел из системы.",
            cancellationToken);

        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToRoleHome(string? role = null)
    {
        var normalizedRole = AppRoles.Normalize(role)
            ?? AppRoles.Normalize(User.Claims.FirstOrDefault(c => c.Type == "df_role")?.Value)
            ?? AppRoles.Normalize(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value)
            ?? AppRoles.Employee;

        if (normalizedRole == AppRoles.Admin)
            return RedirectToAction("Index", "Admin");

        if (normalizedRole == AppRoles.Employee)
            return RedirectToAction("Index", "Home");

        return RedirectToAction("Index", "Home");
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return int.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task<User?> FindCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return null;

        return await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == currentUserId.Value, cancellationToken);
    }

    private async Task<ProfilePageViewModel> BuildProfilePageAsync(
        User user,
        CancellationToken cancellationToken,
        UpdateOwnProfileInputModel? profileInput = null,
        ChangeOwnPasswordInputModel? passwordInput = null)
    {
        await Task.CompletedTask;

        return new ProfilePageViewModel
        {
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
            Summary = new ProfileSummaryViewModel
            {
                UserName = user.UserName,
                Email = user.Email,
                FullName = BuildFullName(user.FirstName, user.LastName),
                RoleName = user.Role?.RoleName ?? "Без роли",
                ApprovalSpecializationLabel = ApprovalSpecializations.GetLabel(user.ApprovalSpecialization),
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedDateUtc = user.CreatedDate,
                LastLoginUtc = user.LastLogin
            },
            Profile = profileInput ?? new UpdateOwnProfileInputModel
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email
            },
            Password = passwordInput ?? new ChangeOwnPasswordInputModel()
        };
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

    private static string BuildFullName(string? firstName, string? lastName)
    {
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(fullName) ? "Не заполнено" : fullName;
    }
}
