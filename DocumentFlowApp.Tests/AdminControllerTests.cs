using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Tests.TestDoubles;
using DocumentFlowApp.Web.Controllers;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentFlowApp.Tests;

public class AdminControllerTests
{
    [Fact]
    public async Task Audit_Returns_Entries_And_System_Event_Counts()
    {
        await using var dbContext = CreateDbContext();
        SeedAuditData(dbContext);
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Audit(null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditAdminPageViewModel>(view.Model);
        Assert.Equal(3, model.TotalCount);
        Assert.Equal(1, model.SystemEventsCount);
        Assert.Equal(1, model.DistinctDocumentsCount);
        Assert.Equal(3, model.Entries.Count);
        Assert.Contains(model.Entries, x => x.DocumentId == null);
        Assert.Contains(model.Entries, x => x.UserDisplayName == "Ivanov Ivan");
    }

    [Fact]
    public async Task Audit_Filters_By_ActivityType()
    {
        await using var dbContext = CreateDbContext();
        SeedAuditData(dbContext);
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Audit(AuditActivityTypes.UserLogin, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditAdminPageViewModel>(view.Model);
        Assert.Equal(AuditActivityTypes.UserLogin, model.SelectedActivityType);
        Assert.Single(model.Entries);
        Assert.All(model.Entries, x => Assert.Equal(AuditActivityTypes.UserLogin, x.ActivityType));
        Assert.Equal(1, model.SystemEventsCount);
    }

    [Fact]
    public async Task Audit_Filters_By_DocumentId()
    {
        await using var dbContext = CreateDbContext();
        SeedAuditData(dbContext);
        dbContext.DocumentActivity.Add(new DocumentActivity
        {
            DocumentId = 99,
            UserId = 7,
            ActivityType = AuditActivityTypes.DocumentCreated,
            ActivityDate = DateTime.UtcNow.AddMinutes(-5),
            Details = "Other document"
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Audit(null, 42, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditAdminPageViewModel>(view.Model);
        Assert.Equal(42, model.SelectedDocumentId);
        Assert.Equal(2, model.TotalCount);
        Assert.Equal(1, model.DistinctDocumentsCount);
        Assert.All(model.Entries, x => Assert.Equal(42, x.DocumentId));
    }

    [Fact]
    public async Task CreateNomenclatureCase_Persists_Case_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        var audit = new FakeAuditService();
        var controller = CreateController(dbContext, audit);

        var result = await controller.CreateNomenclatureCase(
            new CreateNomenclatureCaseInputModel
            {
                Index = "01-01",
                Title = "Contracts",
                RetentionPeriod = "5 years",
                LegalBasis = "Order 1",
                Department = "Legal"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Nomenclature), redirect.ActionName);
        var stored = Assert.Single(dbContext.NomenclatureCases);
        Assert.Equal("01-01", stored.Index);
        Assert.Equal("Contracts", stored.Title);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActivityTypes.NomenclatureCaseCreated, entry.ActivityType);
        Assert.Equal(77, entry.UserId);
    }

    [Fact]
    public async Task CreateNomenclatureCase_Returns_View_For_Duplicate_Index()
    {
        await using var dbContext = CreateDbContext();
        dbContext.NomenclatureCases.Add(new NomenclatureCase
        {
            Index = "01-01",
            Title = "Existing case",
            RetentionPeriod = "5 years",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.CreateNomenclatureCase(
            new CreateNomenclatureCaseInputModel
            {
                Index = "01-01",
                Title = "Duplicate",
                RetentionPeriod = "3 years"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Nomenclature", view.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, dbContext.NomenclatureCases.Count());
    }

    [Fact]
    public async Task CreateNomenclatureRule_Persists_Rule_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        var targetCase = new NomenclatureCase
        {
            NomenclatureCaseId = 5,
            Index = "02-01",
            Title = "Invoices",
            RetentionPeriod = "5 years",
            IsActive = true
        };
        dbContext.NomenclatureCases.Add(targetCase);
        await dbContext.SaveChangesAsync();
        var audit = new FakeAuditService();
        var controller = CreateController(dbContext, audit);

        var result = await controller.CreateNomenclatureRule(
            new CreateNomenclatureRuleInputModel
            {
                NomenclatureCaseId = targetCase.NomenclatureCaseId,
                DocumentType = "Invoice",
                Department = "Finance",
                Note = "Autobind invoices"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Nomenclature), redirect.ActionName);
        var rule = Assert.Single(dbContext.NomenclatureRules);
        Assert.Equal(targetCase.NomenclatureCaseId, rule.NomenclatureCaseId);
        Assert.Equal("Invoice", rule.DocumentType);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActivityTypes.NomenclatureRuleCreated, entry.ActivityType);
        Assert.Equal(77, entry.UserId);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static AdminController CreateController(ApplicationDbContext dbContext, FakeAuditService auditService)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreatePrincipal(("df_role", "Admin"), (ClaimTypes.NameIdentifier, "77"))
        };

        return new AdminController(dbContext, auditService, NullLogger<AdminController>.Instance)
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

    private static void SeedAuditData(ApplicationDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            UserId = 7,
            UserName = "ivanov",
            FirstName = "Ivan",
            LastName = "Ivanov",
            Email = "ivanov@test.local",
            PasswordHash = "hash",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        });
        dbContext.SaveChanges();

        dbContext.Documents.Add(new Document
        {
            DocumentId = 42,
            UserId = 7,
            Title = "Supply contract",
            DocumentType = "Contract",
            Status = "Draft",
            CreatedDate = DateTime.UtcNow
        });

        dbContext.DocumentActivity.AddRange(
            new DocumentActivity
            {
                ActivityId = 1,
                DocumentId = 42,
                UserId = 7,
                ActivityType = AuditActivityTypes.DocumentCreated,
                ActivityDate = DateTime.UtcNow.AddMinutes(-30),
                Details = "Document created"
            },
            new DocumentActivity
            {
                ActivityId = 2,
                DocumentId = 42,
                UserId = 7,
                ActivityType = AuditActivityTypes.StatusChanged,
                ActivityDate = DateTime.UtcNow.AddMinutes(-10),
                Details = "Status changed"
            },
            new DocumentActivity
            {
                ActivityId = 3,
                UserId = 7,
                ActivityType = AuditActivityTypes.UserLogin,
                ActivityDate = DateTime.UtcNow.AddMinutes(-2),
                Details = "User logged in"
            });

        dbContext.SaveChanges();
    }
}
