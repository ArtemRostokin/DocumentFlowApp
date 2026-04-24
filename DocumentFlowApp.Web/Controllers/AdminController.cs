using DocumentFlowApp.Core.Audit;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Web.Models;
using DocumentFlowApp.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext dbContext, IAuditService auditService, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Routes()
    {
        ViewData["SectionTitle"] = "Шаблоны маршрутов";
        ViewData["SectionDescription"] = "Раздел подготовлен под управление маршрутами согласования и шагами маршрута.";
        return View("Section");
    }

    [HttpGet]
    public async Task<IActionResult> Nomenclature(CancellationToken cancellationToken)
    {
        return View(await BuildNomenclaturePageAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNomenclatureCase(CreateNomenclatureCaseInputModel input, CancellationToken cancellationToken)
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
    public async Task<IActionResult> CreateNomenclatureRule(CreateNomenclatureRuleInputModel input, CancellationToken cancellationToken)
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
        var todayCount = await query.CountAsync(
            x => (x.ActivityDate ?? DateTime.MinValue) >= todayUtc,
            cancellationToken);
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
                    : (string.IsNullOrWhiteSpace(x.DocumentTitle)
                        ? $"Документ #{x.DocumentId}"
                        : x.DocumentTitle),
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
        var raw = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;

        return int.TryParse(raw, out var userId) ? userId : null;
    }
}
