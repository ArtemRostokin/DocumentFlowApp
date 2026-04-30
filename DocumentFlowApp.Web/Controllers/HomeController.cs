using System.Diagnostics;
using System.Security.Claims;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentFlowApp.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IDocumentService documentService, ILogger<HomeController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, DocumentType? type)
    {
        var normalizedRole = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (normalizedRole == AppRoles.Employee && currentUserId is null)
            return RedirectToAction("Login", "Account");

        var model = await BuildPageModelAsync(q, type, normalizedRole, currentUserId);
        return View("Kanban", model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private string GetCurrentUserRole()
    {
        var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == "df_role")?.Value
            ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        return AppRoles.Normalize(currentUserRole) ?? AppRoles.Employee;
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return int.TryParse(raw, out var userId) ? userId : null;
    }

    private static IReadOnlyList<KanbanColumnViewModel> BuildColumns(IEnumerable<Document> documents, bool isEmployeeBoard)
    {
        var all = documents.ToList();

        if (isEmployeeBoard)
        {
            return
            [
                CreateColumn(DocumentStatus.OnApproval, "На согласовании", "accent-primary", FilterByStatus(all, DocumentStatus.OnApproval)),
                CreateColumn(DocumentStatus.Approved, "К исполнению", "accent-primary", FilterByStatus(all, DocumentStatus.Approved)),
                CreateColumn(DocumentStatus.InWork, "В работе", "accent-success", FilterByStatus(all, DocumentStatus.InWork)),
                CreateColumn(DocumentStatus.Completed, "Завершено", "accent-muted", FilterByStatus(all, DocumentStatus.Completed), isEmptyPlaceholder: true)
            ];
        }

        return
        [
            CreateColumn(DocumentStatus.Draft, "Черновик", "accent-neutral", FilterByStatus(all, DocumentStatus.Draft)),
            CreateColumn(DocumentStatus.OnApproval, "На согласовании", "accent-primary", FilterByStatus(all, DocumentStatus.OnApproval)),
            CreateColumn(DocumentStatus.Approved, "Утвержден", "accent-success", FilterByStatus(all, DocumentStatus.Approved)),
            CreateColumn(DocumentStatus.InWork, "В работе", "accent-primary", FilterByStatus(all, DocumentStatus.InWork)),
            CreateColumn(DocumentStatus.Completed, "Завершен", "accent-success", FilterByStatus(all, DocumentStatus.Completed)),
            CreateColumn(DocumentStatus.Archived, "Архив", "accent-muted", FilterByStatus(all, DocumentStatus.Archived), isEmptyPlaceholder: true)
        ];
    }

    private static IEnumerable<Document> FilterByStatus(IEnumerable<Document> documents, DocumentStatus status)
    {
        return documents.Where(d => ParseDocumentStatus(d.Status) == status);
    }

    private static KanbanColumnViewModel CreateColumn(
        DocumentStatus status,
        string title,
        string accentClass,
        IEnumerable<Document> documents,
        bool isEmptyPlaceholder = false)
    {
        var cards = documents.Select(ToCard).ToList();

        return new KanbanColumnViewModel
        {
            Title = title,
            AccentClass = accentClass,
            Status = status,
            Documents = cards,
            IsEmptyPlaceholder = isEmptyPlaceholder && cards.Count == 0
        };
    }

    private static KanbanCardViewModel ToCard(Document document)
    {
        var author = "Система";
        if (document.User != null)
        {
            if (!string.IsNullOrWhiteSpace(document.User.FirstName) || !string.IsNullOrWhiteSpace(document.User.LastName))
                author = $"{document.User.FirstName} {document.User.LastName}".Trim();
            else
                author = document.User.UserName ?? "Система";
        }

        var docType = ParseDocumentType(document.DocumentType);
        var docStatus = ParseDocumentStatus(document.Status);

        return new KanbanCardViewModel
        {
            Id = document.DocumentId,
            Title = string.IsNullOrWhiteSpace(document.Title) ? "Без названия" : document.Title,
            Subtitle = string.IsNullOrWhiteSpace(document.ExtractedText) ? "Описание не заполнено" : document.ExtractedText,
            Author = string.IsNullOrWhiteSpace(author) ? "Система" : author,
            TypeLabel = GetTypeLabel(docType),
            TypeClass = GetTypeClass(docType),
            StatusLabel = GetStatusLabel(docStatus),
            StatusClass = GetStatusClass(docStatus),
            StatusIcon = GetStatusIcon(docStatus),
            Progress = GetProgress(docStatus),
            ProgressLabel = GetProgressLabel(docStatus),
            StageHint = GetStageHint(docStatus),
            DueLabel = FormatDueLabel(document),
            DueClass = GetDueClass(docStatus)
        };
    }

    private static DocumentType ParseDocumentType(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return DocumentType.Other;

        if (Enum.TryParse<DocumentType>(stored, true, out var parsed))
            return parsed;

        if (int.TryParse(stored, out var intVal) && Enum.IsDefined(typeof(DocumentType), intVal))
            return (DocumentType)intVal;

        return DocumentType.Other;
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
            return (DocumentStatus)intVal;

        return DocumentStatus.Draft;
    }

    private static string GetTypeLabel(DocumentType type) => type switch
    {
        DocumentType.Contract => "Договор",
        DocumentType.Invoice => "Счет",
        DocumentType.Report => "Отчет",
        DocumentType.Order => "Приказ",
        DocumentType.Application => "Заявление",
        DocumentType.Act => "Акт",
        _ => "Документ"
    };

    private static string GetTypeClass(DocumentType type) => type switch
    {
        DocumentType.Contract => "type-contract",
        DocumentType.Invoice => "type-invoice",
        DocumentType.Report => "type-report",
        DocumentType.Order => "type-order",
        DocumentType.Application => "type-application",
        DocumentType.Act => "type-report",
        _ => "type-other"
    };

    private static string GetStatusLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Черновик",
        DocumentStatus.OnApproval => "На согласовании",
        DocumentStatus.Approved => "Утвержден",
        DocumentStatus.InWork => "В работе",
        DocumentStatus.Completed => "Завершен",
        DocumentStatus.Archived => "Архив",
        _ => "Неизвестно"
    };

    private static string GetStatusClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "status-neutral",
        DocumentStatus.OnApproval => "status-primary",
        DocumentStatus.Approved => "status-success",
        DocumentStatus.InWork => "status-primary",
        DocumentStatus.Completed => "status-success",
        DocumentStatus.Archived => "status-muted",
        _ => "status-neutral"
    };

    private static string GetStatusIcon(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "schedule",
        DocumentStatus.OnApproval => "autorenew",
        DocumentStatus.Approved => "verified",
        DocumentStatus.InWork => "construction",
        DocumentStatus.Completed => "check_circle",
        DocumentStatus.Archived => "inventory_2",
        _ => "description"
    };

    private static int GetProgress(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => 0,
        DocumentStatus.OnApproval => 35,
        DocumentStatus.Approved => 60,
        DocumentStatus.InWork => 80,
        DocumentStatus.Completed => 100,
        DocumentStatus.Archived => 100,
        _ => 0
    };

    private static string GetProgressLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Подготовка",
        DocumentStatus.OnApproval => "Согласование",
        DocumentStatus.Approved => "Готов к исполнению",
        DocumentStatus.InWork => "Исполнение",
        DocumentStatus.Completed => "Завершено",
        DocumentStatus.Archived => "Архивировано",
        _ => "Статус"
    };

    private static string GetStageHint(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Проверьте карточку и подготовьте маршрут согласования.",
        DocumentStatus.OnApproval => "Документ ожидает решение согласующего.",
        DocumentStatus.Approved => "Документ утвержден и готов к началу работы.",
        DocumentStatus.InWork => "Исполнитель ведет работу и фиксирует результат.",
        DocumentStatus.Completed => "Результат готов к проверке и архивированию.",
        DocumentStatus.Archived => "Жизненный цикл завершен, документ хранится в архиве.",
        _ => "Следующее действие зависит от статуса документа."
    };

    private static string GetDueClass(DocumentStatus status) => status switch
    {
        DocumentStatus.OnApproval => "due-danger",
        DocumentStatus.Completed => "due-success",
        _ => "due-neutral"
    };

    private static string FormatDueLabel(Document document)
    {
        if (document.DueDate.HasValue)
            return document.DueDate.Value.ToLocalTime().ToString("dd.MM");

        if (document.UpdatedDate.HasValue)
            return document.UpdatedDate.Value.ToLocalTime().ToString("dd.MM");

        return document.CreatedDate.ToLocalTime().ToString("dd.MM");
    }

    private static List<Document> GetFallbackDocuments()
    {
        var now = DateTime.UtcNow;

        return
        [
            new Document
            {
                DocumentId = 1,
                Title = "Договор оказания услуг №45-А",
                ExtractedText = "ООО ТехПром",
                User = new User { FirstName = "Петров", LastName = "В.", UserName = "petrov" },
                UserId = 1,
                DocumentType = DocumentType.Contract.ToString(),
                Status = DocumentStatus.Draft.ToString(),
                CreatedDate = now.AddDays(-2)
            },
            new Document
            {
                DocumentId = 2,
                Title = "Счет на оплату лицензий ПО",
                ExtractedText = "ЗАО СофтЛайн",
                User = new User { FirstName = "Сидоров", LastName = "И.", UserName = "sidorov" },
                UserId = 1,
                DocumentType = DocumentType.Invoice.ToString(),
                Status = DocumentStatus.OnApproval.ToString(),
                CreatedDate = now.AddDays(-1)
            },
            new Document
            {
                DocumentId = 3,
                Title = "Акт выполненных работ за сентябрь",
                ExtractedText = "ИП Смирнов",
                User = new User { FirstName = "Иванов", LastName = "А.", UserName = "ivanov" },
                UserId = 1,
                DocumentType = DocumentType.Act.ToString(),
                Status = DocumentStatus.Approved.ToString(),
                CreatedDate = now.AddDays(-5),
                UpdatedDate = now.AddDays(-1)
            },
            new Document
            {
                DocumentId = 4,
                Title = "План внедрения",
                ExtractedText = "Исполнение задач",
                User = new User { FirstName = "Смирнов", LastName = "К.", UserName = "smirnov" },
                UserId = 1,
                DocumentType = DocumentType.Order.ToString(),
                Status = DocumentStatus.InWork.ToString(),
                CreatedDate = now.AddDays(-7),
                UpdatedDate = now.AddDays(-2)
            },
            new Document
            {
                DocumentId = 5,
                Title = "Итоговый отчет по проекту",
                ExtractedText = "Выполнено в срок",
                User = new User { FirstName = "Андреев", LastName = "М.", UserName = "andreev" },
                UserId = 1,
                DocumentType = DocumentType.Report.ToString(),
                Status = DocumentStatus.Completed.ToString(),
                CreatedDate = now.AddDays(-10),
                UpdatedDate = now.AddDays(-2)
            },
            new Document
            {
                DocumentId = 6,
                Title = "Архивный договор",
                ExtractedText = "Архивная запись",
                User = new User { FirstName = "Кузнецов", LastName = "С.", UserName = "kuznetsov" },
                UserId = 2,
                DocumentType = DocumentType.Contract.ToString(),
                Status = DocumentStatus.Archived.ToString(),
                CreatedDate = now.AddDays(-20),
                UpdatedDate = now.AddDays(-15)
            }
        ];
    }

    private async Task<KanbanBoardPageViewModel> BuildPageModelAsync(
        string? q,
        DocumentType? type,
        string normalizedRole,
        int? currentUserId)
    {
        var isEmployeeBoard = normalizedRole == AppRoles.Employee;
        var documents = new List<Document>();
        string? notice = null;

        try
        {
            documents = await _documentService.GetAllDocumentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load documents from backend. Using fallback preview data.");
            documents = GetFallbackDocuments();
            notice = "Сервер временно недоступен, поэтому показаны демонстрационные данные.";
        }

        if (isEmployeeBoard && currentUserId.HasValue)
        {
            documents = documents
                .Where(d => d.UserId == currentUserId.Value)
                .Where(d => ParseDocumentStatus(d.Status) is DocumentStatus.OnApproval or DocumentStatus.Approved or DocumentStatus.InWork or DocumentStatus.Completed)
                .ToList();
        }

        if (type.HasValue)
        {
            documents = documents
                .Where(d => string.Equals(d.DocumentType, type.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();
            documents = documents
                .Where(d =>
                    (!string.IsNullOrWhiteSpace(d.Title) && d.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(d.ExtractedText) && d.ExtractedText.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (d.User != null &&
                     ((!string.IsNullOrWhiteSpace(d.User.FirstName) && d.User.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                      (!string.IsNullOrWhiteSpace(d.User.LastName) && d.User.LastName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                      (!string.IsNullOrWhiteSpace(d.User.UserName) && d.User.UserName.Contains(query, StringComparison.OrdinalIgnoreCase))))
                )
                .ToList();
        }

        return new KanbanBoardPageViewModel
        {
            Title = isEmployeeBoard ? "Моя работа" : "Документооборот",
            PeriodLabel = DateTime.Now.ToString("MMMM yyyy"),
            SearchQuery = q,
            SelectedType = type,
            TotalDocuments = documents.Count,
            TotalLabel = isEmployeeBoard ? "Всего задач" : "Всего документов",
            Notice = notice,
            SuccessMessage = TempData["SuccessMessage"] as string,
            ViewModeLabel = isEmployeeBoard ? "Рабочая доска исполнителя" : "Управленческая доска",
            ViewModeDescription = isEmployeeBoard
                ? "Здесь видны только документы, по которым вы должны согласовать, начать или завершить работу."
                : "Здесь менеджер и администратор контролируют движение документов по этапам жизненного цикла.",
            ApprovalCount = documents.Count(d => ParseDocumentStatus(d.Status) == DocumentStatus.OnApproval),
            InWorkCount = documents.Count(d => ParseDocumentStatus(d.Status) == DocumentStatus.InWork),
            CompletedCount = documents.Count(d => ParseDocumentStatus(d.Status) == DocumentStatus.Completed),
            Columns = BuildColumns(documents, isEmployeeBoard)
        };
    }
}
