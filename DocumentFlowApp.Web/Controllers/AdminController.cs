using DocumentFlowApp.Core.Entities;
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
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext dbContext, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
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

    public IActionResult Audit()
    {
        ViewData["SectionTitle"] = "Журнал аудита";
        ViewData["SectionDescription"] = "Раздел подготовлен под просмотр аудита действий пользователей и документов.";
        return View("Section");
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
}
