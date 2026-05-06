using System.Security.Claims;
using System.Text.Json;
using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Web.Models;
using DocumentFlowApp.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminController> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AdminController(ApplicationDbContext dbContext, IAuditService auditService, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildDashboardPageAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Routes(CancellationToken cancellationToken)
    {
        return View(await BuildRoutesPageAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Templates(CancellationToken cancellationToken)
    {
        return View(await BuildTemplatesPageAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDocumentTemplate([Bind(Prefix = "NewTemplate")] CreateDocumentTemplateAdminInputModel input, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CreateDocumentTemplate POST received. Name={TemplateName}, DocumentType={DocumentType}, FieldRows={FieldRows}",
            input.Name,
            input.DocumentType,
            input.Fields?.Count ?? 0);

        ValidateTemplateInput(input.Name, input.DocumentType, input.Fields);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "CreateDocumentTemplate validation failed. Errors: {Errors}",
                string.Join(" | ", ModelState.SelectMany(x => x.Value?.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}") ?? [])));
            return View("Templates", await BuildTemplatesPageAsync(cancellationToken, createInput: input));
        }

        try
        {
            var normalizedName = input.Name.Trim();
            if (await _dbContext.Templates.AnyAsync(x => x.Name == normalizedName, cancellationToken))
            {
                ModelState.AddModelError("NewTemplate.Name", "Шаблон с таким названием уже существует.");
                return View("Templates", await BuildTemplatesPageAsync(cancellationToken, createInput: input));
            }

            var template = new Template
            {
                Name = normalizedName,
                Category = input.DocumentType!.Value.ToString(),
                Content = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                AiSuggestedFields = SerializeTemplateFields(input.Fields),
                UsageCount = 0,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.Templates.Add(template);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.TemplateCreated,
                $"Создан шаблон документа {template.Name} для типа {GetDocumentTypeLabel(input.DocumentType.Value)}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Шаблон документа создан.";
            return RedirectToAction(nameof(Templates));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать шаблон документа {TemplateName}", input.Name);
            ModelState.AddModelError(string.Empty, "Не удалось сохранить шаблон документа.");
            return View("Templates", await BuildTemplatesPageAsync(cancellationToken, createInput: input));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDocumentTemplate(UpdateDocumentTemplateAdminInputModel input, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "UpdateDocumentTemplate POST received. TemplateId={TemplateId}, Name={TemplateName}, DocumentType={DocumentType}, FieldRows={FieldRows}",
            input.TemplateId,
            input.Name,
            input.DocumentType,
            input.Fields?.Count ?? 0);

        ValidateTemplateInput(input.Name, input.DocumentType, input.Fields);

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Проверьте корректность данных шаблона.";
            return RedirectToAction(nameof(Templates));
        }

        var template = await _dbContext.Templates.FirstOrDefaultAsync(x => x.TemplateId == input.TemplateId, cancellationToken);
        if (template is null)
        {
            TempData["ErrorMessage"] = "Шаблон документа не найден.";
            return RedirectToAction(nameof(Templates));
        }

        try
        {
            var normalizedName = input.Name.Trim();
            if (await _dbContext.Templates.AnyAsync(x => x.TemplateId != input.TemplateId && x.Name == normalizedName, cancellationToken))
            {
                TempData["ErrorMessage"] = "Другой шаблон уже использует это название.";
                return RedirectToAction(nameof(Templates));
            }

            var previousSummary = $"{template.Name} ({template.Category ?? "без типа"})";
            template.Name = normalizedName;
            template.Category = input.DocumentType!.Value.ToString();
            template.Content = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            template.AiSuggestedFields = SerializeTemplateFields(input.Fields);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.TemplateUpdated,
                $"Обновлен шаблон документа {previousSummary} -> {template.Name} ({GetDocumentTypeLabel(input.DocumentType.Value)}).",
                cancellationToken);

            TempData["SuccessMessage"] = "Шаблон документа обновлен.";
            return RedirectToAction(nameof(Templates));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить шаблон документа {TemplateId}", input.TemplateId);
            TempData["ErrorMessage"] = "Не удалось обновить шаблон документа.";
            return RedirectToAction(nameof(Templates));
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRouteTemplate([Bind(Prefix = "NewTemplate")] CreateRouteTemplateAdminInputModel input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            ModelState.AddModelError(nameof(input.Name), "Введите название шаблона маршрута.");

        if (!ModelState.IsValid)
            return View("Routes", await BuildRoutesPageAsync(cancellationToken, newTemplate: input));

        try
        {
            if (input.IsDefault)
            {
                var currentDefaults = await _dbContext.RouteTemplates
                    .Where(x => x.DocumentType == (input.DocumentType == null ? null : input.DocumentType.ToString()) && x.IsDefault)
                    .ToListAsync(cancellationToken);

                foreach (var currentDefault in currentDefaults)
                    currentDefault.IsDefault = false;
            }

            var template = new RouteTemplate
            {
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                DocumentType = input.DocumentType?.ToString(),
                Department = string.IsNullOrWhiteSpace(input.Department) ? null : input.Department.Trim(),
                IsActive = input.IsActive,
                IsDefault = input.IsDefault,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.RouteTemplates.Add(template);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.DocumentUpdated,
                $"Создан шаблон маршрута \"{template.Name}\" для типа {(template.DocumentType ?? "Any")}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Шаблон маршрута создан.";
            return RedirectToAction(nameof(Routes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать шаблон маршрута {RouteTemplateName}", input.Name);
            ModelState.AddModelError(string.Empty, "Не удалось создать шаблон маршрута.");
            return View("Routes", await BuildRoutesPageAsync(cancellationToken, newTemplate: input));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRouteStep([Bind(Prefix = "NewStep")] AddRouteStepAdminInputModel input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            ModelState.AddModelError(nameof(input.Title), "Введите название шага.");

        var approvalSpecialization = ApprovalSpecializations.Normalize(input.ApproverSpecialization);
        if (approvalSpecialization is null)
            ModelState.AddModelError(nameof(input.ApproverSpecialization), "Выберите бизнес-роль согласования.");

        var template = await _dbContext.RouteTemplates.FirstOrDefaultAsync(x => x.RouteTemplateId == input.RouteTemplateId, cancellationToken);
        if (template is null)
            ModelState.AddModelError(nameof(input.RouteTemplateId), "Шаблон маршрута не найден.");

        var approver = input.ApproverUserId is null
            ? null
            : await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == input.ApproverUserId.Value && x.IsActive, cancellationToken);

        if (input.ApproverUserId is not null && approver is null)
            ModelState.AddModelError(nameof(input.ApproverUserId), "Выберите активного согласующего.");

        if (approver is not null && approvalSpecialization is not null)
        {
            var approverSpecialization = ApprovalSpecializations.Normalize(approver.ApprovalSpecialization);
            if (!string.Equals(approverSpecialization, approvalSpecialization, StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(input.ApproverUserId), "Выбранный пользователь не соответствует указанной бизнес-роли согласования.");
        }

        if (!ModelState.IsValid)
            return View("Routes", await BuildRoutesPageAsync(cancellationToken, newStep: input));

        try
        {
            var step = new RouteStep
            {
                RouteTemplateId = input.RouteTemplateId,
                StepOrder = input.StepOrder,
                Title = input.Title.Trim(),
                ApproverSpecialization = approvalSpecialization,
                ApproverUserId = input.ApproverUserId,
                ApproverRole = approver?.Role?.RoleName ?? "Employee",
                IsRequired = input.IsRequired
            };

            _dbContext.RouteSteps.Add(step);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.DocumentUpdated,
                $"Добавлен шаг маршрута \"{step.Title}\" в шаблон \"{template!.Name}\".",
                cancellationToken);

            TempData["SuccessMessage"] = "Шаг маршрута добавлен.";
            return RedirectToAction(nameof(Routes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось добавить шаг маршрута в шаблон {RouteTemplateId}", input.RouteTemplateId);
            ModelState.AddModelError(string.Empty, "Не удалось добавить шаг маршрута.");
            return View("Routes", await BuildRoutesPageAsync(cancellationToken, newStep: input));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRouteTemplate(UpdateRouteTemplateAdminInputModel input, CancellationToken cancellationToken)
    {
        var template = await _dbContext.RouteTemplates.FirstOrDefaultAsync(x => x.RouteTemplateId == input.RouteTemplateId, cancellationToken);
        if (template is null)
        {
            TempData["ErrorMessage"] = "Шаблон маршрута не найден.";
            return RedirectToAction(nameof(Routes));
        }

        if (input.IsDefault)
        {
            var currentDefaults = await _dbContext.RouteTemplates
                .Where(x => x.RouteTemplateId != template.RouteTemplateId && x.DocumentType == template.DocumentType && x.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var currentDefault in currentDefaults)
                currentDefault.IsDefault = false;
        }

        template.IsActive = input.IsActive;
        template.IsDefault = input.IsDefault;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogSystemActivityAsync(
            GetCurrentUserId(),
            AuditActivityTypes.DocumentUpdated,
            $"Обновлены параметры шаблона маршрута \"{template.Name}\".",
            cancellationToken);

        TempData["SuccessMessage"] = "Параметры шаблона маршрута обновлены.";
        return RedirectToAction(nameof(Routes));
    }

    [HttpGet]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        return View(await BuildUsersPageAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser([Bind(Prefix = "NewUser")] CreateUserAdminInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Users", await BuildUsersPageAsync(cancellationToken, input));

        var normalizedUserName = input.UserName.Trim();
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();

        if (await _dbContext.Users.AnyAsync(x => x.UserName == normalizedUserName, cancellationToken))
            ModelState.AddModelError(nameof(input.UserName), "Пользователь с таким логином уже существует.");

        if (await _dbContext.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken))
            ModelState.AddModelError(nameof(input.Email), "Пользователь с таким email уже существует.");

        var role = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == input.RoleId, cancellationToken);

        if (role is null)
            ModelState.AddModelError(nameof(input.RoleId), "Выберите существующую роль.");

        var approvalSpecialization = ApprovalSpecializations.Normalize(input.ApprovalSpecialization);
        if (input.ApprovalSpecialization is not null && approvalSpecialization is null)
            ModelState.AddModelError(nameof(input.ApprovalSpecialization), "Выберите корректную бизнес-роль согласования.");

        if (!ModelState.IsValid)
            return View("Users", await BuildUsersPageAsync(cancellationToken, input));

        try
        {
            var isActive = ReadPostedBoolean("NewUser.IsActive", input.IsActive);
            var user = new User
            {
                UserName = normalizedUserName,
                Email = input.Email.Trim(),
                FirstName = input.FirstName.Trim(),
                LastName = input.LastName.Trim(),
                RoleId = input.RoleId,
                ApprovalSpecialization = approvalSpecialization,
                IsActive = isActive,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, input.Password);

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.UserCreated,
                $"Создан пользователь {user.UserName} ({user.Email}) с ролью {role!.RoleName} и профилем согласования {ApprovalSpecializations.GetLabel(user.ApprovalSpecialization)}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Пользователь создан.";
            return RedirectToAction(nameof(Users));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать пользователя {UserName}", input.UserName);
            ModelState.AddModelError(string.Empty, "Не удалось создать пользователя.");
            return View("Users", await BuildUsersPageAsync(cancellationToken, input));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(UpdateUserAdminInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Проверьте корректность данных пользователя.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == input.UserId, cancellationToken);

        if (user is null)
        {
            TempData["ErrorMessage"] = "Пользователь не найден.";
            return RedirectToAction(nameof(Users));
        }

        var normalizedUserName = input.UserName.Trim();
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();

        if (await _dbContext.Users.AnyAsync(x => x.UserId != input.UserId && x.UserName == normalizedUserName, cancellationToken))
        {
            TempData["ErrorMessage"] = "Другой пользователь уже использует этот логин.";
            return RedirectToAction(nameof(Users));
        }

        if (await _dbContext.Users.AnyAsync(x => x.UserId != input.UserId && x.Email.ToLower() == normalizedEmail, cancellationToken))
        {
            TempData["ErrorMessage"] = "Другой пользователь уже использует этот email.";
            return RedirectToAction(nameof(Users));
        }

        var role = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == input.RoleId, cancellationToken);

        if (role is null)
        {
            TempData["ErrorMessage"] = "Выберите существующую роль.";
            return RedirectToAction(nameof(Users));
        }

        var approvalSpecialization = ApprovalSpecializations.Normalize(input.ApprovalSpecialization);
        if (input.ApprovalSpecialization is not null && approvalSpecialization is null)
        {
            TempData["ErrorMessage"] = "Выберите корректную бизнес-роль согласования.";
            return RedirectToAction(nameof(Users));
        }

        try
        {
            var previousRole = user.Role?.RoleName ?? "без роли";
            var previousState = user.IsActive ? "активен" : "отключен";
            var previousUserName = user.UserName;
            var previousEmail = user.Email;
            var previousFullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            var previousApprovalSpecialization = ApprovalSpecializations.GetLabel(user.ApprovalSpecialization);
            var isActive = ReadPostedBoolean("IsActive", input.IsActive);

            user.UserName = normalizedUserName;
            user.Email = input.Email.Trim();
            user.FirstName = input.FirstName.Trim();
            user.LastName = input.LastName.Trim();
            user.RoleId = input.RoleId;
            user.ApprovalSpecialization = approvalSpecialization;
            user.IsActive = isActive;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var currentState = user.IsActive ? "активен" : "отключен";
            var currentFullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.UserUpdated,
                $"Обновлен пользователь {previousUserName}: логин {previousUserName} -> {user.UserName}, email {previousEmail} -> {user.Email}, ФИО {previousFullName} -> {currentFullName}, роль {previousRole} -> {role.RoleName}, бизнес-роль {previousApprovalSpecialization} -> {ApprovalSpecializations.GetLabel(user.ApprovalSpecialization)}, состояние {previousState} -> {currentState}.",
                cancellationToken);

            TempData["SuccessMessage"] = "Данные пользователя обновлены.";
            return RedirectToAction(nameof(Users));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить пользователя {UserId}", input.UserId);
            TempData["ErrorMessage"] = "Не удалось обновить пользователя.";
            return RedirectToAction(nameof(Users));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(ResetUserPasswordAdminInputModel input, CancellationToken cancellationToken)
    {
        var newPassword = ReadPostedString("NewPassword", input.NewPassword);

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length is < 8 or > 100)
        {
            TempData["ErrorMessage"] = "Новый пароль должен содержать от 8 до 100 символов.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.UserId == input.UserId, cancellationToken);
        if (user is null)
        {
            TempData["ErrorMessage"] = "Пользователь не найден.";
            return RedirectToAction(nameof(Users));
        }

        try
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.UserPasswordReset,
                $"Администратор сбросил пароль пользователя {user.UserName}.",
                cancellationToken);

            TempData["SuccessMessage"] = $"Пароль пользователя {user.UserName} обновлен.";
            return RedirectToAction(nameof(Users));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сбросить пароль пользователя {UserId}", input.UserId);
            TempData["ErrorMessage"] = "Не удалось обновить пароль пользователя.";
            return RedirectToAction(nameof(Users));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Nomenclature(CancellationToken cancellationToken)
    {
        return View(await BuildNomenclaturePageAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNomenclatureCase([Bind(Prefix = "NewCase")] CreateNomenclatureCaseInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, input, null));

        try
        {
            var exists = await _dbContext.NomenclatureCases
                .AnyAsync(x => x.Index == input.Index.Trim(), cancellationToken);

            if (exists)
            {
                ModelState.AddModelError(nameof(input.Index), "Дело с таким индексом уже существует.");
                return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, input, null));
            }

            _dbContext.NomenclatureCases.Add(new NomenclatureCase
            {
                Index = input.Index.Trim(),
                Title = input.Title.Trim(),
                RetentionPeriod = input.RetentionPeriod.Trim(),
                LegalBasis = string.IsNullOrWhiteSpace(input.LegalBasis) ? null : input.LegalBasis.Trim(),
                Department = string.IsNullOrWhiteSpace(input.Department) ? null : input.Department.Trim(),
                IsActive = true
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.NomenclatureCaseCreated,
                $"Создано дело номенклатуры {input.Index.Trim()} - {input.Title.Trim()}.",
                cancellationToken);
            TempData["SuccessMessage"] = "Дело номенклатуры добавлено.";
            return RedirectToAction(nameof(Nomenclature));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать дело номенклатуры");
            ModelState.AddModelError(string.Empty, "Не удалось сохранить дело номенклатуры.");
            return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, input, null));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNomenclatureRule([Bind(Prefix = "NewRule")] CreateNomenclatureRuleInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, null, input));

        try
        {
            if (input.NomenclatureCaseId is null)
            {
                ModelState.AddModelError(nameof(input.NomenclatureCaseId), "Выберите дело номенклатуры.");
                return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, null, input));
            }

            var targetCase = await _dbContext.NomenclatureCases
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NomenclatureCaseId == input.NomenclatureCaseId.Value && x.IsActive, cancellationToken);

            if (targetCase is null)
            {
                ModelState.AddModelError(nameof(input.NomenclatureCaseId), "Выберите активное дело номенклатуры.");
                return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, null, input));
            }

            _dbContext.NomenclatureRules.Add(new NomenclatureRule
            {
                NomenclatureCaseId = input.NomenclatureCaseId.Value,
                DocumentType = string.IsNullOrWhiteSpace(input.DocumentType) ? null : input.DocumentType.Trim(),
                Department = string.IsNullOrWhiteSpace(input.Department) ? null : input.Department.Trim(),
                Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                IsActive = true
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditService.LogSystemActivityAsync(
                GetCurrentUserId(),
                AuditActivityTypes.NomenclatureRuleCreated,
                $"Создано правило автопривязки для типа {(string.IsNullOrWhiteSpace(input.DocumentType) ? "любого типа" : input.DocumentType.Trim())}.",
                cancellationToken);
            TempData["SuccessMessage"] = "Правило автопривязки добавлено.";
            return RedirectToAction(nameof(Nomenclature));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать правило номенклатуры");
            ModelState.AddModelError(string.Empty, "Не удалось сохранить правило автопривязки.");
            return View("Nomenclature", await BuildNomenclaturePageAsync(cancellationToken, null, input));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Audit(string? activityType, int? documentId, CancellationToken cancellationToken)
    {
        return View(await BuildAuditPageAsync(activityType, documentId, cancellationToken));
    }

    private async Task<AdminDashboardPageViewModel> BuildDashboardPageAsync(CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
        var activeUsers = await _dbContext.Users.CountAsync(x => x.IsActive, cancellationToken);
        var totalDocuments = await _dbContext.Documents.CountAsync(cancellationToken);
        var pendingApprovalDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.OnApproval), cancellationToken);
        var inWorkDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.InWork), cancellationToken);
        var completedDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.Completed), cancellationToken);
        var auditEventsToday = await _dbContext.DocumentActivity.CountAsync(x => (x.ActivityDate ?? DateTime.MinValue) >= todayUtc, cancellationToken);
        var activeNomenclatureCases = await _dbContext.NomenclatureCases.CountAsync(x => x.IsActive, cancellationToken);
        var activeNomenclatureRules = await _dbContext.NomenclatureRules.CountAsync(x => x.IsActive, cancellationToken);
        var documentsWithoutRoute = await _dbContext.Documents.CountAsync(
            x => (x.Status == nameof(DocumentStatus.Draft) || x.Status == nameof(DocumentStatus.OnApproval)) && x.RouteTemplateId == null,
            cancellationToken);
        var documentsWithoutNomenclature = await _dbContext.Documents.CountAsync(
            x => x.Status != nameof(DocumentStatus.Archived) && x.NomenclatureCaseId == null,
            cancellationToken);
        var routeTemplatesWithoutSteps = await _dbContext.RouteTemplates.CountAsync(
            x => !x.Steps.Any(),
            cancellationToken);
        var staleApprovalDocuments = await _dbContext.Documents.CountAsync(
            x => x.Status == nameof(DocumentStatus.OnApproval) &&
                 ((x.UpdatedDate ?? x.CreatedDate) <= DateTime.UtcNow.AddDays(-3)),
            cancellationToken);
        var inactiveUsers = totalUsers - activeUsers;

        var attentionItems = new List<AdminAttentionItemViewModel>();

        if (documentsWithoutRoute > 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Документы без маршрута",
                Value = documentsWithoutRoute.ToString(),
                Description = "Черновики и согласования без шаблона маршрута стоит проверить в первую очередь.",
                BadgeClass = "text-bg-danger"
            });
        }

        if (documentsWithoutNomenclature > 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Документы без номенклатуры",
                Value = documentsWithoutNomenclature.ToString(),
                Description = "Без привязки к делу документы нельзя корректно довести до архива.",
                BadgeClass = "text-bg-warning"
            });
        }

        if (routeTemplatesWithoutSteps > 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Пустые шаблоны маршрутов",
                Value = routeTemplatesWithoutSteps.ToString(),
                Description = "У этих шаблонов пока нет шагов согласования, поэтому их стоит настроить.",
                BadgeClass = "text-bg-warning"
            });
        }

        if (staleApprovalDocuments > 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Зависли на согласовании",
                Value = staleApprovalDocuments.ToString(),
                Description = "Документы на согласовании дольше 3 дней могут требовать внимания менеджера.",
                BadgeClass = "text-bg-primary"
            });
        }

        if (inactiveUsers > 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Неактивные пользователи",
                Value = inactiveUsers.ToString(),
                Description = "Проверьте, все ли отключенные учетные записи действительно должны оставаться неактивными.",
                BadgeClass = "text-bg-secondary"
            });
        }

        if (attentionItems.Count == 0)
        {
            attentionItems.Add(new AdminAttentionItemViewModel
            {
                Title = "Система в порядке",
                Value = "0 критичных пунктов",
                Description = "Сейчас нет очевидных проблем, требующих внимания администратора.",
                BadgeClass = "text-bg-success"
            });
        }

        return new AdminDashboardPageViewModel
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalDocuments = totalDocuments,
            PendingApprovalDocuments = pendingApprovalDocuments,
            InWorkDocuments = inWorkDocuments,
            CompletedDocuments = completedDocuments,
            AuditEventsToday = auditEventsToday,
            ActiveNomenclatureCases = activeNomenclatureCases,
            ActiveNomenclatureRules = activeNomenclatureRules,
            AttentionItems = attentionItems
        };
    }

    private async Task<RoutesAdminPageViewModel> BuildRoutesPageAsync(
        CancellationToken cancellationToken,
        CreateRouteTemplateAdminInputModel? newTemplate = null,
        AddRouteStepAdminInputModel? newStep = null)
    {
        var templates = await _dbContext.RouteTemplates
            .AsNoTracking()
            .Include(x => x.Steps)
            .ThenInclude(x => x.ApproverUser)
            .ThenInclude(x => x!.Role)
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new RoutesAdminPageViewModel
        {
            PendingApprovalDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.OnApproval), cancellationToken),
            ApprovedDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.Approved), cancellationToken),
            InWorkDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.InWork), cancellationToken),
            CompletedDocuments = await _dbContext.Documents.CountAsync(x => x.Status == nameof(DocumentStatus.Completed), cancellationToken),
            Templates = templates.Select(template => new RouteTemplateAdminListItemViewModel
            {
                Id = template.RouteTemplateId,
                Name = template.Name,
                Description = template.Description,
                DocumentType = TryParseEnum<DocumentType>(template.DocumentType),
                Department = template.Department,
                IsActive = template.IsActive,
                IsDefault = template.IsDefault,
                Steps = template.Steps
                    .OrderBy(step => step.StepOrder)
                    .Select(step => new RouteTemplateStepAdminViewModel
                    {
                        RouteStepId = step.RouteStepId,
                        StepOrder = step.StepOrder,
                        Title = step.Title,
                        ApproverRole = step.ApproverRole,
                        ApproverSpecialization = step.ApproverSpecialization,
                        ApproverSpecializationLabel = ApprovalSpecializations.GetLabel(step.ApproverSpecialization),
                        ApproverUserId = step.ApproverUserId,
                        ApproverDisplayName = FormatUserDisplayName(step.ApproverUser?.FirstName, step.ApproverUser?.LastName, step.ApproverUser?.UserName),
                        IsRequired = step.IsRequired
                    })
                    .ToList(),
            }).ToList(),
            Approvers = await BuildRouteApproverOptionsAsync(cancellationToken),
            ApprovalSpecializations = BuildApprovalSpecializationOptions(),
            NewTemplate = newTemplate ?? new CreateRouteTemplateAdminInputModel(),
            NewStep = newStep ?? new AddRouteStepAdminInputModel(),
            Stages =
            [
                new RouteStageItemViewModel
                {
                    Order = 1,
                    Title = "Черновик",
                    StatusCode = nameof(DocumentStatus.Draft),
                    ResponsibleRole = "Менеджер",
                    Description = "Менеджер создает карточку, проверяет реквизиты, шаблон и номенклатуру."
                },
                new RouteStageItemViewModel
                {
                    Order = 2,
                    Title = "На согласовании",
                    StatusCode = nameof(DocumentStatus.OnApproval),
                    ResponsibleRole = "Пользователь",
                    Description = "Согласующий проверяет данные документа и может утвердить его или вернуть на доработку."
                },
                new RouteStageItemViewModel
                {
                    Order = 3,
                    Title = "Утвержден",
                    StatusCode = nameof(DocumentStatus.Approved),
                    ResponsibleRole = "Менеджер",
                    Description = "После согласования менеджер назначает исполнителя и подготавливает документ к работе."
                },
                new RouteStageItemViewModel
                {
                    Order = 4,
                    Title = "В работе",
                    StatusCode = nameof(DocumentStatus.InWork),
                    ResponsibleRole = "Исполнитель",
                    Description = "Исполнитель фиксирует ход работы, результат и итоговый файл исполнения."
                },
                new RouteStageItemViewModel
                {
                    Order = 5,
                    Title = "Завершен",
                    StatusCode = nameof(DocumentStatus.Completed),
                    ResponsibleRole = "Менеджер / Администратор",
                    Description = "Документ завершен и готов к архивированию при наличии номенклатуры."
                },
                new RouteStageItemViewModel
                {
                    Order = 6,
                    Title = "Архив",
                    StatusCode = nameof(DocumentStatus.Archived),
                    ResponsibleRole = "Менеджер / Администратор",
                    Description = "Документ переводится в архив только после завершения и привязки к делу номенклатуры."
                }
            ],
            Roles =
            [
                new RouteRoleResponsibilityViewModel
                {
                    RoleName = "Администратор",
                    Responsibilities = "Настраивает пользователей, аудит, номенклатуру и контролирует системный контур."
                },
                new RouteRoleResponsibilityViewModel
                {
                    RoleName = "Менеджер",
                    Responsibilities = "Создает документы, отправляет их на согласование, назначает исполнителей и контролирует маршрут."
                },
                new RouteRoleResponsibilityViewModel
                {
                    RoleName = "Пользователь / Исполнитель",
                    Responsibilities = "Согласует документы, возвращает их на доработку и выполняет назначенные задачи."
                }
            ]
        };
    }

    private async Task<IReadOnlyList<RouteApproverOptionViewModel>> BuildRouteApproverOptionsAsync(CancellationToken cancellationToken)
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
                DisplayName = FormatUserDisplayName(x.FirstName, x.LastName, x.UserName),
                RoleName = x.RoleName,
                ApprovalSpecialization = x.ApprovalSpecialization,
                ApprovalSpecializationLabel = ApprovalSpecializations.GetLabel(x.ApprovalSpecialization)
            })
            .ToList();
    }

    private static string FormatUserDisplayName(string? firstName, string? lastName, string? userName)
    {
        var fullName = string.Join(" ", new[] { lastName, firstName }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(fullName) ? (userName ?? "Не назначен") : fullName;
    }

    private static TEnum? TryParseEnum<TEnum>(string? raw) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return Enum.TryParse<TEnum>(raw, true, out var parsed) ? parsed : null;
    }

    private async Task<UsersAdminPageViewModel> BuildUsersPageAsync(
        CancellationToken cancellationToken,
        CreateUserAdminInputModel? createInput = null)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(x => x.RoleName)
            .Select(x => new RoleOptionViewModel
            {
                Id = x.RoleId,
                Label = x.RoleName
            })
            .ToListAsync(cancellationToken);

        var rawUsers = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.UserName)
            .Select(x => new
            {
                x.UserId,
                x.UserName,
                x.Email,
                x.FirstName,
                x.LastName,
                x.ApprovalSpecialization,
                RoleName = x.Role != null ? x.Role.RoleName : null,
                x.RoleId,
                x.IsActive,
                x.EmailConfirmed,
                x.CreatedDate,
                x.LastLogin
            })
            .ToListAsync(cancellationToken);

        var users = rawUsers
            .Select(x => new UserAdminItemViewModel
            {
                UserId = x.UserId,
                UserName = x.UserName,
                Email = x.Email,
                FirstName = x.FirstName ?? string.Empty,
                LastName = x.LastName ?? string.Empty,
                FullName = string.Join(" ", new[] { x.LastName, x.FirstName }.Where(v => !string.IsNullOrWhiteSpace(v))),
                RoleName = string.IsNullOrWhiteSpace(x.RoleName) ? "Без роли" : x.RoleName,
                ApprovalSpecialization = x.ApprovalSpecialization ?? string.Empty,
                ApprovalSpecializationLabel = ApprovalSpecializations.GetLabel(x.ApprovalSpecialization),
                RoleId = x.RoleId,
                IsActive = x.IsActive,
                EmailConfirmed = x.EmailConfirmed,
                CreatedDateUtc = x.CreatedDate,
                LastLoginUtc = x.LastLogin
            })
            .ToList();

        return new UsersAdminPageViewModel
        {
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
            NewUser = createInput ?? new CreateUserAdminInputModel(),
            Roles = roles,
            ApprovalSpecializations = BuildApprovalSpecializationOptions(),
            Users = users
        };
    }

    private static IReadOnlyList<ApprovalSpecializationOptionViewModel> BuildApprovalSpecializationOptions()
    {
        return ApprovalSpecializations.All
            .Select(value => new ApprovalSpecializationOptionViewModel
            {
                Value = value,
                Label = ApprovalSpecializations.GetLabel(value)
            })
            .ToList();
    }

    private async Task<DocumentTemplatesAdminPageViewModel> BuildTemplatesPageAsync(
        CancellationToken cancellationToken,
        CreateDocumentTemplateAdminInputModel? createInput = null)
    {
        var templates = await _dbContext.Templates
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.TemplateId,
                x.Name,
                x.Content,
                x.Category,
                x.AiSuggestedFields,
                x.UsageCount,
                x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return new DocumentTemplatesAdminPageViewModel
        {
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
            NewTemplate = EnsureTemplateFieldRows(createInput ?? new CreateDocumentTemplateAdminInputModel()),
            Templates = templates.Select(template =>
            {
                var type = ParseDocumentType(template.Category);
                var fields = ParseTemplateFields(template.AiSuggestedFields);
                return new DocumentTemplateAdminItemViewModel
                {
                    Id = template.TemplateId,
                    Name = template.Name,
                    Description = template.Content ?? string.Empty,
                    DocumentType = type,
                    TypeLabel = GetDocumentTypeLabel(type),
                    UsageCount = template.UsageCount,
                    CreatedDateUtc = template.CreatedDate,
                    Fields = fields,
                    EditTemplate = new UpdateDocumentTemplateAdminInputModel
                    {
                        TemplateId = template.TemplateId,
                        Name = template.Name,
                        Description = template.Content,
                        DocumentType = type,
                        Fields = EnsureTemplateFieldRows(fields
                            .Select(field => new DocumentTemplateFieldAdminInputModel
                            {
                                Key = field.Key,
                                Label = field.Label,
                                Placeholder = field.Placeholder,
                                Required = field.Required,
                                InputType = field.InputType
                            })
                            .ToList(), 8)
                    }
                };
            }).ToList()
        };
    }

    private async Task<NomenclatureAdminPageViewModel> BuildNomenclaturePageAsync(
        CancellationToken cancellationToken,
        CreateNomenclatureCaseInputModel? caseInput = null,
        CreateNomenclatureRuleInputModel? ruleInput = null)
    {
        var cases = await _dbContext.NomenclatureCases
            .AsNoTracking()
            .OrderBy(x => x.Index)
            .Select(x => new NomenclatureCaseItemViewModel
            {
                Id = x.NomenclatureCaseId,
                Index = x.Index,
                Title = x.Title,
                RetentionPeriod = x.RetentionPeriod,
                LegalBasis = x.LegalBasis,
                Department = x.Department,
                IsActive = x.IsActive,
                DocumentsCount = x.Documents.Count
            })
            .ToListAsync(cancellationToken);

        var rules = await _dbContext.NomenclatureRules
            .AsNoTracking()
            .Include(x => x.NomenclatureCase)
            .OrderBy(x => x.NomenclatureCase!.Index)
            .ThenBy(x => x.DocumentType)
            .Select(x => new NomenclatureRuleItemViewModel
            {
                Id = x.NomenclatureRuleId,
                CaseLabel = x.NomenclatureCase == null ? "Не найдено" : $"{x.NomenclatureCase.Index} - {x.NomenclatureCase.Title}",
                DocumentType = string.IsNullOrWhiteSpace(x.DocumentType) ? "Любой тип" : x.DocumentType,
                Department = string.IsNullOrWhiteSpace(x.Department) ? "Любое подразделение" : x.Department,
                Note = x.Note,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return new NomenclatureAdminPageViewModel
        {
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
            NewCase = caseInput ?? new CreateNomenclatureCaseInputModel(),
            NewRule = ruleInput ?? new CreateNomenclatureRuleInputModel(),
            Cases = cases,
            Rules = rules
        };
    }

    private async Task<AuditAdminPageViewModel> BuildAuditPageAsync(
        string? activityType,
        int? documentId,
        CancellationToken cancellationToken)
    {
        var normalizedType = string.IsNullOrWhiteSpace(activityType)
            ? null
            : activityType.Trim();

        var query = _dbContext.DocumentActivity
            .AsNoTracking()
            .Include(x => x.Document)
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedType))
            query = query.Where(x => x.ActivityType == normalizedType);

        if (documentId.HasValue)
            query = query.Where(x => x.DocumentId == documentId.Value);

        var todayUtc = DateTime.UtcNow.Date;

        var totalCount = await query.CountAsync(cancellationToken);
        var todayCount = await query.CountAsync(x => (x.ActivityDate ?? DateTime.MinValue) >= todayUtc, cancellationToken);
        var distinctDocumentsCount = await query
            .Where(x => x.DocumentId != null)
            .Select(x => x.DocumentId)
            .Distinct()
            .CountAsync(cancellationToken);
        var systemEventsCount = await query.CountAsync(x => x.DocumentId == null, cancellationToken);

        var rawEntries = await query
            .OrderByDescending(x => x.ActivityDate ?? DateTime.MinValue)
            .ThenByDescending(x => x.ActivityId)
            .Take(200)
            .Select(x => new
            {
                x.ActivityId,
                x.ActivityDate,
                x.ActivityType,
                x.DocumentId,
                DocumentTitle = x.Document != null ? x.Document.Title : null,
                UserFirstName = x.User != null ? x.User.FirstName : null,
                UserLastName = x.User != null ? x.User.LastName : null,
                UserName = x.User != null ? x.User.UserName : null,
                x.Details
            })
            .ToListAsync(cancellationToken);

        var entries = rawEntries
            .Select(x => new AuditEntryItemViewModel
            {
                Id = x.ActivityId,
                ActivityDateUtc = x.ActivityDate,
                ActivityType = x.ActivityType ?? string.Empty,
                ActivityTypeLabel = AuditActivityTypes.GetDisplayName(x.ActivityType),
                DocumentId = x.DocumentId,
                DocumentTitle = x.DocumentId == null
                    ? "Системное событие"
                    : (string.IsNullOrWhiteSpace(x.DocumentTitle) ? $"Документ #{x.DocumentId}" : x.DocumentTitle),
                UserDisplayName = BuildAuditUserDisplayName(x.UserLastName, x.UserFirstName, x.UserName),
                Details = x.Details ?? string.Empty
            })
            .ToList();

        return new AuditAdminPageViewModel
        {
            SelectedActivityType = normalizedType,
            SelectedDocumentId = documentId,
            TotalCount = totalCount,
            TodayCount = todayCount,
            DistinctDocumentsCount = distinctDocumentsCount,
            SystemEventsCount = systemEventsCount,
            ActivityTypes = AuditActivityTypes.All
                .Select(x => new AuditActivityTypeOptionViewModel
                {
                    Value = x,
                    Label = AuditActivityTypes.GetDisplayName(x)
                })
                .ToList(),
            Entries = entries
        };
    }

    private static string BuildAuditUserDisplayName(string? lastName, string? firstName, string? userName)
    {
        var fullName = string.Join(" ", new[] { lastName, firstName }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        return "Системное действие";
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
        return int.TryParse(raw, out var userId) ? userId : null;
    }

    private bool ReadPostedBoolean(string key, bool fallback)
    {
        if (!Request.HasFormContentType)
            return fallback;

        if (!Request.Form.TryGetValue(key, out var values))
            return fallback;

        return values.Any(value =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
    }

    private string ReadPostedString(string key, string fallback)
    {
        if (!Request.HasFormContentType)
            return fallback;

        if (!Request.Form.TryGetValue(key, out var values))
            return fallback;

        return values.LastOrDefault() ?? fallback;
    }

    private void ValidateTemplateInput(
        string? name,
        DocumentType? documentType,
        IReadOnlyCollection<DocumentTemplateFieldAdminInputModel>? fields)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError("NewTemplate.Name", "Введите название шаблона.");

        if (documentType is null)
            ModelState.AddModelError("NewTemplate.DocumentType", "Выберите тип документа.");

        var normalizedFields = NormalizeTemplateFields(fields);
        if (normalizedFields.Count == 0)
            ModelState.AddModelError("NewTemplate.Fields", "Добавьте хотя бы одно поле шаблона.");
    }

    private static string SerializeTemplateFields(IReadOnlyCollection<DocumentTemplateFieldAdminInputModel>? fields)
    {
        var normalizedFields = NormalizeTemplateFields(fields)
            .Select(field => new DocumentTemplateFieldViewModel
            {
                Key = field.Key,
                Label = field.Label,
                Placeholder = field.Placeholder,
                Required = field.Required,
                InputType = field.InputType
            })
            .ToList();

        return JsonSerializer.Serialize(normalizedFields);
    }

    private static List<DocumentTemplateFieldAdminInputModel> NormalizeTemplateFields(IReadOnlyCollection<DocumentTemplateFieldAdminInputModel>? fields)
    {
        if (fields is null)
            return [];

        return fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key) || !string.IsNullOrWhiteSpace(field.Label))
            .Select(field => new DocumentTemplateFieldAdminInputModel
            {
                Key = NormalizeTemplateFieldKey(field.Key),
                Label = field.Label.Trim(),
                Placeholder = string.IsNullOrWhiteSpace(field.Placeholder) ? string.Empty : field.Placeholder.Trim(),
                Required = field.Required,
                InputType = NormalizeInputType(field.InputType)
            })
            .Where(field => !string.IsNullOrWhiteSpace(field.Key) && !string.IsNullOrWhiteSpace(field.Label))
            .ToList();
    }

    private static CreateDocumentTemplateAdminInputModel EnsureTemplateFieldRows(CreateDocumentTemplateAdminInputModel input)
    {
        input.Fields = EnsureTemplateFieldRows(input.Fields, 4);
        return input;
    }

    private static List<DocumentTemplateFieldAdminInputModel> EnsureTemplateFieldRows(List<DocumentTemplateFieldAdminInputModel>? fields, int minimumRows)
    {
        var rows = fields ?? [];
        while (rows.Count < minimumRows)
            rows.Add(new DocumentTemplateFieldAdminInputModel());

        return rows;
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
                .Where(field => !string.IsNullOrWhiteSpace(field.Key) && !string.IsNullOrWhiteSpace(field.Label))
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static DocumentType ParseDocumentType(string? category)
    {
        return Enum.TryParse<DocumentType>(category, true, out var parsed)
            ? parsed
            : DocumentType.Other;
    }

    private static string NormalizeTemplateFieldKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return key.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }

    private static string NormalizeInputType(string? inputType)
    {
        return (inputType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "textarea" => "textarea",
            "date" => "date",
            "number" => "number",
            _ => "text"
        };
    }

    private static string GetDocumentTypeLabel(DocumentType type) => type switch
    {
        DocumentType.Contract => "???????",
        DocumentType.Invoice => "????",
        DocumentType.Report => "?????",
        DocumentType.Order => "??????",
        DocumentType.Application => "?????????",
        DocumentType.Act => "???",
        _ => "??????"
    };
}
