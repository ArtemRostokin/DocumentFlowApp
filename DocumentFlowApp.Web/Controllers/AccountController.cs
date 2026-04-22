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

    public AccountController(IAuthService authService)
    {
        _authService = authService;
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

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToRoleHome(authResult.Role);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookieName);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToRoleHome(string? role = null)
    {
        var normalizedRole = AppRoles.Normalize(role)
            ?? AppRoles.Normalize(User.Claims.FirstOrDefault(c => c.Type == "df_role")?.Value)
            ?? AppRoles.Normalize(User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value)
            ?? AppRoles.Employee;

        if (normalizedRole == AppRoles.Admin)
            return RedirectToAction("Index", "Admin");

        if (normalizedRole == AppRoles.Employee)
            return RedirectToAction("Index", "Home");

        return RedirectToAction("Index", "Home");
    }
}
