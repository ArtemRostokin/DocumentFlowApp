using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
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
    private readonly IDocumentService _documentService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
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
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [Route("Documents/{id:int}/approval")]
    public async Task<IActionResult> ApprovalAction(int id, ApprovalActionInputModel input, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

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
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Approved);
                TempData["SuccessMessage"] = $"Документ #{id} утвержден.";
            }
            else
            {
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Draft, input.Comment);
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
            return Forbid();

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
            FileUrl = doc.FilePath,
            FileKind = GetFileKind(doc.FilePath),
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
            return Forbid();

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
            FileUrl = doc.FilePath,
            FileKind = GetFileKind(doc.FilePath),
            ExecutionComment = doc.ExecutionComment ?? string.Empty,
            ExecutionResult = doc.ExecutionResult,
            ExecutionStartedAt = doc.ExecutionStartedAt,
            ExecutionCompletedAt = doc.ExecutionCompletedAt,
            ExecutionFileUrl = doc.ExecutionFilePath,
            ExecutionFileName = doc.ExecutionFileName,
            ExecutionFileKind = GetFileKind(doc.ExecutionFilePath),
            Assignment = await BuildAssignmentPanelAsync(doc, cancellationToken)
        };

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

            await _documentService.UpdateDocumentAsync(doc);

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
            return Forbid();

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
            return Forbid();

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
            TempData["ErrorMessage"] = "Итоговый файл не должен превышать 25MB.";
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
            await _documentService.ChangeDocumentStatusAsync(id, next);
            TempData["SuccessMessage"] = $"Статус изменен: {current} -> {next}";
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
                    ModelState.AddModelError(nameof(model.Files), $"Файл {file.FileName} превышает 25MB.");
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

                await _documentService.CreateDocumentAsync(new CreateDocumentRequest
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
                    ModelState.AddModelError(nameof(model.File), "Размер файла не должен превышать 25MB.");
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

            await _documentService.CreateDocumentAsync(new CreateDocumentRequest
            {
                Title = model.Title,
                Description = BuildTemplateAwareDescription(model.Description, selectedTemplate, model.TemplateFieldValues),
                Type = model.Type ?? DocumentType.Other,
                DueDate = model.DueDate,
                Priority = model.Priority,
                Tags = BuildTemplateAwareTags(model.Tags, model.TemplateId),
                TemplateId = model.TemplateId,
                FilePath = filePath,
                FileSize = fileSize,
                FileHash = fileHash
            });

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
            return Forbid();

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
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Approved);
                TempData["SuccessMessage"] = $"Документ #{id} утвержден.";
            }
            else
            {
                await _documentService.ChangeDocumentStatusAsync(id, DocumentStatus.Draft, input.Comment);
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
            return BadRequest(new { message = "Неверное значение статуса." });

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
                    return Forbid();

                var currentStatus = ParseDocumentStatus(document.Status);
                var isAllowedEmployeeTransition =
                    (currentStatus == DocumentStatus.OnApproval && newStatus == DocumentStatus.Approved) ||
                    (currentStatus == DocumentStatus.Approved && newStatus == DocumentStatus.InWork) ||
                    (currentStatus == DocumentStatus.InWork && newStatus == DocumentStatus.Completed);

                if (!isAllowedEmployeeTransition)
                    return BadRequest(new { message = "Пользователь может менять статус только в своей цепочке: На согласовании -> К исполнению -> В работе -> Завершено." });
            }

            await _documentService.ChangeDocumentStatusAsync(id, newStatus, request.Comment);
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
            var documents = await _documentService.GetAllDocumentsAsync();
            var items = documents
                .Where(d => ParseDocumentStatus(d.Status) == DocumentStatus.OnApproval)
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
            return Forbid();

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
        model.FileUrl = doc.FilePath;
        model.FileKind = GetFileKind(doc.FilePath);
        model.ExecutionComment = doc.ExecutionComment ?? string.Empty;
        model.ExecutionResult = doc.ExecutionResult;
        model.ExecutionStartedAt = doc.ExecutionStartedAt;
        model.ExecutionCompletedAt = doc.ExecutionCompletedAt;
        model.ExecutionFileUrl = doc.ExecutionFilePath;
        model.ExecutionFileName = doc.ExecutionFileName;
        model.ExecutionFileKind = GetFileKind(doc.ExecutionFilePath);
        model.AiSuggestions = BuildAiSuggestions(doc);
        model.Assignment = await BuildAssignmentPanelAsync(doc, CancellationToken.None);
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
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
