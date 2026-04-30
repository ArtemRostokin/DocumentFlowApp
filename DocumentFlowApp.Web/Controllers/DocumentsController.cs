using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Web.Models;
using DocumentFlowApp.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private const string UploadFolderName = "DocumentFlowAppUploads";
    private static readonly HashSet<DocumentType> MakerCheckerProtectedTypes =
    [
        DocumentType.Contract,
        DocumentType.Invoice,
        DocumentType.Order,
        DocumentType.Act
    ];

    private readonly IDocumentService _documentService;
    private readonly IAuditService _auditService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        IAuditService auditService,
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _auditService = auditService;
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> Create(int? templateId, CancellationToken cancellationToken)
    {
        var model = await BuildCreatePageModelAsync(new CreateDocumentPageViewModel
        {
            TemplateId = templateId
        }, cancellationToken);

        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public IActionResult UploadIncoming()
    {
        return View(new UploadIncomingDocumentsPageViewModel());
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> ApprovalQueue(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var model = await BuildApprovalQueueModelAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/approval")]
    public async Task<IActionResult> ApprovalAction(int id, ApprovalActionInputModel input, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var decision = (input.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (decision is not ("approve" or "rework"))
        {
            TempData["ErrorMessage"] = "Неизвестное действие согласования.";
            return RedirectToAction(nameof(ApprovalQueue));
        }

        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
        {
            TempData["ErrorMessage"] = "Документ не найден.";
            return RedirectToAction(nameof(ApprovalQueue));
        }

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != DocumentStatus.OnApproval)
        {
            TempData["ErrorMessage"] = "Документ уже не находится на согласовании.";
            return RedirectToAction(nameof(ApprovalQueue));
        }

        try
        {
            if (decision == "approve")
            {
                var makerCheckerMessage = await GetMakerCheckerViolationMessageAsync(
                    document,
                    currentStatus,
                    DocumentStatus.Approved,
                    currentUserId,
                    cancellationToken);
                if (makerCheckerMessage is not null)
                {
                    TempData["ErrorMessage"] = makerCheckerMessage;
                    return RedirectToAction(nameof(ApprovalQueue));
                }

                var approvalStatus = await AdvanceApprovalWorkflowAsync(document, currentUserId, cancellationToken);
                await LogDocumentActivityAsync(
                    id,
                    AuditActivityTypes.ApprovalApproved,
                    "Менеджер утвердил документ из очереди согласования.",
                    cancellationToken);
                TempData["SuccessMessage"] = $"Документ #{id} утвержден.";
            }
            else
            {
                await CaptureApprovalReworkAsync(document, currentUserId, input.Comment, cancellationToken);
                await CaptureApprovalReworkAsync(document, currentUserId, input.Comment, cancellationToken);
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Draft, input.Comment);
                await LogDocumentActivityAsync(
                    id,
                    AuditActivityTypes.ApprovalRework,
                    BuildReworkAuditDetails(input.Comment, "Менеджер вернул документ на доработку."),
                    cancellationToken);
                TempData["SuccessMessage"] = $"Документ #{id} возвращен на доработку.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось выполнить действие согласования для документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось выполнить действие согласования. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(ApprovalQueue));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    public async Task<IActionResult> MyTasks(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var model = await BuildMyTasksPageModelAsync(currentUserId.Value, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/start-work")]
    public async Task<IActionResult> StartWork(int id, bool returnToEdit, CancellationToken cancellationToken)
    {
        return await ExecuteEmployeeActionAsync(id, DocumentStatus.Approved, DocumentStatus.InWork, $"Документ #{id} переведен в работу.", returnToEdit, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/complete-work")]
    public async Task<IActionResult> CompleteWork(int id, bool returnToEdit, CancellationToken cancellationToken)
    {
        return await ExecuteEmployeeActionAsync(id, DocumentStatus.InWork, DocumentStatus.Completed, $"Документ #{id} отмечен как завершенный.", returnToEdit, cancellationToken);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/File/{fileName}")]
    public IActionResult File(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return NotFound();

        var safeFileName = Path.GetFileName(fileName);
        var physicalPath = Path.Combine(GetUploadsRootPath(), safeFileName);
        if (!System.IO.File.Exists(physicalPath))
            return NotFound();

        return PhysicalFile(physicalPath, GetContentTypeByExtension(safeFileName));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var doc = await _documentService.GetDocumentByIdAsync(id);
        if (doc is null)
            return NotFound();

        var currentUserId = GetCurrentUserId();
        if (!IsManagerOrAdmin() && doc.UserId != currentUserId)
            return RedirectAccessDenied("У вас нет доступа к этой карточке документа.");

        var model = new EditDocumentPageViewModel
        {
            Id = doc.DocumentId,
            Title = doc.Title ?? string.Empty,
            Description = doc.ExtractedText ?? string.Empty,
            Type = TryParseEnum<DocumentType>(doc.DocumentType) ?? DocumentType.Other,
            DueDate = doc.DueDate,
            Priority = doc.Priority,
            Tags = doc.Tags,
            Status = doc.Status ?? string.Empty,
            RouteTemplateId = doc.RouteTemplateId,
            RouteTemplateName = await GetRouteTemplateNameAsync(doc.RouteTemplateId, cancellationToken),
            RouteTemplateOptions = await LoadRouteTemplateOptionsAsync(cancellationToken, TryParseEnum<DocumentType>(doc.DocumentType)),
            ApprovalRouteSteps = await BuildApprovalRouteStepsAsync(doc, cancellationToken),
            RouteApproverOptions = await LoadRouteApproverOptionsAsync(cancellationToken),
            NomenclatureCaseId = doc.NomenclatureCaseId,
            NomenclatureCaseLabel = await BuildNomenclatureCaseLabelAsync(doc.NomenclatureCaseId, cancellationToken),
            NomenclatureCaseOptions = await LoadNomenclatureCaseOptionsAsync(cancellationToken),
            FileUrl = doc.FilePath,
            FileKind = GetFileKind(doc.FilePath),
            TemplateFields = await BuildTemplateFieldDisplayAsync(doc, cancellationToken),
            ExecutionComment = doc.ExecutionComment ?? string.Empty,
            ExecutionResult = doc.ExecutionResult,
            ExecutionStartedAt = doc.ExecutionStartedAt,
            ExecutionCompletedAt = doc.ExecutionCompletedAt,
            ExecutionFileUrl = doc.ExecutionFilePath,
            ExecutionFileName = doc.ExecutionFileName,
            ExecutionFileKind = GetFileKind(doc.ExecutionFilePath),
            AiSuggestions = BuildAiSuggestions(doc),
            Assignment = await BuildAssignmentPanelAsync(doc, cancellationToken)
        };
        model.TemplateName = model.TemplateFields.Count > 0
            ? await GetTemplateNameAsync(doc.TemplateId, cancellationToken)
            : null;
        ApplyExecutionHints(model);

        ApplyEditAccessState(model, doc);

        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/print-form")]
    public async Task<IActionResult> PrintForm(int id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var doc = await _documentService.GetDocumentByIdAsync(id);
        if (doc is null)
            return NotFound();

        var currentUserId = GetCurrentUserId();
        if (!IsManagerOrAdmin() && doc.UserId != currentUserId)
            return RedirectAccessDenied("У вас нет доступа к печатной форме этого документа.");

        var model = new EditDocumentPageViewModel
        {
            Id = doc.DocumentId,
            Title = doc.Title ?? string.Empty,
            Description = doc.ExtractedText ?? string.Empty,
            Type = TryParseEnum<DocumentType>(doc.DocumentType) ?? DocumentType.Other,
            DueDate = doc.DueDate,
            Priority = doc.Priority,
            Tags = doc.Tags,
            Status = doc.Status ?? string.Empty,
            NomenclatureCaseId = doc.NomenclatureCaseId,
            NomenclatureCaseLabel = await BuildNomenclatureCaseLabelAsync(doc.NomenclatureCaseId, cancellationToken),
            NomenclatureCaseOptions = await LoadNomenclatureCaseOptionsAsync(cancellationToken),
            FileUrl = doc.FilePath,
            FileKind = GetFileKind(doc.FilePath),
            TemplateFields = await BuildTemplateFieldDisplayAsync(doc, cancellationToken),
            ExecutionComment = doc.ExecutionComment ?? string.Empty,
            ExecutionResult = doc.ExecutionResult,
            ExecutionStartedAt = doc.ExecutionStartedAt,
            ExecutionCompletedAt = doc.ExecutionCompletedAt,
            ExecutionFileUrl = doc.ExecutionFilePath,
            ExecutionFileName = doc.ExecutionFileName,
            ExecutionFileKind = GetFileKind(doc.ExecutionFilePath),
            Assignment = await BuildAssignmentPanelAsync(doc, cancellationToken)
        };
        model.TemplateName = model.TemplateFields.Count > 0
            ? await GetTemplateNameAsync(doc.TemplateId, cancellationToken)
            : null;
        ApplyExecutionHints(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(EditDocumentPageViewModel model, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (!ModelState.IsValid)
        {
            await EnrichEditModelAsync(model);
            return View(model);
        }

        try
        {
            var doc = await _documentService.GetDocumentByIdAsync(model.Id);
            if (doc is null)
                return NotFound();

            doc.Title = model.Title;
            doc.ExtractedText = model.Description;
            doc.DocumentType = (model.Type ?? DocumentType.Other).ToString();
            doc.DueDate = model.DueDate;
            doc.Priority = model.Priority;
            doc.Tags = model.Tags;
            doc.RouteTemplateId = model.RouteTemplateId;
            doc.NomenclatureCaseId = model.NomenclatureCaseId;

            await _documentService.UpdateDocumentAsync(doc);
            await LogDocumentActivityAsync(
                doc.DocumentId,
                AuditActivityTypes.DocumentUpdated,
                $"Карточка обновлена. Тип: {GetDocumentTypeLabel(model.Type ?? DocumentType.Other)}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Изменения сохранены.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить изменения документа {DocumentId}", model.Id);
            ModelState.AddModelError(string.Empty, "Не удалось сохранить изменения. Повторите попытку позже.");
            await EnrichEditModelAsync(model);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/execution")]
    public async Task<IActionResult> SaveExecutionProgress(int id, EditDocumentPageViewModel model, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        if (document.UserId != currentUserId.Value)
            return RedirectAccessDenied("Документ уже назначен другому исполнителю.", nameof(MyTasks));

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != DocumentStatus.InWork)
        {
            TempData["ErrorMessage"] = "Сохранять ход исполнения можно только для документов в работе.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            document.ExecutionComment = (model.ExecutionComment ?? string.Empty).Trim();
            document.ExecutionResult = string.IsNullOrWhiteSpace(model.ExecutionResult)
                ? null
                : model.ExecutionResult.Trim();
            document.ExecutionStartedAt ??= DateTime.UtcNow;

            await _documentService.UpdateDocumentAsync(document);
            await LogDocumentActivityAsync(
                document.DocumentId,
                AuditActivityTypes.ExecutionSaved,
                BuildExecutionAuditDetails(document),
                cancellationToken);
            TempData["SuccessMessage"] = "Ход исполнения сохранен.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить ход исполнения документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось сохранить ход исполнения. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/execution-print-file")]
    public async Task<IActionResult> GenerateExecutionPrintFile(int id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        if (document.UserId != currentUserId.Value)
            return RedirectAccessDenied("Документ уже назначен другому исполнителю.", nameof(MyTasks));

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != DocumentStatus.InWork)
        {
            TempData["ErrorMessage"] = "Печатную форму результата можно сформировать только для документа в работе.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            var generated = await SaveGeneratedExecutionPrintFileAsync(document, cancellationToken);
            document.ExecutionFilePath = generated.FilePath;
            document.ExecutionFileName = generated.FileName;
            document.ExecutionStartedAt ??= DateTime.UtcNow;

            await _documentService.UpdateDocumentAsync(document);
            await LogDocumentActivityAsync(
                document.DocumentId,
                AuditActivityTypes.ExecutionFileGenerated,
                $"Сформирован итоговый файл исполнения: {document.ExecutionFileName}.",
                cancellationToken);
            TempData["SuccessMessage"] = "Печатная форма сохранена как итоговый файл исполнения.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сформировать итоговый файл исполнения для документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось сформировать печатную форму. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200 * 1024 * 1024)]
    [Route("Documents/{id:int}/execution-file")]
    public async Task<IActionResult> UploadExecutionFile(int id, IFormFile? executionFile, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        if (document.UserId != currentUserId.Value)
            return RedirectAccessDenied("Документ уже назначен другому исполнителю.", nameof(MyTasks));

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != DocumentStatus.InWork)
        {
            TempData["ErrorMessage"] = "Итоговый файл можно загружать только для документов в работе.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (executionFile is null || executionFile.Length <= 0)
        {
            TempData["ErrorMessage"] = "Выберите итоговый файл.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (executionFile.Length > 25 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Итоговый файл не должен превышать 25 MB.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (!IsAllowedExecutionAttachment(executionFile))
        {
            TempData["ErrorMessage"] = "Поддерживаются PDF, DOCX, XLSX, PNG и JPEG.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            _logger.LogInformation("Загрузка итогового файла для документа {DocumentId}: {FileName}, {Length} bytes", id, executionFile.FileName, executionFile.Length);
            var stored = await SaveUploadedDocumentFileAsync(executionFile, cancellationToken);
            document.ExecutionFilePath = stored.FilePath;
            document.ExecutionFileName = Path.GetFileName(executionFile.FileName);
            document.ExecutionStartedAt ??= DateTime.UtcNow;

            await _documentService.UpdateDocumentAsync(document);
            await LogDocumentActivityAsync(
                document.DocumentId,
                AuditActivityTypes.ExecutionFileUploaded,
                $"Загружен итоговый файл исполнения: {document.ExecutionFileName}.",
                cancellationToken);
            _logger.LogInformation("Итоговый файл сохранен для документа {DocumentId}: {FilePath}", id, document.ExecutionFilePath);
            TempData["SuccessMessage"] = "Итоговый файл загружен.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить итоговый файл для документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось загрузить итоговый файл. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/nomenclature")]
    public async Task<IActionResult> AssignNomenclature(int id, int? nomenclatureCaseId, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetDocumentByIdAsync(id);
        if (doc is null)
            return NotFound();

        if (nomenclatureCaseId is not null)
        {
            var exists = await _dbContext.NomenclatureCases
                .AsNoTracking()
                .AnyAsync(x => x.NomenclatureCaseId == nomenclatureCaseId.Value && x.IsActive, cancellationToken);

            if (!exists)
            {
                TempData["ErrorMessage"] = "Выбранное дело номенклатуры не найдено.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        try
        {
            doc.NomenclatureCaseId = nomenclatureCaseId;
            await _documentService.UpdateDocumentAsync(doc);
            await LogDocumentActivityAsync(
                doc.DocumentId,
                AuditActivityTypes.NomenclatureAssigned,
                await BuildNomenclatureAuditDetailsAsync(nomenclatureCaseId, cancellationToken),
                cancellationToken);
            TempData["SuccessMessage"] = nomenclatureCaseId is null
                ? "Привязка к номенклатуре снята."
                : "Номенклатура документа обновлена.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить номенклатуру для документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось сохранить номенклатуру документа.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/assign")]
    public async Task<IActionResult> AssignExecutor(int id, int assignedUserId, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetDocumentByIdAsync(id);
        if (doc is null)
            return NotFound();

        var currentStatus = ParseDocumentStatus(doc.Status);
        if (currentStatus is not (DocumentStatus.Approved or DocumentStatus.InWork))
        {
            TempData["ErrorMessage"] = "Назначение исполнителя доступно только для утвержденных документов или документов в работе.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var executors = await LoadExecutorOptionsAsync(cancellationToken);
        var executor = executors.FirstOrDefault(x => x.UserId == assignedUserId);
        if (executor is null)
        {
            TempData["ErrorMessage"] = "Исполнитель не найден.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            doc.UserId = assignedUserId;
            await _documentService.UpdateDocumentAsync(doc);
            await LogDocumentActivityAsync(
                doc.DocumentId,
                AuditActivityTypes.ExecutorAssigned,
                $"Назначен исполнитель: {executor.DisplayName}.",
                cancellationToken);
            TempData["SuccessMessage"] = $"Исполнитель назначен: {executor.DisplayName}.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось назначить исполнителя {ExecutorId} для документа {DocumentId}", assignedUserId, id);
            TempData["ErrorMessage"] = "Не удалось назначить исполнителя. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/approval-route")]
    public async Task<IActionResult> PrepareApprovalRoute(int id, int? routeTemplateId, CancellationToken cancellationToken)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        var status = ParseDocumentStatus(document.Status);
        if (status != DocumentStatus.Draft)
        {
            TempData["ErrorMessage"] = "Маршрут согласования можно настраивать только для черновика.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        document.RouteTemplateId = routeTemplateId;
        await _documentService.UpdateDocumentAsync(document);

        var prepared = await EnsureApprovalRoutePreparedAsync(document, regenerate: true, cancellationToken);
        if (!prepared)
        {
            TempData["ErrorMessage"] = "Не удалось подготовить маршрут: выберите шаблон хотя бы с одним шагом согласования.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["SuccessMessage"] = "Маршрут согласования подготовлен.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/approval-route/step")]
    public async Task<IActionResult> UpdateApprovalRouteStep(int id, int documentApprovalStepId, int? approverUserId, CancellationToken cancellationToken)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        if (ParseDocumentStatus(document.Status) != DocumentStatus.Draft)
        {
            TempData["ErrorMessage"] = "Шаги маршрута можно менять только до отправки на согласование.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var step = await _dbContext.DocumentApprovalSteps
            .FirstOrDefaultAsync(x => x.DocumentApprovalStepId == documentApprovalStepId && x.DocumentId == id, cancellationToken);
        if (step is null)
        {
            TempData["ErrorMessage"] = "Шаг маршрута не найден.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        User? approver = null;
        if (approverUserId.HasValue)
        {
            approver = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == approverUserId.Value && x.IsActive, cancellationToken);
            if (approver is null)
            {
                TempData["ErrorMessage"] = "Выберите активного согласующего.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var normalizedStepSpecialization = ApprovalSpecializations.Normalize(step.ApproverSpecialization);
            var approverSpecialization = ApprovalSpecializations.Normalize(approver.ApprovalSpecialization);
            if (normalizedStepSpecialization is not null &&
                !string.Equals(normalizedStepSpecialization, approverSpecialization, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Выбранный пользователь не соответствует бизнес-роли шага.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        step.ApproverUserId = approver?.UserId;
        if (approver is not null)
            step.ApproverRole = approver.Role?.RoleName ?? step.ApproverRole;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = approver is null
            ? "Для шага включено автоматическое назначение по бизнес-роли."
            : "Согласующий шага обновлен.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/next-stage")]
    public async Task<IActionResult> NextStage(int id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var doc = await _documentService.GetDocumentByIdAsync(id);
        if (doc is null)
            return NotFound();

        var current = TryParseEnum<DocumentStatus>(doc.Status) ?? DocumentStatus.Draft;
        var next = GetNextStatus(current);

        try
        {
            var currentUserId = GetCurrentUserId();
            var makerCheckerMessage = await GetMakerCheckerViolationMessageAsync(
                doc,
                current,
                next,
                currentUserId,
                cancellationToken);
            if (makerCheckerMessage is not null)
            {
                TempData["ErrorMessage"] = makerCheckerMessage;
                return RedirectToAction(nameof(Edit), new { id });
            }

            if (current == DocumentStatus.Draft && next == DocumentStatus.OnApproval)
            {
                var prepared = await EnsureApprovalRoutePreparedAsync(doc, regenerate: false, cancellationToken);
                if (!prepared)
                {
                    TempData["ErrorMessage"] = "РџРµСЂРµРґ РѕС‚РїСЂР°РІРєРѕР№ РЅР° СЃРѕРіР»Р°СЃРѕРІР°РЅРёРµ РЅСѓР¶РЅРѕ РІС‹Р±СЂР°С‚СЊ С€Р°Р±Р»РѕРЅ РјР°СЂС€СЂСѓС‚Р° Рё РїРѕРґРіРѕС‚РѕРІРёС‚СЊ С…РѕС‚СЏ Р±С‹ РѕРґРёРЅ С€Р°Рі СЃРѕРіР»Р°СЃРѕРІР°РЅРёСЏ.";
                    return RedirectToAction(nameof(Edit), new { id });
                }
            }

            if (current == DocumentStatus.OnApproval && next == DocumentStatus.Approved)
            {
                next = await AdvanceApprovalWorkflowAsync(doc, currentUserId, cancellationToken);
            }
            else
            {
                await _documentService.ChangeDocumentStatusAsync(id, next);
                if (current == DocumentStatus.Draft && next == DocumentStatus.OnApproval)
                    await ActivatePreparedApprovalRouteAsync(doc, cancellationToken);
            }

            await LogDocumentActivityAsync(
                id,
                AuditActivityTypes.StatusChanged,
                $"Менеджер перевел документ из статуса {GetDocumentStatusLabel(current)} в статус {GetDocumentStatusLabel(next)}.",
                cancellationToken);
            TempData["SuccessMessage"] = $"Статус изменен: {GetDocumentStatusLabel(current)} -> {GetDocumentStatusLabel(next)}";
            return RedirectToAction("Index", "Home");
        }
        catch (InvalidOperationException ex)
        {
            TempData["SuccessMessage"] = null;
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200 * 1024 * 1024)]
    public async Task<IActionResult> UploadIncoming(UploadIncomingDocumentsPageViewModel model, CancellationToken cancellationToken)
    {
        var files = model.Files
            .Where(f => f is { Length: > 0 })
            .ToList();

        if (files.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Files), "Выберите хотя бы один файл.");
            return View(model);
        }

        if (files.Count > 20)
        {
            ModelState.AddModelError(nameof(model.Files), "За одну загрузку можно обработать не больше 20 файлов.");
            return View(model);
        }

        var createdCount = 0;

        try
        {
            foreach (var file in files)
            {
                if (file.Length > 25 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.Files), $"Файл {file.FileName} превышает 25 MB.");
                    return View(model);
                }

                if (!IsAllowedIncomingFile(file))
                {
                    ModelState.AddModelError(nameof(model.Files), $"Файл {file.FileName} имеет неподдерживаемый формат.");
                    return View(model);
                }

                var stored = await SaveUploadedDocumentFileAsync(file, cancellationToken);
                var classifiedType = ClassifyIncomingDocument(file.FileName);
                var title = Path.GetFileNameWithoutExtension(file.FileName);

                var created = await _documentService.CreateDocumentAsync(new CreateDocumentRequest
                {
                    Title = string.IsNullOrWhiteSpace(title) ? $"Входящий документ {DateTime.UtcNow:yyyyMMdd-HHmmss}" : title,
                    Description = $"Загружено из внешнего источника. Предварительная AI-классификация: {GetDocumentTypeLabel(classifiedType)}.",
                    Type = classifiedType,
                    Priority = 2,
                    Tags = "incoming,batch,ai-classified",
                    FilePath = stored.FilePath,
                    FileSize = stored.FileSize,
                    FileHash = stored.FileHash
                });

                await LogDocumentActivityAsync(
                    created.DocumentId,
                    AuditActivityTypes.IncomingUploaded,
                    $"Загружен входящий файл {Path.GetFileName(file.FileName)}.",
                    cancellationToken);
                await TryAutoAssignRouteTemplateAsync(created, cancellationToken);
                await TryAutoAssignNomenclatureAsync(created, cancellationToken);

                createdCount++;
            }

            TempData["SuccessMessage"] = $"Загружено входящих документов: {createdCount}.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить входящие документы.");
            ModelState.AddModelError(string.Empty, "Не удалось загрузить документы. Повторите попытку позже.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateDocumentPageViewModel model, CancellationToken cancellationToken)
    {
        var selectedTemplate = await GetTemplateViewModelAsync(model.TemplateId, cancellationToken);
        if (selectedTemplate is null)
            ModelState.AddModelError(nameof(model.TemplateId), "Выберите шаблон документа.");

        if (selectedTemplate is not null)
        {
            model.Type = ParseTemplateType(selectedTemplate.Category);

            foreach (var field in selectedTemplate.Fields.Where(f => f.Required))
            {
                if (!model.TemplateFieldValues.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value))
                    ModelState.AddModelError($"TemplateFieldValues[{field.Key}]", $"Заполните поле \"{field.Label}\".");
            }
        }

        ApplyQuickCreateDefaults(model);

        if (!ModelState.IsValid)
        {
            await PopulateTemplateStateAsync(model, cancellationToken);
            return View(model);
        }

        string? filePath = null;
        long? fileSize = null;
        string? fileHash = null;

        try
        {
            if (model.File is { Length: > 0 })
            {
                if (model.File.Length > 25 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.File), "Размер файла не должен превышать 25 MB.");
                    await PopulateTemplateStateAsync(model, cancellationToken);
                    return View(model);
                }

                if (!IsAllowedPdf(model.File))
                {
                    ModelState.AddModelError(nameof(model.File), "Поддерживается только PDF-файл.");
                    await PopulateTemplateStateAsync(model, cancellationToken);
                    return View(model);
                }

                var stored = await SaveUploadedDocumentFileAsync(model.File, cancellationToken);
                filePath = stored.FilePath;
                fileSize = stored.FileSize;
                fileHash = stored.FileHash;
            }

            var created = await _documentService.CreateDocumentAsync(new CreateDocumentRequest
            {
                Title = model.Title,
                Description = BuildTemplateAwareDescription(model.Description, selectedTemplate, model.TemplateFieldValues),
                Type = model.Type ?? DocumentType.Other,
                RouteTemplateId = model.RouteTemplateId,
                DueDate = model.DueDate,
                Priority = model.Priority,
                Tags = BuildTemplateAwareTags(model.Tags, model.TemplateId),
                TemplateId = model.TemplateId,
                FilePath = filePath,
                FileSize = fileSize,
                FileHash = fileHash
            });

            await LogDocumentActivityAsync(
                created.DocumentId,
                AuditActivityTypes.DocumentCreated,
                $"Документ создан по шаблону{(selectedTemplate is null ? string.Empty : $": {selectedTemplate.Name}")}.",
                cancellationToken);
            await TryAutoAssignRouteTemplateAsync(created, cancellationToken);
            await TryAutoAssignNomenclatureAsync(created, cancellationToken);

            TempData["SuccessMessage"] = "Документ успешно создан.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать документ.");
            ModelState.AddModelError(string.Empty, "Не удалось создать документ. Повторите попытку позже.");
            await PopulateTemplateStateAsync(model, cancellationToken);
            return View(model);
        }
    }

    public sealed class ChangeDocumentStatusRequest
    {
        public string? NewStatus { get; init; }
        public string? Comment { get; init; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/review")]
    public async Task<IActionResult> ReviewDocument(int id, ApprovalActionInputModel input, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document is null)
            return NotFound();

        if (document.UserId != currentUserId && !IsManagerOrAdmin())
            return RedirectAccessDenied("Документ уже передан другому участнику согласования.", nameof(ApprovalQueue));

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != DocumentStatus.OnApproval)
        {
            TempData["ErrorMessage"] = "Действие доступно только для документов на согласовании.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var decision = (input.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (decision is not ("approve" or "rework"))
        {
            TempData["ErrorMessage"] = "Неизвестное действие согласования.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (decision == "rework" && string.IsNullOrWhiteSpace(input.Comment))
        {
            TempData["ErrorMessage"] = "Комментарий обязателен при возврате на доработку.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            if (decision == "approve")
            {
                var makerCheckerMessage = await GetMakerCheckerViolationMessageAsync(
                    document,
                    currentStatus,
                    DocumentStatus.Approved,
                    currentUserId,
                    cancellationToken);
                if (makerCheckerMessage is not null)
                {
                    TempData["ErrorMessage"] = makerCheckerMessage;
                    return RedirectToAction(nameof(Edit), new { id });
                }

                var approvalStatus = await AdvanceApprovalWorkflowAsync(document, currentUserId, cancellationToken);
                await LogDocumentActivityAsync(
                    id,
                    AuditActivityTypes.ApprovalApproved,
                    "Пользователь утвердил документ на этапе согласования.",
                    cancellationToken);
                TempData["SuccessMessage"] = $"Документ #{id} утвержден.";
            }
            else
            {
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Draft, input.Comment);
                await LogDocumentActivityAsync(
                    id,
                    AuditActivityTypes.ApprovalRework,
                    BuildReworkAuditDetails(input.Comment, "Пользователь вернул документ на доработку."),
                    cancellationToken);
                TempData["SuccessMessage"] = $"Документ #{id} возвращен на доработку.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось выполнить пользовательское согласование для документа {DocumentId}", id);
            TempData["ErrorMessage"] = "Не удалось выполнить действие согласования. Повторите попытку позже.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.EmployeeOrHigher)]
    [Route("Documents/{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeDocumentStatusRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (request is null || string.IsNullOrWhiteSpace(request.NewStatus))
            return BadRequest(new { message = "Не указан новый статус." });

        if (!Enum.TryParse<DocumentStatus>(request.NewStatus, true, out var newStatus))
            return BadRequest(new { message = "Некорректное значение статуса." });

        try
        {
            var currentUserId = GetCurrentUserId();
            var isManagerOrAdmin = IsManagerOrAdmin();
            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document is null)
                return NotFound(new { message = "Документ не найден." });

            if (!isManagerOrAdmin)
            {
                if (currentUserId is null || document.UserId != currentUserId.Value)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Документ уже передан другому участнику процесса." });

                var currentStatus = ParseDocumentStatus(document.Status);
                var isAllowedEmployeeTransition =
                    (currentStatus == DocumentStatus.OnApproval && newStatus == DocumentStatus.Approved) ||
                    (currentStatus == DocumentStatus.Approved && newStatus == DocumentStatus.InWork) ||
                    (currentStatus == DocumentStatus.InWork && newStatus == DocumentStatus.Completed);

                if (!isAllowedEmployeeTransition)
                    return BadRequest(new { message = "Пользователь может менять статус только по своей цепочке: На согласовании -> Утвержден -> В работе -> Завершен." });
            }

            var previousStatus = ParseDocumentStatus(document.Status);
            var makerCheckerMessage = await GetMakerCheckerViolationMessageAsync(
                document,
                previousStatus,
                newStatus,
                currentUserId,
                cancellationToken);
            if (makerCheckerMessage is not null)
                return BadRequest(new { message = makerCheckerMessage });

            if (previousStatus == DocumentStatus.Draft && newStatus == DocumentStatus.OnApproval)
            {
                var prepared = await EnsureApprovalRoutePreparedAsync(document, regenerate: false, cancellationToken);
                if (!prepared)
                    return BadRequest(new { message = "Перед отправкой на согласование выберите шаблон маршрута и подготовьте хотя бы один шаг." });
            }

            if (previousStatus == DocumentStatus.OnApproval && newStatus == DocumentStatus.Approved)
            {
                newStatus = await AdvanceApprovalWorkflowAsync(document, currentUserId, cancellationToken);
            }
            else
            {
                await _documentService.ChangeDocumentStatusAsync(id, newStatus, request.Comment);
                if (previousStatus == DocumentStatus.Draft && newStatus == DocumentStatus.OnApproval)
                    await ActivatePreparedApprovalRouteAsync(document, cancellationToken);
            }

            await LogDocumentActivityAsync(
                id,
                AuditActivityTypes.StatusChanged,
                BuildStatusChangeAuditDetails(previousStatus, newStatus, request.Comment, isManagerOrAdmin),
                cancellationToken);
            return Ok(new { id, status = newStatus.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось изменить статус документа {DocumentId} на {NewStatus}", id, newStatus);
            return StatusCode(500, new { message = "Не удалось изменить статус. Повторите попытку позже." });
        }
    }

    private async Task<ApprovalQueuePageViewModel> BuildApprovalQueueModelAsync()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = AuthorizationPolicies.IsInAppRole(User, DocumentFlowApp.Core.Security.AppRoles.Admin);
            var currentUserSpecialization = await GetCurrentUserApprovalSpecializationAsync();
            var documents = await _documentService.GetAllDocumentsAsync();
            var currentSteps = await _dbContext.DocumentApprovalSteps
                .AsNoTracking()
                .Where(x => x.IsCurrent)
                .ToDictionaryAsync(x => x.DocumentId, x => x, cancellationToken: CancellationToken.None);

            var items = documents
                .Where(d => ParseDocumentStatus(d.Status) == DocumentStatus.OnApproval)
                .Where(d =>
                {
                    if (isAdmin)
                        return true;

                    if (currentUserId is null)
                        return false;

                    if (d.UserId == currentUserId.Value)
                        return true;

                    if (!currentSteps.TryGetValue(d.DocumentId, out var step))
                        return false;

                    if (step.ApproverUserId == currentUserId.Value)
                        return true;

                    if (step.ApproverUserId is not null)
                        return false;

                    var stepSpecialization = ApprovalSpecializations.Normalize(step.ApproverSpecialization);
                    return stepSpecialization is not null &&
                           string.Equals(stepSpecialization, currentUserSpecialization, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(d => d.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(d => d.CreatedDate)
                .Select(ToApprovalQueueItem)
                .ToList();

            return new ApprovalQueuePageViewModel
            {
                PendingCount = items.Count,
                SuccessMessage = TempData["SuccessMessage"] as string,
                ErrorMessage = TempData["ErrorMessage"] as string,
                Documents = items
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить очередь согласования.");
            return new ApprovalQueuePageViewModel
            {
                PendingCount = 0,
                ErrorMessage = "Не удалось загрузить очередь согласования. Повторите попытку позже.",
                Documents = []
            };
        }
    }

    private async Task<CreateDocumentPageViewModel> BuildCreatePageModelAsync(CreateDocumentPageViewModel model, CancellationToken cancellationToken)
    {
        await PopulateTemplateStateAsync(model, cancellationToken);
        model.RouteTemplateOptions = await LoadRouteTemplateOptionsAsync(cancellationToken, model.Type);
        return model;
    }

    private async Task PopulateTemplateStateAsync(CreateDocumentPageViewModel model, CancellationToken cancellationToken)
    {
        var templates = await LoadTemplateCatalogAsync(cancellationToken);
        model.Templates = templates;

        if (model.TemplateId is null)
        {
            model.SelectedTemplateFields = [];
            model.SelectedTemplateName = null;
            model.SelectedTemplateDescription = null;
            model.SelectedTemplateTypeLabel = null;
            return;
        }

        var selectedTemplate = templates.FirstOrDefault(t => t.Id == model.TemplateId.Value);
        if (selectedTemplate is null)
        {
            model.SelectedTemplateFields = [];
            model.SelectedTemplateName = null;
            model.SelectedTemplateDescription = null;
            model.SelectedTemplateTypeLabel = null;
            return;
        }

        model.SelectedTemplateName = selectedTemplate.Name;
        model.SelectedTemplateDescription = selectedTemplate.Description;
        model.SelectedTemplateTypeLabel = selectedTemplate.TypeLabel;
        model.SelectedTemplateFields = selectedTemplate.Fields;

        foreach (var field in selectedTemplate.Fields)
            model.TemplateFieldValues.TryAdd(field.Key, string.Empty);
    }

    private async Task<IReadOnlyList<DocumentTemplateViewModel>> LoadTemplateCatalogAsync(CancellationToken cancellationToken)
    {
        var templates = await _dbContext.Templates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(MapTemplate).ToList();
    }

    private async Task<DocumentTemplateViewModel?> GetTemplateViewModelAsync(int? templateId, CancellationToken cancellationToken)
    {
        if (templateId is null)
            return null;

        var template = await _dbContext.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateId == templateId.Value, cancellationToken);

        return template is null ? null : MapTemplate(template);
    }

    private async Task<IReadOnlyList<RouteTemplateOptionViewModel>> LoadRouteTemplateOptionsAsync(CancellationToken cancellationToken, DocumentType? documentType = null)
    {
        var requestedType = documentType?.ToString();
        var templates = await _dbContext.RouteTemplates
            .AsNoTracking()
            .Include(x => x.Steps)
            .ThenInclude(x => x.ApproverUser)
            .Where(x => x.IsActive)
            .Where(x => x.DocumentType == null || requestedType == null || x.DocumentType == requestedType)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(x => new RouteTemplateOptionViewModel
        {
            Id = x.RouteTemplateId,
            Name = x.Name,
            DocumentType = x.DocumentType,
            ApproverSummary = x.Steps.Count == 0
                ? "Шаги не настроены"
                : string.Join(" -> ", x.Steps.OrderBy(s => s.StepOrder).Select(s =>
                    string.IsNullOrWhiteSpace(s.ApproverUser?.UserName)
                        ? $"{s.Title}: {ApprovalSpecializations.GetLabel(s.ApproverSpecialization)}"
                        : $"{s.Title}: {s.ApproverUser.UserName} ({ApprovalSpecializations.GetLabel(s.ApproverSpecialization)})"))
        }).ToList();
    }

    private async Task<IReadOnlyList<RouteApproverOptionViewModel>> LoadRouteApproverOptionsAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.IsActive)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.UserName)
            .Select(x => new
            {
                x.UserId,
                x.UserName,
                x.FirstName,
                x.LastName,
                x.ApprovalSpecialization,
                RoleName = x.Role != null ? x.Role.RoleName : "User"
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(x => new RouteApproverOptionViewModel
            {
                UserId = x.UserId,
                DisplayName = string.Join(" ", new[] { x.LastName, x.FirstName }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim() == string.Empty
                    ? x.UserName
                    : string.Join(" ", new[] { x.LastName, x.FirstName }.Where(v => !string.IsNullOrWhiteSpace(v))),
                RoleName = x.RoleName,
                ApprovalSpecialization = x.ApprovalSpecialization,
                ApprovalSpecializationLabel = ApprovalSpecializations.GetLabel(x.ApprovalSpecialization)
            })
            .ToList();
    }

    private async Task<string?> GetRouteTemplateNameAsync(int? routeTemplateId, CancellationToken cancellationToken)
    {
        if (routeTemplateId is null)
            return null;

        return await _dbContext.RouteTemplates
            .AsNoTracking()
            .Where(x => x.RouteTemplateId == routeTemplateId.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ApprovalRouteStepViewModel>> BuildApprovalRouteStepsAsync(Document document, CancellationToken cancellationToken)
    {
        var steps = await _dbContext.DocumentApprovalSteps
            .AsNoTracking()
            .Include(x => x.ApproverUser)
            .OrderBy(x => x.StepOrder)
            .Where(x => x.DocumentId == document.DocumentId)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0 && document.RouteTemplateId is not null)
        {
            var templateSteps = await _dbContext.RouteSteps
                .AsNoTracking()
                .Include(x => x.ApproverUser)
                .Where(x => x.RouteTemplateId == document.RouteTemplateId.Value)
                .OrderBy(x => x.StepOrder)
                .ToListAsync(cancellationToken);

            return templateSteps.Select(step => new ApprovalRouteStepViewModel
            {
                RouteStepId = step.RouteStepId,
                Order = step.StepOrder,
                Title = step.Title,
                ApproverRole = step.ApproverRole,
                ApproverSpecialization = step.ApproverSpecialization,
                ApproverSpecializationLabel = ApprovalSpecializations.GetLabel(step.ApproverSpecialization),
                ApproverUserId = step.ApproverUserId,
                ApproverDisplayName = FormatDisplayName(step.ApproverUser),
                Status = "Template",
                IsCurrent = false
            }).ToList();
        }

        return steps.Select(step => new ApprovalRouteStepViewModel
        {
            DocumentApprovalStepId = step.DocumentApprovalStepId,
            RouteStepId = step.RouteStepId,
            Order = step.StepOrder,
            Title = step.Title,
            ApproverRole = step.ApproverRole,
            ApproverSpecialization = step.ApproverSpecialization,
            ApproverSpecializationLabel = ApprovalSpecializations.GetLabel(step.ApproverSpecialization),
            ApproverUserId = step.ApproverUserId,
            ApproverDisplayName = FormatDisplayName(step.ApproverUser),
            Status = step.Status,
            IsCurrent = step.IsCurrent,
            Comment = step.Comment
        }).ToList();
    }

    private async Task TryAutoAssignRouteTemplateAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.RouteTemplateId is not null)
            return;

        var matchedTemplateId = await _dbContext.RouteTemplates
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => x.DocumentType == document.DocumentType && x.IsDefault)
            .OrderBy(x => x.RouteTemplateId)
            .Select(x => (int?)x.RouteTemplateId)
            .FirstOrDefaultAsync(cancellationToken);

        matchedTemplateId ??= await _dbContext.RouteTemplates
            .AsNoTracking()
            .Where(x => x.IsActive && x.DocumentType == null && x.IsDefault)
            .OrderBy(x => x.RouteTemplateId)
            .Select(x => (int?)x.RouteTemplateId)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchedTemplateId is null)
            return;

        document.RouteTemplateId = matchedTemplateId.Value;
        await _documentService.UpdateDocumentAsync(document);
    }

    private async Task<MyTasksPageViewModel> BuildMyTasksPageModelAsync(int currentUserId, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = await _dbContext.Documents
                .AsNoTracking()
                .Where(d => d.UserId == currentUserId)
                .OrderBy(d => d.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(d => d.CreatedDate)
                .ToListAsync(cancellationToken);

            var activeTasks = tasks
                .Where(d => ParseDocumentStatus(d.Status) is DocumentStatus.Approved or DocumentStatus.InWork)
                .ToList();

            var employee = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);
            var employeeName = employee is null
                ? "Исполнитель"
                : string.IsNullOrWhiteSpace($"{employee.FirstName} {employee.LastName}".Trim())
                    ? employee.UserName
                    : $"{employee.FirstName} {employee.LastName}".Trim();

            return new MyTasksPageViewModel
            {
                EmployeeName = employeeName,
                TotalTasks = activeTasks.Count,
                ApprovedTasks = activeTasks.Count(d => ParseDocumentStatus(d.Status) == DocumentStatus.Approved),
                InWorkTasks = activeTasks.Count(d => ParseDocumentStatus(d.Status) == DocumentStatus.InWork),
                SuccessMessage = TempData["SuccessMessage"] as string,
                ErrorMessage = TempData["ErrorMessage"] as string,
                Tasks = activeTasks.Select(ToMyTaskCard).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить задачи исполнителя {UserId}", currentUserId);
            return new MyTasksPageViewModel
            {
                EmployeeName = "Исполнитель",
                ErrorMessage = "Не удалось загрузить список задач. Повторите попытку позже.",
                Tasks = []
            };
        }
    }

    private static MyTaskCardViewModel ToMyTaskCard(Document document)
    {
        var status = ParseDocumentStatus(document.Status);
        var type = TryParseEnum<DocumentType>(document.DocumentType) ?? DocumentType.Other;

        return new MyTaskCardViewModel
        {
            Id = document.DocumentId,
            Title = string.IsNullOrWhiteSpace(document.Title) ? "Без названия" : document.Title,
            Description = string.IsNullOrWhiteSpace(document.ExtractedText) ? "Описание не заполнено." : document.ExtractedText,
            TypeLabel = GetDocumentTypeLabel(type),
            StatusLabel = GetDocumentStatusLabel(status),
            DueDateLabel = document.DueDate.HasValue ? document.DueDate.Value.ToLocalTime().ToString("dd.MM.yyyy") : "Срок не указан",
            CanStartWork = status == DocumentStatus.Approved,
            CanComplete = status == DocumentStatus.InWork
        };
    }

    private async Task<AssignmentPanelViewModel> BuildAssignmentPanelAsync(Document document, CancellationToken cancellationToken)
    {
        var status = ParseDocumentStatus(document.Status);
        var executors = await LoadExecutorOptionsAsync(cancellationToken);
        var assignedExecutor = executors.FirstOrDefault(x => x.UserId == document.UserId);

        return new AssignmentPanelViewModel
        {
            CanAssign = status is DocumentStatus.Approved or DocumentStatus.InWork,
            AssignedUserId = document.UserId,
            AssignedUserName = assignedExecutor?.DisplayName,
            Executors = executors
        };
    }

    private async Task<IReadOnlyList<NomenclatureCaseOptionViewModel>> LoadNomenclatureCaseOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.NomenclatureCases
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Index)
            .Select(x => new NomenclatureCaseOptionViewModel
            {
                Id = x.NomenclatureCaseId,
                Label = $"{x.Index} - {x.Title}"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> BuildNomenclatureCaseLabelAsync(int? nomenclatureCaseId, CancellationToken cancellationToken)
    {
        if (nomenclatureCaseId is null)
            return null;

        var item = await _dbContext.NomenclatureCases
            .AsNoTracking()
            .Where(x => x.NomenclatureCaseId == nomenclatureCaseId.Value)
            .Select(x => new { x.Index, x.Title })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? null : $"{item.Index} - {item.Title}";
    }

    private async Task TryAutoAssignNomenclatureAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.NomenclatureCaseId is not null)
            return;

        var documentType = document.DocumentType;
        if (string.IsNullOrWhiteSpace(documentType))
            return;

        var matchedCaseId = await _dbContext.NomenclatureRules
            .AsNoTracking()
            .Where(r => r.IsActive && r.NomenclatureCase != null && r.NomenclatureCase.IsActive)
            .Where(r => r.DocumentType == documentType)
            .OrderBy(r => r.NomenclatureRuleId)
            .Select(r => (int?)r.NomenclatureCaseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchedCaseId is null)
            return;

        document.NomenclatureCaseId = matchedCaseId.Value;
        await _documentService.UpdateDocumentAsync(document);
        await LogDocumentActivityAsync(
            document.DocumentId,
            AuditActivityTypes.NomenclatureAssigned,
            await BuildNomenclatureAuditDetailsAsync(matchedCaseId, cancellationToken, true),
            cancellationToken);
    }

    private Task LogDocumentActivityAsync(
        int documentId,
        string activityType,
        string details,
        CancellationToken cancellationToken)
    {
        return _auditService.LogDocumentActivityAsync(
            documentId,
            GetCurrentUserId(),
            activityType,
            details,
            cancellationToken);
    }

    private async Task<string?> GetMakerCheckerViolationMessageAsync(
        Document document,
        DocumentStatus currentStatus,
        DocumentStatus targetStatus,
        int? actingUserId,
        CancellationToken cancellationToken)
    {
        if (currentStatus != DocumentStatus.OnApproval || targetStatus != DocumentStatus.Approved)
            return null;

        if (actingUserId is null || !RequiresMakerChecker(document))
            return null;

        var creatorUserId = await GetDocumentCreatorUserIdAsync(document.DocumentId, cancellationToken);
        if (creatorUserId != actingUserId)
            return null;

        return "Для критичных документов действует maker-checker: автор не может самостоятельно утвердить документ.";
    }

    private bool RequiresMakerChecker(Document document)
    {
        var documentType = TryParseEnum<DocumentType>(document.DocumentType);
        return documentType.HasValue && MakerCheckerProtectedTypes.Contains(documentType.Value);
    }

    private async Task<int?> GetDocumentCreatorUserIdAsync(int documentId, CancellationToken cancellationToken)
    {
        return await _dbContext.DocumentActivity
            .AsNoTracking()
            .Where(activity =>
                activity.DocumentId == documentId &&
                activity.ActivityType == AuditActivityTypes.DocumentCreated &&
                activity.UserId.HasValue)
            .OrderBy(activity => activity.ActivityDate ?? DateTime.MinValue)
            .ThenBy(activity => activity.ActivityId)
            .Select(activity => activity.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> EnsureApprovalRoutePreparedAsync(Document document, bool regenerate, CancellationToken cancellationToken)
    {
        if (document.RouteTemplateId is null)
            return false;

        var templateSteps = await _dbContext.RouteSteps
            .AsNoTracking()
            .Where(x => x.RouteTemplateId == document.RouteTemplateId.Value)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

        if (templateSteps.Count == 0)
            return false;

        var existingSteps = await _dbContext.DocumentApprovalSteps
            .Where(x => x.DocumentId == document.DocumentId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

        if (existingSteps.Count > 0 && !regenerate)
            return existingSteps.All(x => x.ApproverUserId is not null || !string.IsNullOrWhiteSpace(x.ApproverSpecialization));

        if (existingSteps.Count > 0)
            _dbContext.DocumentApprovalSteps.RemoveRange(existingSteps);

        foreach (var templateStep in templateSteps)
        {
            var resolvedApproverUserId = await ResolveApprovalAssigneeUserIdAsync(
                templateStep.ApproverSpecialization,
                templateStep.ApproverUserId,
                cancellationToken);

            _dbContext.DocumentApprovalSteps.Add(new DocumentApprovalStep
            {
                DocumentId = document.DocumentId,
                RouteTemplateId = document.RouteTemplateId,
                RouteStepId = templateStep.RouteStepId,
                StepOrder = templateStep.StepOrder,
                Title = templateStep.Title,
                ApproverRole = templateStep.ApproverRole,
                ApproverSpecialization = templateStep.ApproverSpecialization,
                ApproverUserId = resolvedApproverUserId,
                Status = "Pending",
                IsCurrent = false
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return templateSteps.All(x => x.ApproverUserId is not null || !string.IsNullOrWhiteSpace(x.ApproverSpecialization));
    }

    private async Task ActivatePreparedApprovalRouteAsync(Document document, CancellationToken cancellationToken)
    {
        var steps = await _dbContext.DocumentApprovalSteps
            .Where(x => x.DocumentId == document.DocumentId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

        foreach (var step in steps)
        {
            step.Status = "Pending";
            step.IsCurrent = false;
            step.Comment = null;
            step.ActionDate = null;
            step.ActionByUserId = null;
        }

        var firstStep = steps.FirstOrDefault();
        if (firstStep is not null)
        {
            firstStep.ApproverUserId = await ResolveApprovalAssigneeUserIdAsync(firstStep.ApproverSpecialization, firstStep.ApproverUserId, cancellationToken);
            firstStep.IsCurrent = true;
            document.UserId = firstStep.ApproverUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _documentService.UpdateDocumentAsync(document);
    }

    private async Task<DocumentStatus> AdvanceApprovalWorkflowAsync(Document document, int? actingUserId, CancellationToken cancellationToken)
    {
        var steps = await _dbContext.DocumentApprovalSteps
            .Where(x => x.DocumentId == document.DocumentId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            await _documentService.ChangeDocumentStatusAsync(document.DocumentId, DocumentStatus.Approved);
            document.UserId = null;
            await _documentService.UpdateDocumentAsync(document);
            return DocumentStatus.Approved;
        }

        var currentStep = steps.FirstOrDefault(x => x.IsCurrent) ?? steps.FirstOrDefault(x => x.Status == "Pending");
        if (currentStep is null)
            throw new InvalidOperationException("Не найден текущий шаг согласования.");

        var currentUserSpecialization = await GetCurrentUserApprovalSpecializationAsync();
        if (currentStep.ApproverUserId is not null &&
            actingUserId != currentStep.ApproverUserId &&
            !AuthorizationPolicies.IsInAppRole(User, DocumentFlowApp.Core.Security.AppRoles.Admin))
        {
            throw new InvalidOperationException("Текущий шаг маршрута назначен другому согласующему.");
        }

        if (currentStep.ApproverUserId is null &&
            !string.IsNullOrWhiteSpace(currentStep.ApproverSpecialization) &&
            !AuthorizationPolicies.IsInAppRole(User, DocumentFlowApp.Core.Security.AppRoles.Admin) &&
            !string.Equals(ApprovalSpecializations.Normalize(currentStep.ApproverSpecialization), currentUserSpecialization, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Текущий шаг маршрута предназначен для другого профиля согласования.");
        }

        currentStep.Status = "Approved";
        currentStep.IsCurrent = false;
        currentStep.ActionDate = DateTime.UtcNow;
        currentStep.ActionByUserId = actingUserId;

        var nextStep = steps
            .Where(x => x.DocumentApprovalStepId != currentStep.DocumentApprovalStepId && x.Status == "Pending")
            .OrderBy(x => x.StepOrder)
            .FirstOrDefault();

        if (nextStep is not null)
        {
            nextStep.ApproverUserId = await ResolveApprovalAssigneeUserIdAsync(nextStep.ApproverSpecialization, nextStep.ApproverUserId, cancellationToken);
            nextStep.IsCurrent = true;
            document.UserId = nextStep.ApproverUserId;
            document.Status = DocumentStatus.OnApproval.ToString();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _documentService.UpdateDocumentAsync(document);
            return DocumentStatus.OnApproval;
        }

        document.UserId = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _documentService.ChangeDocumentStatusAsync(document.DocumentId, DocumentStatus.Approved);
        return DocumentStatus.Approved;
    }

    private async Task CaptureApprovalReworkAsync(Document document, int? actingUserId, string? comment, CancellationToken cancellationToken)
    {
        var currentStep = await _dbContext.DocumentApprovalSteps
            .Where(x => x.DocumentId == document.DocumentId && x.IsCurrent)
            .OrderBy(x => x.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentStep is null)
            return;

        currentStep.Status = "Reworked";
        currentStep.IsCurrent = false;
        currentStep.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        currentStep.ActionDate = DateTime.UtcNow;
        currentStep.ActionByUserId = actingUserId;
        document.UserId = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _documentService.UpdateDocumentAsync(document);
    }

    private async Task<string?> GetCurrentUserApprovalSpecializationAsync()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return null;

        var specialization = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId.Value && x.IsActive)
            .Select(x => x.ApprovalSpecialization)
            .FirstOrDefaultAsync();

        return ApprovalSpecializations.Normalize(specialization);
    }

    private async Task<int?> ResolveApprovalAssigneeUserIdAsync(
        string? approvalSpecialization,
        int? preferredUserId,
        CancellationToken cancellationToken)
    {
        var normalizedSpecialization = ApprovalSpecializations.Normalize(approvalSpecialization);
        if (preferredUserId.HasValue)
        {
            var preferredUser = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == preferredUserId.Value && x.IsActive)
                .Select(x => new { x.UserId, x.ApprovalSpecialization })
                .FirstOrDefaultAsync(cancellationToken);

            if (preferredUser is not null)
            {
                if (normalizedSpecialization is null)
                    return preferredUser.UserId;

                var preferredSpecialization = ApprovalSpecializations.Normalize(preferredUser.ApprovalSpecialization);
                if (string.Equals(preferredSpecialization, normalizedSpecialization, StringComparison.OrdinalIgnoreCase))
                    return preferredUser.UserId;
            }
        }

        if (normalizedSpecialization is null)
            return preferredUserId;

        return await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => x.ApprovalSpecialization != null)
            .Where(x => x.ApprovalSpecialization!.ToLower() == normalizedSpecialization.ToLower())
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.UserName)
            .Select(x => (int?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string FormatDisplayName(User? user)
    {
        if (user is null)
            return "Не назначен";

        var fullName = string.Join(" ", new[] { user.LastName, user.FirstName }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
    }

    private static DocumentType? ParseDocumentType(string? raw)
    {
        return TryParseEnum<DocumentType>(raw);
    }

    private async Task<string> BuildNomenclatureAuditDetailsAsync(
        int? nomenclatureCaseId,
        CancellationToken cancellationToken,
        bool autoAssigned = false)
    {
        if (nomenclatureCaseId is null)
            return "Привязка к делу номенклатуры снята.";

        var targetCase = await _dbContext.NomenclatureCases
            .AsNoTracking()
            .Where(x => x.NomenclatureCaseId == nomenclatureCaseId.Value)
            .Select(x => $"{x.Index} - {x.Title}")
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(targetCase))
            return autoAssigned
                ? "Документ автоматически привязан к делу номенклатуры."
                : "Номенклатура документа обновлена.";

        return autoAssigned
            ? $"Документ автоматически привязан к делу номенклатуры: {targetCase}."
            : $"Номенклатура документа обновлена: {targetCase}.";
    }

    private static string BuildReworkAuditDetails(string? comment, string prefix)
    {
        var normalizedComment = string.IsNullOrWhiteSpace(comment)
            ? "Комментарий не указан."
            : comment.Trim();

        return $"{prefix} Комментарий: {normalizedComment}";
    }

    private static string BuildExecutionAuditDetails(Document document)
    {
        var result = string.IsNullOrWhiteSpace(document.ExecutionResult)
            ? "не указан"
            : document.ExecutionResult.Trim();

        var comment = string.IsNullOrWhiteSpace(document.ExecutionComment)
            ? "без комментария"
            : document.ExecutionComment.Trim();

        return $"Исполнитель сохранил ход работы. Результат: {result}. Комментарий: {comment}";
    }

    private string BuildStatusChangeAuditDetails(
        DocumentStatus fromStatus,
        DocumentStatus newStatus,
        string? comment,
        bool isManagerOrAdmin)
    {
        var actor = isManagerOrAdmin ? "Менеджер или администратор" : "Исполнитель";
        var details = $"{actor} перевел документ из статуса {GetDocumentStatusLabel(fromStatus)} в статус {GetDocumentStatusLabel(newStatus)}.";

        if (!string.IsNullOrWhiteSpace(comment))
            details += $" Комментарий: {comment.Trim()}";

        return details;
    }

    private static string BuildWorkCompletedAuditDetails(Document document)
    {
        var result = string.IsNullOrWhiteSpace(document.ExecutionResult)
            ? "не указан"
            : document.ExecutionResult.Trim();

        return $"Исполнитель завершил работу по документу. Результат: {result}.";
    }

    private async Task<string?> GetTemplateNameAsync(int? templateId, CancellationToken cancellationToken)
    {
        if (templateId is null)
            return null;

        return await _dbContext.Templates
            .AsNoTracking()
            .Where(t => t.TemplateId == templateId.Value)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DocumentTemplateFieldDisplayViewModel>> BuildTemplateFieldDisplayAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        if (document.TemplateId is null)
            return [];

        var template = await _dbContext.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateId == document.TemplateId.Value, cancellationToken);

        if (template is null)
            return [];

        var mappedTemplate = MapTemplate(template);
        return mappedTemplate.Fields
            .Select(field => new DocumentTemplateFieldDisplayViewModel
            {
                Label = field.Label,
                Value = GetTemplateFieldValue(document.ExtractedText, field.Label, "Не заполнено"),
                Required = field.Required
            })
            .ToList();
    }

    private static void ApplyExecutionHints(EditDocumentPageViewModel model)
    {
        var type = model.Type ?? DocumentType.Other;
        model.ExecutionHintTitle = type switch
        {
            DocumentType.Contract => "Что проверить по договору",
            DocumentType.Invoice => "Что проверить по счету",
            DocumentType.Application => "Что проверить по заявлению",
            DocumentType.Order => "Что проверить по приказу",
            DocumentType.Act => "Что проверить по акту",
            _ => "Что проверить при исполнении"
        };

        model.ExecutionHintItems = type switch
        {
            DocumentType.Contract =>
            [
                "Проверьте контрагента, предмет договора и сумму.",
                "Убедитесь, что условия договора соответствуют задаче.",
                "Зафиксируйте итог: согласовано, нужны правки или требуются материалы."
            ],
            DocumentType.Invoice =>
            [
                "Проверьте поставщика, сумму и срок оплаты.",
                "Сверьте счет с основанием для оплаты.",
                "При необходимости приложите платежное поручение или подтверждение."
            ],
            DocumentType.Application =>
            [
                "Проверьте заявителя, подразделение и тему обращения.",
                "Подготовьте решение или комментарий по заявлению.",
                "При необходимости приложите ответ или подтверждающий файл."
            ],
            DocumentType.Order =>
            [
                "Проверьте основание приказа и ответственных лиц.",
                "Убедитесь, что поручения сформулированы однозначно.",
                "Зафиксируйте результат ознакомления или исполнения."
            ],
            DocumentType.Act =>
            [
                "Проверьте основание акта и описанную выполненную работу.",
                "Убедитесь, что результат можно подтвердить документально.",
                "При необходимости приложите подписанный акт или скан."
            ],
            _ =>
            [
                "Ознакомьтесь с данными документа.",
                "Выполните поручение по документу.",
                "Заполните комментарий и результат исполнения."
            ]
        };
    }

    private async Task<IReadOnlyList<ExecutorOptionViewModel>> LoadExecutorOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);

        return users
            .Where(u => string.Equals(DocumentFlowApp.Core.Security.AppRoles.Normalize(u.Role?.RoleName), DocumentFlowApp.Core.Security.AppRoles.Employee, StringComparison.OrdinalIgnoreCase))
            .Select(u => new ExecutorOptionViewModel
            {
                UserId = u.UserId,
                DisplayName = string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim()) ? u.UserName : $"{u.FirstName} {u.LastName}".Trim(),
                Email = u.Email
            })
            .ToList();
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return int.TryParse(raw, out var userId) ? userId : null;
    }

    private IActionResult RedirectAccessDenied(string message, string action = "Index", string controller = "Home", object? routeValues = null)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToAction(action, controller, routeValues);
    }

    private bool IsManagerOrAdmin()
    {
        return AuthorizationPolicies.IsInAppRole(User, DocumentFlowApp.Core.Security.AppRoles.Admin) ||
               AuthorizationPolicies.IsInAppRole(User, DocumentFlowApp.Core.Security.AppRoles.Manager);
    }

    private void ApplyEditAccessState(EditDocumentPageViewModel model, Document document)
    {
        var currentUserId = GetCurrentUserId();
        var status = ParseDocumentStatus(document.Status);
        var isManagerOrAdmin = IsManagerOrAdmin();
        var isAssignedToCurrentUser = currentUserId.HasValue && document.UserId == currentUserId.Value;

        model.CanEditDocument = isManagerOrAdmin;
        model.CanAdvanceWorkflow = isManagerOrAdmin;
        model.CanApprove = isAssignedToCurrentUser && status == DocumentStatus.OnApproval;
        model.CanRework = isAssignedToCurrentUser && status == DocumentStatus.OnApproval;
        model.CanStartWork = isAssignedToCurrentUser && status == DocumentStatus.Approved;
        model.CanComplete = isAssignedToCurrentUser && status == DocumentStatus.InWork;
        model.CanSaveExecutionProgress = isAssignedToCurrentUser && status == DocumentStatus.InWork;
        model.CanConfigureApprovalRoute = isManagerOrAdmin && status == DocumentStatus.Draft;
    }

    private async Task<IActionResult> ExecuteEmployeeActionAsync(
        int documentId,
        DocumentStatus expectedStatus,
        DocumentStatus newStatus,
        string successMessage,
        bool returnToEdit,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return RedirectToAction("Login", "Account");

        var document = await _documentService.GetDocumentByIdAsync(documentId);
        if (document is null)
            return NotFound();

        if (document.UserId != currentUserId && !IsManagerOrAdmin())
            return RedirectAccessDenied("Документ уже передан другому участнику процесса.", returnToEdit ? nameof(Edit) : nameof(MyTasks), routeValues: returnToEdit ? new { id = documentId } : null);

        var currentStatus = ParseDocumentStatus(document.Status);
        if (currentStatus != expectedStatus)
        {
            TempData["ErrorMessage"] = $"Операция недоступна для статуса {GetDocumentStatusLabel(currentStatus)}.";
            return returnToEdit
                ? RedirectToAction(nameof(Edit), new { id = documentId })
                : RedirectToAction(nameof(MyTasks));
        }

        if (newStatus == DocumentStatus.Completed)
        {
            if (string.IsNullOrWhiteSpace(document.ExecutionComment))
            {
                TempData["ErrorMessage"] = "Перед завершением заполните комментарий исполнителя.";
                return returnToEdit
                    ? RedirectToAction(nameof(Edit), new { id = documentId })
                    : RedirectToAction(nameof(MyTasks));
            }

            if (string.IsNullOrWhiteSpace(document.ExecutionResult))
            {
                TempData["ErrorMessage"] = "Перед завершением выберите результат исполнения.";
                return returnToEdit
                    ? RedirectToAction(nameof(Edit), new { id = documentId })
                    : RedirectToAction(nameof(MyTasks));
            }
        }

        try
        {
            await _documentService.ChangeDocumentStatusAsync(documentId, newStatus);

            var updatedDocument = await _documentService.GetDocumentByIdAsync(documentId);
            if (updatedDocument is not null)
            {
                if (newStatus == DocumentStatus.InWork)
                    updatedDocument.ExecutionStartedAt ??= DateTime.UtcNow;

                if (newStatus == DocumentStatus.Completed)
                    updatedDocument.ExecutionCompletedAt ??= DateTime.UtcNow;

                await _documentService.UpdateDocumentAsync(updatedDocument);
            }

            await LogDocumentActivityAsync(
                documentId,
                newStatus == DocumentStatus.InWork ? AuditActivityTypes.WorkStarted : AuditActivityTypes.WorkCompleted,
                newStatus == DocumentStatus.InWork
                    ? "Исполнитель начал работу по документу."
                    : BuildWorkCompletedAuditDetails(updatedDocument ?? document),
                cancellationToken);
            TempData["SuccessMessage"] = successMessage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось выполнить действие {NewStatus} для документа {DocumentId}", newStatus, documentId);
            TempData["ErrorMessage"] = "Не удалось изменить статус документа. Повторите попытку позже.";
        }

        return returnToEdit
            ? RedirectToAction(nameof(Edit), new { id = documentId })
            : RedirectToAction(nameof(MyTasks));
    }
    private static DocumentTemplateViewModel MapTemplate(Template template)
    {
        var type = ParseTemplateType(template.Category);

        return new DocumentTemplateViewModel
        {
            Id = template.TemplateId,
            Name = template.Name,
            Description = string.IsNullOrWhiteSpace(template.Content) ? "Шаблон документа" : template.Content,
            Category = template.Category ?? string.Empty,
            TypeLabel = GetDocumentTypeLabel(type),
            Fields = ParseTemplateFields(template.AiSuggestedFields)
        };
    }

    private static IReadOnlyList<DocumentTemplateFieldViewModel> ParseTemplateFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var fields = JsonSerializer.Deserialize<List<DocumentTemplateFieldViewModel>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return fields?
                .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Label))
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static DocumentType ParseTemplateType(string? category)
    {
        return TryParseEnum<DocumentType>(category) ?? DocumentType.Other;
    }

    private static string BuildTemplateAwareDescription(
        string? description,
        DocumentTemplateViewModel? template,
        IReadOnlyDictionary<string, string> fieldValues)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(description))
            builder.AppendLine(description.Trim());

        if (template is not null)
        {
            var filledFields = template.Fields
                .Select(field => new
                {
                    field.Label,
                    Value = fieldValues.TryGetValue(field.Key, out var value) ? value?.Trim() : null
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList();

            if (filledFields.Count > 0)
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.AppendLine($"Шаблон: {template.Name}");
                builder.AppendLine("Поля шаблона:");
                foreach (var field in filledFields)
                    builder.AppendLine($"- {field.Label}: {field.Value}");
            }
        }

        return builder.ToString().Trim();
    }

    private static string? BuildTemplateAwareTags(string? tags, int? templateId)
    {
        var result = string.IsNullOrWhiteSpace(tags) ? string.Empty : tags.Trim();
        if (templateId is null)
            return string.IsNullOrWhiteSpace(result) ? null : result;

        var templateTag = $"template-{templateId.Value}";
        if (result.Contains(templateTag, StringComparison.OrdinalIgnoreCase))
            return result;

        return string.IsNullOrWhiteSpace(result) ? templateTag : $"{result},{templateTag}";
    }
    private static ApprovalQueueItemViewModel ToApprovalQueueItem(Document document)
    {
        var type = TryParseEnum<DocumentType>(document.DocumentType) ?? DocumentType.Other;
        var status = ParseDocumentStatus(document.Status);

        return new ApprovalQueueItemViewModel
        {
            Id = document.DocumentId,
            Title = string.IsNullOrWhiteSpace(document.Title) ? "Без названия" : document.Title,
            TypeLabel = GetDocumentTypeLabel(type),
            StatusLabel = GetDocumentStatusLabel(status),
            Description = string.IsNullOrWhiteSpace(document.ExtractedText) ? "Описание не заполнено." : document.ExtractedText,
            CreatedAtLabel = document.CreatedDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
            DueDateLabel = document.DueDate.HasValue ? document.DueDate.Value.ToLocalTime().ToString("dd.MM.yyyy") : "Не указан",
            AuthorLabel = GetAuthorLabel(document)
        };
    }

    private static string GetAuthorLabel(Document document)
    {
        if (document.User is null)
            return "Система";

        var fullName = $"{document.User.FirstName} {document.User.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        return string.IsNullOrWhiteSpace(document.User.UserName) ? "Система" : document.User.UserName;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private async Task<(string FilePath, long FileSize, string FileHash)> SaveUploadedDocumentFileAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var uploadsRoot = GetUploadsRootPath();
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        // Сначала полностью закрываем поток записи, и только потом читаем файл для хеша.
        await using (var fileStream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }

        return ($"/Documents/File/{fileName}", file.Length, await ComputeSha256Async(physicalPath, cancellationToken));
    }

    private async Task<(string FilePath, string FileName)> SaveGeneratedExecutionPrintFileAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var uploadsRoot = GetUploadsRootPath();
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"execution-print-{document.DocumentId}-{DateTime.UtcNow:yyyyMMddHHmmss}.html";
        var physicalPath = Path.Combine(uploadsRoot, fileName);
        var html = await BuildGeneratedExecutionPrintHtmlAsync(document, cancellationToken);

        await System.IO.File.WriteAllTextAsync(physicalPath, html, Encoding.UTF8, cancellationToken);
        return ($"/Documents/File/{fileName}", $"Печатная форма документа #{document.DocumentId}.html");
    }

    private async Task<string> BuildGeneratedExecutionPrintHtmlAsync(Document document, CancellationToken cancellationToken)
    {
        var type = TryParseEnum<DocumentType>(document.DocumentType) ?? DocumentType.Other;
        var status = ParseDocumentStatus(document.Status);
        var executor = document.UserId.HasValue
            ? await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == document.UserId.Value, cancellationToken)
            : null;
        var executorName = executor is null
            ? "Не назначен"
            : string.IsNullOrWhiteSpace($"{executor.FirstName} {executor.LastName}".Trim())
                ? executor.UserName
                : $"{executor.FirstName} {executor.LastName}".Trim();

        string Enc(string? value) => System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "Не указано" : value);
        string Field(string label, string fallback = "Не указано") => Enc(GetTemplateFieldValue(document.ExtractedText, label, fallback));
        string DateOnly(DateTime? value) => value.HasValue ? value.Value.ToLocalTime().ToString("dd.MM.yyyy") : "Не указано";
        string DateTimeLabel(DateTime? value) => value.HasValue ? value.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "Не указано";

        var title = Enc(document.Title);
        var description = Enc(document.ExtractedText);
        var statusLabel = Enc(GetDocumentStatusLabel(status));
        var typeLabel = Enc(GetDocumentTypeLabel(type));
        var generatedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        var body = type switch
        {
            DocumentType.Contract => $"""
                <div class="doc-title">Договор № {Field("Номер договора", document.DocumentId.ToString())}</div>
                <div class="doc-subtitle">{Field("Предмет договора", document.Title ?? "Предмет договора")}</div>
                <div class="row"><span>г. Санкт-Петербург</span><span>{Field("Дата договора", DateOnly(document.DueDate))}</span></div>
                <p>ООО «Документооборот», именуемое в дальнейшем «Сторона 1», и {Field("Контрагент", "Контрагент не указан")}, именуемое в дальнейшем «Сторона 2», заключили настоящий договор.</p>
                <h2>1. Предмет договора</h2>
                <p>{Field("Предмет договора", document.Title ?? "Не указано")}</p>
                <h2>2. Основные условия</h2>
                <div class="grid"><div><b>Контрагент</b><br>{Field("Контрагент")}</div><div><b>Сумма</b><br>{Field("Сумма договора")}</div><div><b>Срок</b><br>{DateOnly(document.DueDate)}</div><div><b>Статус</b><br>{statusLabel}</div></div>
                """,
            DocumentType.Invoice => $"""
                <div class="doc-title">Счет на оплату № {Field("Номер счета", document.DocumentId.ToString())}</div>
                <div class="row"><span>Поставщик: {Field("Поставщик", "ООО «Документооборот»")}</span><span>{Field("Дата счета", DateOnly(document.DueDate))}</span></div>
                <table><thead><tr><th>№</th><th>Наименование</th><th>Кол-во</th><th>Сумма</th></tr></thead><tbody><tr><td>1</td><td>{title}</td><td>1</td><td>{Field("Сумма")}</td></tr></tbody><tfoot><tr><th colspan="3">Итого</th><th>{Field("Сумма")}</th></tr></tfoot></table>
                """,
            DocumentType.Application => $"""
                <div class="recipient">Руководителю организации<br>от {Field("ФИО сотрудника", "сотрудника")}</div>
                <div class="doc-title">Заявление</div>
                <div class="doc-subtitle">{Field("Тема обращения", document.Title ?? "Тема обращения")}</div>
                <p>{Field("Текст заявления", document.ExtractedText ?? "Текст заявления не заполнен.")}</p>
                """,
            DocumentType.Order => $"""
                <div class="doc-title">Приказ № {document.DocumentId}</div>
                <div class="doc-subtitle">{title}</div>
                <div class="row"><span>г. Санкт-Петербург</span><span>{DateOnly(document.DueDate)}</span></div>
                <h2>Приказываю</h2>
                <p>{description}</p>
                """,
            DocumentType.Act => $"""
                <div class="doc-title">Акт № {document.DocumentId}</div>
                <div class="doc-subtitle">{title}</div>
                <div class="row"><span>г. Санкт-Петербург</span><span>{DateOnly(document.DueDate)}</span></div>
                <h2>Содержание акта</h2>
                <p>{description}</p>
                """,
            _ => $"""
                <div class="doc-title">{typeLabel} № {document.DocumentId}</div>
                <div class="doc-subtitle">{title}</div>
                <p>{description}</p>
                """
        };

        var style = """
            body { margin: 0; background: #eef2f7; color: #111827; font-family: "Times New Roman", serif; }
            .page { width: 210mm; min-height: 297mm; margin: 18px auto; background: #fff; padding: 18mm 20mm; box-sizing: border-box; box-shadow: 0 12px 32px rgba(15, 23, 42, .16); }
            .org { display: grid; grid-template-columns: 1fr auto; gap: 24px; border-bottom: 2px solid #111827; padding-bottom: 12px; margin-bottom: 22px; }
            .org-name { font-size: 18px; font-weight: 700; text-transform: uppercase; }
            .org-meta { margin-top: 5px; font-size: 12px; line-height: 1.45; color: #374151; }
            .stamp { width: 84px; height: 84px; border: 2px solid #9ca3af; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: #6b7280; font-size: 11px; text-align: center; transform: rotate(-8deg); }
            .doc-title { margin: 26px 0 10px; text-align: center; font-size: 20px; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; }
            .doc-subtitle { text-align: center; font-size: 15px; font-weight: 700; margin-bottom: 18px; }
            .row { display: flex; justify-content: space-between; gap: 24px; margin: 8px 0 18px; font-size: 14px; }
            .recipient { text-align: right; line-height: 1.5; margin-bottom: 28px; }
            h2 { margin: 22px 0 8px; font-size: 14px; text-transform: uppercase; border-bottom: 1px solid #d1d5db; padding-bottom: 4px; }
            p { font-size: 14px; line-height: 1.55; text-align: justify; white-space: pre-wrap; overflow-wrap: anywhere; }
            .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px 14px; }
            .grid > div { border: 1px solid #d1d5db; padding: 8px 10px; }
            table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 14px; }
            th, td { border: 1px solid #111827; padding: 8px; vertical-align: top; }
            th { background: #f3f4f6; }
            .signatures { margin-top: 34px; display: grid; grid-template-columns: 1fr 1fr; gap: 42px; }
            .line { border-bottom: 1px solid #111827; height: 34px; margin-top: 10px; }
            .hint { margin-top: 4px; color: #6b7280; font-size: 11px; font-family: Arial, sans-serif; text-align: center; }
            .footer { margin-top: 28px; padding-top: 8px; border-top: 1px solid #d1d5db; color: #6b7280; font-size: 11px; font-family: Arial, sans-serif; }
            @media print { body { background: #fff; } .page { margin: 0; box-shadow: none; } }
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
                <meta charset="utf-8">
                <title>Итоговый файл исполнения #{document.DocumentId}</title>
                <style>{style}</style>
            </head>
            <body>
                <main class="page">
                    <header class="org">
                        <div>
                            <div class="org-name">ООО «Документооборот»</div>
                            <div class="org-meta">190000, г. Санкт-Петербург, Невский проспект, д. 1<br>ИНН 7800000000 / КПП 780001001<br>Электронная система документационного обеспечения управления</div>
                        </div>
                        <div class="stamp">Место<br>печати</div>
                    </header>
                    {body}
                    <h2>Служебная информация</h2>
                    <div class="grid">
                        <div><b>Номер карточки</b><br>{document.DocumentId}</div>
                        <div><b>Статус</b><br>{statusLabel}</div>
                        <div><b>Исполнитель</b><br>{Enc(executorName)}</div>
                        <div><b>Результат</b><br>{Enc(document.ExecutionResult)}</div>
                        <div><b>Начало исполнения</b><br>{DateTimeLabel(document.ExecutionStartedAt)}</div>
                        <div><b>Завершение исполнения</b><br>{DateTimeLabel(document.ExecutionCompletedAt)}</div>
                    </div>
                    <h2>Комментарий исполнителя</h2>
                    <p>{Enc(document.ExecutionComment)}</p>
                    <section class="signatures">
                        <div><strong>Ответственный</strong><div class="line"></div><div class="hint">подпись / расшифровка</div></div>
                        <div><strong>Дата</strong><div class="line"></div><div class="hint">дд.мм.гггг</div></div>
                    </section>
                    <div class="footer">Файл сформирован автоматически из электронной карточки документа #{document.DocumentId} в DocManager. Дата формирования: {generatedAt}.</div>
                </main>
            </body>
            </html>
            """;
    }

    private static string GetTemplateFieldValue(string? text, string label, string fallback = "Не указано")
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var prefix = $"- {label}:";
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmed[prefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        return fallback;
    }

    private static bool IsAllowedPdf(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Некоторые браузеры и прокси отдают octet-stream, поэтому разрешаем такой вариант по расширению.
            if (!string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool IsAllowedIncomingFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);

        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return IsAllowedPdf(file);

        if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsAllowedExecutionAttachment(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);

        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return IsAllowedPdf(file);

        if (string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(file.ContentType, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(file.ContentType, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static DocumentType ClassifyIncomingDocument(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (ContainsAny(name, "договор", "contract", "agreement"))
            return DocumentType.Contract;

        if (ContainsAny(name, "счет", "счёт", "invoice", "bill"))
            return DocumentType.Invoice;

        if (ContainsAny(name, "акт", "act"))
            return DocumentType.Act;

        if (ContainsAny(name, "приказ", "order"))
            return DocumentType.Order;

        if (ContainsAny(name, "заявление", "application", "request"))
            return DocumentType.Application;

        if (ContainsAny(name, "отчет", "отчёт", "report"))
            return DocumentType.Report;

        return DocumentType.Other;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetUploadsRootPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, UploadFolderName, "documents");
    }

    private void ApplyQuickCreateDefaults(CreateDocumentPageViewModel model)
    {
        // Быстрый сценарий: если загружен файл, но поля не заполнены,
        // подставляем безопасные значения по умолчанию.
        if (model.File is null || model.File.Length <= 0)
            return;

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            var baseName = Path.GetFileNameWithoutExtension(model.File.FileName);
            model.Title = string.IsNullOrWhiteSpace(baseName)
                ? $"Документ {DateTime.UtcNow:yyyyMMdd-HHmmss}"
                : baseName;
            ModelState.Remove(nameof(model.Title));
        }

        if (model.Type is null)
        {
            var types = Enum.GetValues<DocumentType>();
            var idx = Math.Abs((model.File.FileName ?? string.Empty).GetHashCode()) % types.Length;
            model.Type = types[idx];
            ModelState.Remove(nameof(model.Type));
        }

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            model.Description = "Создано через быструю загрузку файла.";
            ModelState.Remove(nameof(model.Description));
        }

        model.Tags ??= "quick-upload,pdf";
        model.Priority ??= 2;
    }

    private static TEnum? TryParseEnum<TEnum>(string? stored) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        if (Enum.TryParse<TEnum>(stored, true, out var parsed))
            return parsed;

        if (int.TryParse(stored, out var intVal) && Enum.IsDefined(typeof(TEnum), intVal))
            return (TEnum)Enum.ToObject(typeof(TEnum), intVal);

        return null;
    }

    private static DocumentStatus ParseDocumentStatus(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return DocumentStatus.Draft;

        if (string.Equals(stored, "InProgress", StringComparison.OrdinalIgnoreCase))
            return DocumentStatus.OnApproval;

        if (string.Equals(stored, "Rejected", StringComparison.OrdinalIgnoreCase))
            return DocumentStatus.Draft;

        if (Enum.TryParse<DocumentStatus>(stored, true, out var parsed))
            return parsed;

        if (int.TryParse(stored, out var intVal) && Enum.IsDefined(typeof(DocumentStatus), intVal))
            return (DocumentStatus)Enum.ToObject(typeof(DocumentStatus), intVal);

        return DocumentStatus.Draft;
    }

    private static DocumentStatus GetNextStatus(DocumentStatus current) => current switch
    {
        DocumentStatus.Draft => DocumentStatus.OnApproval,
        DocumentStatus.OnApproval => DocumentStatus.Approved,
        DocumentStatus.Approved => DocumentStatus.InWork,
        DocumentStatus.InWork => DocumentStatus.Completed,
        DocumentStatus.Completed => DocumentStatus.Archived,
        DocumentStatus.Archived => DocumentStatus.Archived,
        _ => DocumentStatus.Draft
    };

    private static string GetDocumentTypeLabel(DocumentType type) => type switch
    {
        DocumentType.Contract => "Договор",
        DocumentType.Invoice => "Счет",
        DocumentType.Report => "Отчет",
        DocumentType.Order => "Приказ",
        DocumentType.Application => "Заявление",
        DocumentType.Act => "Акт",
        _ => "Прочее"
    };

    private static string GetDocumentStatusLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Черновик",
        DocumentStatus.OnApproval => "На согласовании",
        DocumentStatus.Approved => "Утвержден",
        DocumentStatus.InWork => "В работе",
        DocumentStatus.Completed => "Завершен",
        DocumentStatus.Archived => "Архив",
        _ => "Неизвестно"
    };

    private static IReadOnlyList<AiSuggestionViewModel> BuildAiSuggestions(Document doc)
    {
        var seed = doc.DocumentId;
        if (!string.IsNullOrWhiteSpace(doc.FileHash))
        {
            unchecked
            {
                foreach (var ch in doc.FileHash)
                    seed = (seed * 31) + ch;
            }
        }

        var rng = new Random(seed);

        int Conf(bool high) => high ? rng.Next(82, 97) : rng.Next(45, 72);
        bool Flip() => rng.NextDouble() > 0.45;

        var currentType = TryParseEnum<DocumentType>(doc.DocumentType) ?? DocumentType.Other;
        var availableTypes = Enum.GetValues<DocumentType>();
        var suggestedType = Flip() ? currentType : availableTypes[rng.Next(availableTypes.Length)];

        var currentDue = doc.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        var suggestedDue = string.IsNullOrWhiteSpace(currentDue)
            ? DateTime.UtcNow.Date.AddDays(rng.Next(2, 20)).ToString("yyyy-MM-dd")
            : currentDue;

        var title = doc.Title ?? string.Empty;
        var suggestedTitle = string.IsNullOrWhiteSpace(title) ? $"Документ #{doc.DocumentId}" : title;

        var description = doc.ExtractedText ?? string.Empty;
        var suggestedDescription = string.IsNullOrWhiteSpace(description)
            ? "Извлечено ИИ: краткое описание документа."
            : description;

        var tags = doc.Tags ?? string.Empty;
        var suggestedTags = string.IsNullOrWhiteSpace(tags) ? "pdf,входящие" : tags;

        var priority = doc.Priority?.ToString() ?? string.Empty;
        var suggestedPriority = string.IsNullOrWhiteSpace(priority) ? rng.Next(1, 4).ToString() : priority;

        var lowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "title", "type", "duedate", "description", "priority", "tags" }.OrderBy(_ => rng.Next()).Take(3))
            lowKeys.Add(key);

        return
        [
            new AiSuggestionViewModel { FieldKey = "type", Label = "Тип", SuggestedValue = suggestedType.ToString(), Confidence = Conf(!lowKeys.Contains("type")) },
            new AiSuggestionViewModel { FieldKey = "duedate", Label = "Срок исполнения", SuggestedValue = suggestedDue, Confidence = Conf(!lowKeys.Contains("duedate")) },
            new AiSuggestionViewModel { FieldKey = "title", Label = "Название", SuggestedValue = suggestedTitle, Confidence = Conf(!lowKeys.Contains("title")) },
            new AiSuggestionViewModel { FieldKey = "description", Label = "Описание", SuggestedValue = suggestedDescription, Confidence = Conf(!lowKeys.Contains("description")) },
            new AiSuggestionViewModel { FieldKey = "priority", Label = "Приоритет", SuggestedValue = suggestedPriority, Confidence = Conf(!lowKeys.Contains("priority")) },
            new AiSuggestionViewModel { FieldKey = "tags", Label = "Теги", SuggestedValue = suggestedTags, Confidence = Conf(!lowKeys.Contains("tags")) }
        ];
    }

    private async Task EnrichEditModelAsync(EditDocumentPageViewModel model)
    {
        var doc = await _documentService.GetDocumentByIdAsync(model.Id);
        if (doc is null)
            return;

        model.Status = doc.Status ?? model.Status;
        model.RouteTemplateId = doc.RouteTemplateId;
        model.RouteTemplateName = await GetRouteTemplateNameAsync(doc.RouteTemplateId, CancellationToken.None);
        model.RouteTemplateOptions = await LoadRouteTemplateOptionsAsync(CancellationToken.None, ParseDocumentType(doc.DocumentType));
        model.ApprovalRouteSteps = await BuildApprovalRouteStepsAsync(doc, CancellationToken.None);
        model.RouteApproverOptions = await LoadRouteApproverOptionsAsync(CancellationToken.None);
        model.NomenclatureCaseId = doc.NomenclatureCaseId;
        model.NomenclatureCaseLabel = await BuildNomenclatureCaseLabelAsync(doc.NomenclatureCaseId, CancellationToken.None);
        model.NomenclatureCaseOptions = await LoadNomenclatureCaseOptionsAsync(CancellationToken.None);
        model.FileUrl = doc.FilePath;
        model.FileKind = GetFileKind(doc.FilePath);
        model.TemplateFields = await BuildTemplateFieldDisplayAsync(doc, CancellationToken.None);
        model.TemplateName = model.TemplateFields.Count > 0
            ? await GetTemplateNameAsync(doc.TemplateId, CancellationToken.None)
            : null;
        model.ExecutionComment = doc.ExecutionComment ?? string.Empty;
        model.ExecutionResult = doc.ExecutionResult;
        model.ExecutionStartedAt = doc.ExecutionStartedAt;
        model.ExecutionCompletedAt = doc.ExecutionCompletedAt;
        model.ExecutionFileUrl = doc.ExecutionFilePath;
        model.ExecutionFileName = doc.ExecutionFileName;
        model.ExecutionFileKind = GetFileKind(doc.ExecutionFilePath);
        model.AiSuggestions = BuildAiSuggestions(doc);
        model.Assignment = await BuildAssignmentPanelAsync(doc, CancellationToken.None);
        ApplyExecutionHints(model);
        ApplyEditAccessState(model, doc);
    }

    private static string GetFileKind(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return "none";

        var ext = Path.GetExtension(fileUrl);
        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";

        if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
            return "image";

        if (string.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase))
            return "html";

        return "other";
    }

    private static string GetContentTypeByExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
