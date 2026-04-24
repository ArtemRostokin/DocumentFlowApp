using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentFlowApp.Web.Controllers;

public class AccountController : Controller
{
    private const string AuthCookieName = "df_auth_token";
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;

    public AccountController(IAuthService authService, IAuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
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
}
