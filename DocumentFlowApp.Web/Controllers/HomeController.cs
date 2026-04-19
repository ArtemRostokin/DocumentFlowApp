using System.Diagnostics;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
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
        var model = await BuildPageModelAsync(q, type);
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

    private static IReadOnlyList<KanbanColumnViewModel> BuildColumns(IEnumerable<Document> documents)
    {
        var all = documents.ToList();

        return
        [
            CreateColumn(DocumentStatus.Draft, "Новые", "accent-neutral", all.Where(d => string.Equals(d.Status, DocumentStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))),
            CreateColumn(DocumentStatus.InProgress, "В работе", "accent-primary", all.Where(d => string.Equals(d.Status, DocumentStatus.InProgress.ToString(), StringComparison.OrdinalIgnoreCase))),
            CreateColumn(DocumentStatus.Approved, "Утверждены", "accent-success", all.Where(d => string.Equals(d.Status, DocumentStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase))),
            CreateColumn(DocumentStatus.Rejected, "Отклонены", "accent-danger", all.Where(d => string.Equals(d.Status, DocumentStatus.Rejected.ToString(), StringComparison.OrdinalIgnoreCase))),
            CreateColumn(DocumentStatus.Archived, "Архив", "accent-muted", all.Where(d => string.Equals(d.Status, DocumentStatus.Archived.ToString(), StringComparison.OrdinalIgnoreCase)), isEmptyPlaceholder: true)
        ];
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
        var author = "System";
        if (document.User != null)
        {
            if (!string.IsNullOrWhiteSpace(document.User.FirstName) || !string.IsNullOrWhiteSpace(document.User.LastName))
                author = $"{document.User.FirstName} {document.User.LastName}".Trim();
            else
                author = document.User.UserName ?? "System";
        }

        var docType = ParseDocumentType(document.DocumentType);
        var docStatus = ParseDocumentStatus(document.Status);

        return new KanbanCardViewModel
        {
            Id = document.DocumentId,
            Title = string.IsNullOrWhiteSpace(document.Title) ? "Без названия" : document.Title,
            Subtitle = string.IsNullOrWhiteSpace(document.ExtractedText) ? "Без описания" : document.ExtractedText,
            Author = string.IsNullOrWhiteSpace(author) ? "System" : author,
            TypeLabel = GetTypeLabel(docType),
            TypeClass = GetTypeClass(docType),
            StatusLabel = GetStatusLabel(docStatus),
            StatusClass = GetStatusClass(docStatus),
            StatusIcon = GetStatusIcon(docStatus),
            Progress = GetProgress(docStatus),
            ProgressLabel = GetProgressLabel(docStatus),
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

        // Попробуем по числовому представлению
        if (int.TryParse(stored, out var intVal) && Enum.IsDefined(typeof(DocumentType), intVal))
            return (DocumentType)intVal;

        return DocumentType.Other;
    }

    private static DocumentStatus ParseDocumentStatus(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
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
        _ => "Документ"
    };

    private static string GetTypeClass(DocumentType type) => type switch
    {
        DocumentType.Contract => "type-contract",
        DocumentType.Invoice => "type-invoice",
        DocumentType.Report => "type-report",
        DocumentType.Order => "type-order",
        DocumentType.Application => "type-application",
        _ => "type-other"
    };

    private static string GetStatusLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Ожидает старта",
        DocumentStatus.InProgress => "Исполняется",
        DocumentStatus.Approved => "Готово",
        DocumentStatus.Rejected => "Требует внимания",
        DocumentStatus.Archived => "Завершено",
        _ => "Неизвестно"
    };

    private static string GetStatusClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "status-neutral",
        DocumentStatus.InProgress => "status-primary",
        DocumentStatus.Approved => "status-success",
        DocumentStatus.Rejected => "status-danger",
        DocumentStatus.Archived => "status-muted",
        _ => "status-neutral"
    };

    private static string GetStatusIcon(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "schedule",
        DocumentStatus.InProgress => "autorenew",
        DocumentStatus.Approved => "verified",
        DocumentStatus.Rejected => "warning",
        DocumentStatus.Archived => "inventory_2",
        _ => "description"
    };

    private static int GetProgress(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => 0,
        DocumentStatus.InProgress => 65,
        DocumentStatus.Approved => 100,
        DocumentStatus.Rejected => 100,
        DocumentStatus.Archived => 100,
        _ => 0
    };

    private static string GetProgressLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "Ожидает AI",
        DocumentStatus.InProgress => "Обработка",
        DocumentStatus.Approved => "Согласовано",
        DocumentStatus.Rejected => "Проверить",
        DocumentStatus.Archived => "Архивировано",
        _ => "Статус"
    };

    private static string GetDueClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Rejected => "due-danger",
        DocumentStatus.Approved => "due-success",
        _ => "due-neutral"
    };

    private static string FormatDueLabel(Document document)
    {
        if (document.UpdatedDate.HasValue)
        {
            return document.UpdatedDate.Value.ToString("dd.MM");
        }

        return document.CreatedDate.ToString("dd.MM");
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
                ExtractedText = "ООО \"ТехПром\"",
                User = new User { FirstName = "Петров", LastName = "В.", UserName = "petrov" },
                DocumentType = DocumentType.Contract.ToString(),
                Status = DocumentStatus.Draft.ToString(),
                CreatedDate = now.AddDays(-2)
            },
            new Document
            {
                DocumentId = 2,
                Title = "Счет на оплату лицензий ПО",
                ExtractedText = "ЗАО \"СофтЛайн\"",
                User = new User { FirstName = "Сидоров", LastName = "И.", UserName = "sidorov" },
                DocumentType = DocumentType.Invoice.ToString(),
                Status = DocumentStatus.Draft.ToString(),
                CreatedDate = now.AddDays(-1)
            },
            new Document
            {
                DocumentId = 3,
                Title = "Акт выполненных работ за сентябрь",
                ExtractedText = "ИП Смирнов",
                User = new User { FirstName = "Иванов", LastName = "А.", UserName = "ivanov" },
                DocumentType = DocumentType.Report.ToString(),
                Status = DocumentStatus.InProgress.ToString(),
                CreatedDate = now.AddDays(-5),
                UpdatedDate = now.AddDays(-1)
            },
            new Document
            {
                DocumentId = 4,
                Title = "Доп. соглашение к договору №12",
                ExtractedText = "ПАО \"Альфа\"",
                User = new User { FirstName = "Кузнецов", LastName = "С.", UserName = "kuznetsov" },
                DocumentType = DocumentType.Contract.ToString(),
                Status = DocumentStatus.Approved.ToString(),
                CreatedDate = now.AddDays(-7),
                UpdatedDate = now.AddDays(-2)
            }
        ];
    }

    private async Task<KanbanBoardPageViewModel> BuildPageModelAsync(
        string? q,
        DocumentType? type)
    {
        var documents = new List<Document>();
        string? notice = null;

        try
        {
            documents = await _documentService.GetAllDocumentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить документы из backend. Показываем временные данные.");
            documents = GetFallbackDocuments();
            notice = "Backend недоступен, поэтому сейчас показаны временные данные макета.";
        }

        if (type.HasValue)
        {
            documents = documents.Where(d => string.Equals(d.DocumentType, type.Value.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();
            documents = documents
                .Where(d =>
                    (!string.IsNullOrWhiteSpace(d.Title) && d.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(d.ExtractedText) && d.ExtractedText.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (d.User != null && (
                        (!string.IsNullOrWhiteSpace(d.User.FirstName) && d.User.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(d.User.LastName) && d.User.LastName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(d.User.UserName) && d.User.UserName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    ))
                )
                .ToList();
        }

        return new KanbanBoardPageViewModel
        {
            PeriodLabel = DateTime.Now.ToString("MMMM yyyy"),
            SearchQuery = q,
            SelectedType = type,
            TotalDocuments = documents.Count,
            Notice = notice,
            SuccessMessage = TempData["SuccessMessage"] as string,
            Columns = BuildColumns(documents)
        };
    }
}
