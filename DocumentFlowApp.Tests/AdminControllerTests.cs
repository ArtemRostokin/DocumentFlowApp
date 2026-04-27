using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Infrastructure.Services;
using DocumentFlowApp.Tests.TestDoubles;
using DocumentFlowApp.Web.Controllers;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentFlowApp.Tests;

public class AdminControllerTests
{
    [Fact]
    public async Task Index_Returns_Dashboard_With_Key_Metrics()
    {
        await using var dbContext = CreateDbContext();
        SeedAuditData(dbContext);
        dbContext.Users.Add(new User
        {
            UserId = 8,
            UserName = "inactive",
            Email = "inactive@test.local",
            PasswordHash = "hash",
            IsActive = false,
            CreatedDate = DateTime.UtcNow
        });
        dbContext.Documents.Add(new Document
        {
            DocumentId = 43,
            UserId = 7,
            Title = "Invoice",
            DocumentType = "Invoice",
            Status = "OnApproval",
            CreatedDate = DateTime.UtcNow
        });
        dbContext.Documents.Add(new Document
        {
            DocumentId = 44,
            UserId = 7,
            Title = "Execution",
            DocumentType = "Act",
            Status = "InWork",
            CreatedDate = DateTime.UtcNow
        });
        dbContext.NomenclatureCases.Add(new NomenclatureCase
        {
            NomenclatureCaseId = 10,
            Index = "01-01",
            Title = "Contracts",
            RetentionPeriod = "5 years",
            IsActive = true
        });
        dbContext.NomenclatureRules.Add(new NomenclatureRule
        {
            NomenclatureRuleId = 11,
            NomenclatureCaseId = 10,
            DocumentType = "Contract",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminDashboardPageViewModel>(view.Model);
        Assert.Equal(2, model.TotalUsers);
        Assert.Equal(1, model.ActiveUsers);
        Assert.Equal(3, model.TotalDocuments);
        Assert.Equal(1, model.PendingApprovalDocuments);
        Assert.Equal(1, model.InWorkDocuments);
        Assert.Equal(1, model.ActiveNomenclatureCases);
        Assert.Equal(1, model.ActiveNomenclatureRules);
        Assert.NotEmpty(model.RecentActivities);
    }

    [Fact]
    public async Task Routes_Returns_Current_Route_Overview()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Documents.AddRange(
            new Document
            {
                DocumentId = 1,
                UserId = 7,
                Title = "Approval",
                DocumentType = "Contract",
                Status = "OnApproval",
                CreatedDate = DateTime.UtcNow
            },
            new Document
            {
                DocumentId = 2,
                UserId = 7,
                Title = "Approved",
                DocumentType = "Invoice",
                Status = "Approved",
                CreatedDate = DateTime.UtcNow
            },
            new Document
            {
                DocumentId = 3,
                UserId = 7,
                Title = "Execution",
                DocumentType = "Act",
                Status = "InWork",
                CreatedDate = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Routes(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RoutesAdminPageViewModel>(view.Model);
        Assert.Equal(1, model.PendingApprovalDocuments);
        Assert.Equal(1, model.ApprovedDocuments);
        Assert.Equal(1, model.InWorkDocuments);
        Assert.Equal(6, model.Stages.Count);
        Assert.Contains(model.Roles, x => x.RoleName.Contains("Менеджер"));
    }

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

    [Fact]
    public async Task Users_Returns_List_Of_Users_And_Roles()
    {
        await using var dbContext = CreateDbContext();
        var adminRole = new Role { RoleId = 1, RoleName = "Admin" };
        var employeeRole = new Role { RoleId = 2, RoleName = "Employee" };
        dbContext.Roles.AddRange(adminRole, employeeRole);
        await dbContext.SaveChangesAsync();
        dbContext.Users.Add(new User
        {
            UserId = 15,
            UserName = "employee",
            Email = "employee@test.local",
            PasswordHash = "hash",
            FirstName = "Alex",
            LastName = "Worker",
            RoleId = employeeRole.RoleId,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var result = await controller.Users(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UsersAdminPageViewModel>(view.Model);
        Assert.Single(model.Users);
        Assert.Equal(2, model.Roles.Count);
        Assert.Equal("employee", model.Users[0].UserName);
    }

    [Fact]
    public async Task CreateUser_Persists_User_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Roles.Add(new Role { RoleId = 3, RoleName = "Manager" });
        await dbContext.SaveChangesAsync();
        var audit = new FakeAuditService();
        var controller = CreateController(dbContext, audit);

        var result = await controller.CreateUser(
            new CreateUserAdminInputModel
            {
                UserName = "new.manager",
                Email = "manager2@test.local",
                Password = "StrongPass1!",
                FirstName = "Irina",
                LastName = "Petrova",
                RoleId = 3,
                IsActive = true
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Users), redirect.ActionName);
        var user = Assert.Single(dbContext.Users.Where(x => x.UserName == "new.manager"));
        Assert.Equal("manager2@test.local", user.Email);
        Assert.True(user.IsActive);
        Assert.NotEqual("StrongPass1!", user.PasswordHash);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActivityTypes.UserCreated, entry.ActivityType);
    }

    [Fact]
    public async Task CreateUser_Allows_Login_With_Created_Credentials()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Roles.Add(new Role { RoleId = 30, RoleName = "Employee" });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, new FakeAuditService());

        var createResult = await controller.CreateUser(
            new CreateUserAdminInputModel
            {
                UserName = "new.employee",
                Email = "new.employee@test.local",
                Password = "Test12345!",
                FirstName = "New",
                LastName = "Employee",
                RoleId = 30,
                IsActive = true
            },
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(createResult);

        var authService = new AuthService(
            dbContext,
            Options.Create(new JwtOptions
            {
                Issuer = "Tests",
                Audience = "Tests",
                Key = "G9!pZ2@rX5*nQ8#mW1$kY4&bV7(cJ0)tU3^hS6",
                ExpiresMinutes = 60
            }));

        var authResult = await authService.LoginAsync("new.employee@test.local", "Test12345!", CancellationToken.None);

        Assert.True(authResult.IsSuccess);
        Assert.Null(authResult.ErrorMessage);
        Assert.Equal("new.employee", authResult.UserName);
    }

    [Fact]
    public async Task UpdateUser_Changes_Profile_Role_And_Activity_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        var employeeRole = new Role { RoleId = 4, RoleName = "Employee" };
        var managerRole = new Role { RoleId = 5, RoleName = "Manager" };
        dbContext.Roles.AddRange(employeeRole, managerRole);
        await dbContext.SaveChangesAsync();
        dbContext.Users.Add(new User
        {
            UserId = 21,
            UserName = "worker",
            Email = "worker@test.local",
            PasswordHash = "hash",
            FirstName = "Aleksey",
            LastName = "Worker",
            RoleId = employeeRole.RoleId,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var audit = new FakeAuditService();
        var controller = CreateController(dbContext, audit);

        var result = await controller.UpdateUser(
            new UpdateUserAdminInputModel
            {
                UserId = 21,
                UserName = "worker.updated",
                Email = "worker.updated@test.local",
                FirstName = "Alexey",
                LastName = "Updated",
                RoleId = 5,
                IsActive = false
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Users), redirect.ActionName);
        var user = Assert.Single(dbContext.Users.Where(x => x.UserId == 21));
        Assert.Equal("worker.updated", user.UserName);
        Assert.Equal("worker.updated@test.local", user.Email);
        Assert.Equal("Alexey", user.FirstName);
        Assert.Equal("Updated", user.LastName);
        Assert.Equal(5, user.RoleId);
        Assert.False(user.IsActive);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActivityTypes.UserUpdated, entry.ActivityType);
    }

    [Fact]
    public async Task ResetUserPassword_Updates_Hash_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            UserId = 31,
            UserName = "reset.me",
            Email = "reset@test.local",
            PasswordHash = "old-hash",
            FirstName = "Reset",
            LastName = "Target",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var audit = new FakeAuditService();
        var controller = CreateController(dbContext, audit);

        var result = await controller.ResetUserPassword(
            new ResetUserPasswordAdminInputModel
            {
                UserId = 31,
                NewPassword = "NewStrongPass1!"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Users), redirect.ActionName);

        var user = Assert.Single(dbContext.Users.Where(x => x.UserId == 31));
        Assert.NotEqual("old-hash", user.PasswordHash);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActivityTypes.UserPasswordReset, entry.ActivityType);
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
