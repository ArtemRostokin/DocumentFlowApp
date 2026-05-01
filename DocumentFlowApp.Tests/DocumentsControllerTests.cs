using System.Security.Claims;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Services;
using DocumentFlowApp.Tests.TestDoubles;
using DocumentFlowApp.Web.Controllers;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentFlowApp.Tests;

public class DocumentsControllerTests
{
    [Fact]
    public async Task ReviewDocument_Approve_Updates_Status_And_Writes_Audit()
    {
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 1,
                UserId = 10,
                Title = "Approval doc",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.ReviewDocument(
            1,
            new ApprovalActionInputModel { Decision = "approve" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.Approved.ToString(), repository.StoredDocument!.Status);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.ApprovalApproved && x.DocumentId == 1);
    }

    [Fact]
    public async Task ReviewDocument_Blocks_SelfApproval_For_MakerChecker_Type()
    {
        await using var dbContext = CreateDbContext();
        SeedDocumentCreatedActivity(dbContext, 12, 10);

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 12,
                UserId = 10,
                Title = "Self approval blocked",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")), dbContext);

        var result = await controller.ReviewDocument(
            12,
            new ApprovalActionInputModel { Decision = "approve" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Для критичных документов действует maker-checker: автор не может самостоятельно утвердить документ.", controller.TempData["ErrorMessage"]);
        Assert.DoesNotContain(audit.Entries, x => x.ActivityType == AuditActivityTypes.ApprovalApproved && x.DocumentId == 12);
    }

    [Fact]
    public async Task ReviewDocument_Allows_SelfApproval_For_NonCritical_Type()
    {
        await using var dbContext = CreateDbContext();
        SeedDocumentCreatedActivity(dbContext, 15, 10);

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 15,
                UserId = 10,
                Title = "Non critical self approval",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Report.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")), dbContext);

        var result = await controller.ReviewDocument(
            15,
            new ApprovalActionInputModel { Decision = "approve" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.Approved.ToString(), repository.StoredDocument!.Status);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.ApprovalApproved && x.DocumentId == 15);
    }

    [Fact]
    public async Task ReviewDocument_Rework_Without_Comment_Sets_Error_And_Does_Not_Change_Status()
    {
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 2,
                UserId = 10,
                Title = "Rework doc",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.ReviewDocument(
            2,
            new ApprovalActionInputModel { Decision = "rework", Comment = "" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Комментарий обязателен при возврате на доработку.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task StartWork_Transitions_To_InWork_And_Writes_Audit()
    {
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 3,
                UserId = 10,
                Title = "Execution doc",
                Status = DocumentStatus.Approved.ToString(),
                DocumentType = DocumentType.Act.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.StartWork(3, returnToEdit: false, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.MyTasks), redirect.ActionName);
        Assert.Equal(DocumentStatus.InWork.ToString(), repository.StoredDocument!.Status);
        Assert.NotNull(repository.StoredDocument.ExecutionStartedAt);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.WorkStarted && x.DocumentId == 3);
    }

    [Fact]
    public async Task CompleteWork_Requires_Execution_Data()
    {
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 4,
                UserId = 10,
                Title = "Need details",
                Status = DocumentStatus.InWork.ToString(),
                DocumentType = DocumentType.Report.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.CompleteWork(4, returnToEdit: true, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.InWork.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Перед завершением заполните комментарий исполнителя.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task ChangeStatus_Blocks_Employee_Outside_Own_Chain()
    {
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 5,
                UserId = 10,
                Title = "Restricted",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Other.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")));

        var result = await controller.ChangeStatus(
            5,
            new DocumentsController.ChangeDocumentStatusRequest { NewStatus = DocumentStatus.Approved.ToString() },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(DocumentStatus.Draft.ToString(), repository.StoredDocument!.Status);
    }

    [Fact]
    public async Task ChangeStatus_Allows_Manager_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        SeedExecutor(dbContext, 55, "approver-manager", "Manager");
        SeedRouteTemplate(dbContext, 20, "Other", 55, "Manager");

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 6,
                UserId = 10,
                Title = "Manager doc",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Other.ToString(),
                RouteTemplateId = 20,
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.ChangeStatus(
            6,
            new DocumentsController.ChangeDocumentStatusRequest { NewStatus = DocumentStatus.OnApproval.ToString() },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.StatusChanged && x.DocumentId == 6);
    }

    [Fact]
    public async Task ChangeStatus_Blocks_SelfApproval_For_MakerChecker_Type()
    {
        await using var dbContext = CreateDbContext();
        SeedDocumentCreatedActivity(dbContext, 13, 10);

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 13,
                UserId = 10,
                Title = "Change status blocked",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Order.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")), dbContext);

        var result = await controller.ChangeStatus(
            13,
            new DocumentsController.ChangeDocumentStatusRequest { NewStatus = DocumentStatus.Approved.ToString() },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
    }

    [Fact]
    public async Task AssignNomenclature_Updates_Document_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.NomenclatureCases.Add(new NomenclatureCase
        {
            NomenclatureCaseId = 15,
            Index = "01-01",
            Title = "Договоры",
            RetentionPeriod = "5 лет",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 7,
                UserId = 10,
                Title = "Nomenclature doc",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.AssignNomenclature(7, 15, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(15, repository.StoredDocument!.NomenclatureCaseId);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.NomenclatureAssigned && x.DocumentId == 7);
    }

    [Fact]
    public async Task AssignNomenclature_Returns_Error_For_Unknown_Case()
    {
        await using var dbContext = CreateDbContext();
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 8,
                UserId = 10,
                Title = "Nomenclature validation",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.AssignNomenclature(8, 404, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Null(repository.StoredDocument!.NomenclatureCaseId);
        Assert.Equal("Выбранное дело номенклатуры не найдено.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task AssignExecutor_Updates_Assigned_User_And_Writes_Audit()
    {
        await using var dbContext = CreateDbContext();
        SeedExecutor(dbContext, 55, "employee", "Employee");
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 9,
                UserId = 10,
                Title = "Assign executor",
                Status = DocumentStatus.Approved.ToString(),
                DocumentType = DocumentType.Order.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var audit = new FakeAuditService();
        var controller = CreateController(repository, audit, CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.AssignExecutor(9, 55, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(55, repository.StoredDocument!.UserId);
        Assert.Contains(audit.Entries, x => x.ActivityType == AuditActivityTypes.ExecutorAssigned && x.DocumentId == 9);
    }

    [Fact]
    public async Task AssignExecutor_Returns_Error_For_Invalid_Status()
    {
        await using var dbContext = CreateDbContext();
        SeedExecutor(dbContext, 55, "employee", "Employee");
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 10,
                UserId = 10,
                Title = "Bad status",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Order.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.AssignExecutor(10, 55, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(10, repository.StoredDocument!.UserId);
        Assert.Equal("Назначение исполнителя доступно только для утвержденных документов или документов в работе.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task NextStage_Returns_Error_When_Archive_Blocked_Without_Nomenclature()
    {
        await using var dbContext = CreateDbContext();
        var repository = CreateRepository(
            new Document
            {
                DocumentId = 11,
                UserId = 10,
                Title = "Archive guard",
                Status = DocumentStatus.Completed.ToString(),
                DocumentType = DocumentType.Act.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.NextStage(11, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.Completed.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Перед архивированием документ должен быть привязан к делу номенклатуры.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task NextStage_Blocks_SelfApproval_For_MakerChecker_Type()
    {
        await using var dbContext = CreateDbContext();
        SeedDocumentCreatedActivity(dbContext, 14, 99);

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 14,
                UserId = 10,
                Title = "Queue approval blocked",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Act.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.NextStage(14, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Для критичных документов действует maker-checker: автор не может самостоятельно утвердить документ.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task ApprovalAction_Blocks_Manager_SelfApproval_For_MakerChecker_Type()
    {
        await using var dbContext = CreateDbContext();
        SeedDocumentCreatedActivity(dbContext, 16, 99);

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 16,
                UserId = 10,
                Title = "Approval queue blocked",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Invoice.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.ApprovalAction(
            16,
            new ApprovalActionInputModel { Decision = "approve" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.ApprovalQueue), redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal("Для критичных документов действует maker-checker: автор не может самостоятельно утвердить документ.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task NextStage_FromDraft_Activates_PreparedApprovalRoute()
    {
        await using var dbContext = CreateDbContext();
        SeedExecutor(dbContext, 55, "approver", "Manager");
        SeedRouteTemplate(dbContext, 21, "Contract", 55, "Manager");

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 17,
                Title = "Route draft",
                Status = DocumentStatus.Draft.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                RouteTemplateId = 21,
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Manager"), (ClaimTypes.NameIdentifier, "99")), dbContext);

        var result = await controller.NextStage(17, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal(55, repository.StoredDocument.UserId);
        Assert.Equal(1, dbContext.DocumentApprovalSteps.Count(x => x.DocumentId == 17 && x.IsCurrent));
    }

    [Fact]
    public async Task ReviewDocument_Approve_Advances_To_NextRouteStep_When_Not_Last()
    {
        await using var dbContext = CreateDbContext();
        SeedExecutor(dbContext, 55, "approver2", "Manager");
        dbContext.DocumentApprovalSteps.AddRange(
            new DocumentApprovalStep
            {
                DocumentApprovalStepId = 1001,
                DocumentId = 18,
                StepOrder = 1,
                Title = "Первичное согласование",
                ApproverRole = "Manager",
                ApproverUserId = 10,
                Status = "Pending",
                IsCurrent = true
            },
            new DocumentApprovalStep
            {
                DocumentApprovalStepId = 1002,
                DocumentId = 18,
                StepOrder = 2,
                Title = "Финальное согласование",
                ApproverRole = "Manager",
                ApproverUserId = 55,
                Status = "Pending",
                IsCurrent = false
            });
        dbContext.SaveChanges();

        var repository = CreateRepository(
            new Document
            {
                DocumentId = 18,
                UserId = 10,
                Title = "Two-step approval",
                Status = DocumentStatus.OnApproval.ToString(),
                DocumentType = DocumentType.Contract.ToString(),
                CreatedDate = DateTime.UtcNow
            });
        var controller = CreateController(repository, new FakeAuditService(), CreatePrincipal(("df_role", "Employee"), (ClaimTypes.NameIdentifier, "10")), dbContext);

        var result = await controller.ReviewDocument(
            18,
            new ApprovalActionInputModel { Decision = "approve" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DocumentsController.Edit), redirect.ActionName);
        Assert.Equal(DocumentStatus.OnApproval.ToString(), repository.StoredDocument!.Status);
        Assert.Equal(55, repository.StoredDocument.UserId);
        Assert.True(dbContext.DocumentApprovalSteps.Single(x => x.DocumentApprovalStepId == 1002).IsCurrent);
    }

    private static DocumentsController CreateController(
        InMemoryDocumentRepository repository,
        FakeAuditService audit,
        ClaimsPrincipal user,
        ApplicationDbContext? dbContext = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user
        };

        return new DocumentsController(
            new DocumentService(repository),
            audit,
            new FakeAiClassifier(),
            new FakeOcrService(),
            new FakeTextExtractionService(),
            dbContext ?? CreateDbContext(),
            new FakeWebHostEnvironment(),
            NullLogger<DocumentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };
    }

    private static InMemoryDocumentRepository CreateRepository(Document document)
    {
        return new InMemoryDocumentRepository(document);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SeedExecutor(ApplicationDbContext dbContext, int userId, string userName, string roleName)
    {
        var role = new Role
        {
            RoleId = 100 + userId,
            RoleName = roleName
        };

        dbContext.Roles.Add(role);
        dbContext.SaveChanges();

        dbContext.Users.Add(new User
        {
            UserId = userId,
            UserName = userName,
            Email = $"{userName}@test.local",
            FirstName = "Test",
            LastName = "Employee",
            PasswordHash = "hash",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            RoleId = role.RoleId
        });
        dbContext.SaveChanges();
    }

    private static void SeedDocumentCreatedActivity(ApplicationDbContext dbContext, int documentId, int creatorUserId)
    {
        dbContext.DocumentActivity.Add(new DocumentActivity
        {
            DocumentId = documentId,
            UserId = creatorUserId,
            ActivityType = AuditActivityTypes.DocumentCreated,
            ActivityDate = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static void SeedRouteTemplate(ApplicationDbContext dbContext, int routeTemplateId, string documentType, int approverUserId, string approverRole)
    {
        dbContext.RouteTemplates.Add(new RouteTemplate
        {
            RouteTemplateId = routeTemplateId,
            Name = $"Route {routeTemplateId}",
            DocumentType = documentType,
            IsActive = true,
            IsDefault = true,
            CreatedDate = DateTime.UtcNow
        });
        dbContext.SaveChanges();

        dbContext.RouteSteps.Add(new RouteStep
        {
            RouteTemplateId = routeTemplateId,
            StepOrder = 1,
            Title = "Согласование",
            ApproverUserId = approverUserId,
            ApproverRole = approverRole,
            IsRequired = true
        });
        dbContext.SaveChanges();
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims.Select(x => new Claim(x.Type, x.Value)), "TestAuth"));
    }
}
