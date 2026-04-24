using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Web.Controllers;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using DocumentFlowApp.Tests.TestDoubles;

namespace DocumentFlowApp.Tests;

public class AccountControllerTests
{
    [Fact]
    public async Task Login_Redirects_Admin_To_AdminHome_And_Writes_Audit()
    {
        var authService = new FakeAuthService
        {
            Result = AuthResult.Success("token", DateTime.UtcNow.AddHours(1), 7, "admin", "admin@local", "Admin")
        };
        var auditService = new FakeAuditService();
        var controller = CreateController(authService, auditService);

        var result = await controller.Login(
            new LoginViewModel { Email = "admin@local", Password = "secret" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        var entry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditActivityTypes.UserLogin, entry.ActivityType);
        Assert.Equal(7, entry.UserId);
    }

    [Fact]
    public async Task Login_Redirects_To_ReturnUrl_When_Local()
    {
        var authService = new FakeAuthService
        {
            Result = AuthResult.Success("token", DateTime.UtcNow.AddHours(1), 5, "manager", "manager@local", "Manager")
        };
        var controller = CreateController(authService, new FakeAuditService());

        var result = await controller.Login(
            new LoginViewModel
            {
                Email = "manager@local",
                Password = "secret",
                ReturnUrl = "/Documents/Create"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Documents/Create", redirect.Url);
    }

    [Fact]
    public async Task Login_Returns_View_On_Failed_Auth()
    {
        var authService = new FakeAuthService
        {
            Result = AuthResult.Failed("bad credentials")
        };
        var controller = CreateController(authService, new FakeAuditService());

        var result = await controller.Login(
            new LoginViewModel { Email = "user@local", Password = "bad" },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("bad credentials", controller.ModelState[string.Empty]!.Errors[0].ErrorMessage);
        Assert.IsType<LoginViewModel>(view.Model);
    }

    [Fact]
    public async Task Logout_Deletes_Cookie_And_Writes_Audit()
    {
        var auditService = new FakeAuditService();
        var controller = CreateController(new FakeAuthService(), auditService, CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "12"), (ClaimTypes.Name, "manager")));

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var entry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditActivityTypes.UserLogout, entry.ActivityType);
        Assert.Equal(12, entry.UserId);
    }

    private static AccountController CreateController(FakeAuthService authService, FakeAuditService auditService, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
        };

        var controller = new AccountController(authService, auditService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            Url = new UrlHelper(new ActionContext(
                httpContext,
                new RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor())),
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims.Select(x => new Claim(x.Type, x.Value)), "TestAuth"));
    }
}
