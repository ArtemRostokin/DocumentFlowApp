using System.Security.Claims;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Web.Controllers;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using DocumentFlowApp.Tests.TestDoubles;

namespace DocumentFlowApp.Tests;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_Redirects_Employee_To_Login_When_UserId_Missing()
    {
        var service = new FakeDocumentService();
        var controller = CreateController(service, CreatePrincipal(("df_role", "Employee")));

        var result = await controller.Index(null, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public async Task Index_Shows_Employee_Board_Only_For_Assigned_Documents()
    {
        var service = new FakeDocumentService();
        service.Documents.AddRange(
        [
            CreateDocument(1, 10, DocumentStatus.OnApproval, "Approval"),
            CreateDocument(2, 10, DocumentStatus.InWork, "Work"),
            CreateDocument(3, 99, DocumentStatus.Completed, "Other user"),
            CreateDocument(4, 10, DocumentStatus.Draft, "Draft should be hidden")
        ]);

        var controller = CreateController(service, CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Kanban", view.ViewName);
        var model = Assert.IsType<KanbanBoardPageViewModel>(view.Model);
        Assert.Equal("Моя работа", model.Title);
        Assert.Equal(2, model.TotalDocuments);
        Assert.Equal(4, model.Columns.Count);
        Assert.Contains(model.Columns, x => x.Status == DocumentStatus.OnApproval && x.Documents.Any(d => d.Id == 1));
        Assert.Contains(model.Columns, x => x.Status == DocumentStatus.InWork && x.Documents.Any(d => d.Id == 2));
        Assert.DoesNotContain(model.Columns.SelectMany(x => x.Documents), x => x.Id == 3 || x.Id == 4);
    }

    [Fact]
    public async Task Index_Shows_Manager_Board_With_All_Documents()
    {
        var service = new FakeDocumentService();
        service.Documents.AddRange(
        [
            CreateDocument(1, 10, DocumentStatus.Draft, "Draft"),
            CreateDocument(2, 11, DocumentStatus.Archived, "Archived")
        ]);

        var controller = CreateController(service, CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<KanbanBoardPageViewModel>(view.Model);
        Assert.Equal("Документооборот", model.Title);
        Assert.Equal(2, model.TotalDocuments);
        Assert.Equal(6, model.Columns.Count);
    }

    private static HomeController CreateController(FakeDocumentService service, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user
        };

        return new HomeController(service, NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims.Select(x => new Claim(x.Type, x.Value)), "TestAuth"));
    }

    private static Document CreateDocument(int id, int userId, DocumentStatus status, string title)
    {
        return new Document
        {
            DocumentId = id,
            UserId = userId,
            User = new User { UserId = userId, UserName = $"user{userId}" },
            Title = title,
            ExtractedText = $"Description {id}",
            DocumentType = DocumentType.Other.ToString(),
            Status = status.ToString(),
            CreatedDate = DateTime.UtcNow
        };
    }
}
