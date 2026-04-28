using System.ComponentModel.DataAnnotations;
using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public class CreateDocumentPageViewModel
{
    public int? TemplateId { get; set; }

    [StringLength(200, ErrorMessage = "Название документа не должно превышать 200 символов.")]
    public string Title { get; set; } = string.Empty;

    public DocumentType? Type { get; set; }

    public string Description { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    [Range(1, 3, ErrorMessage = "Приоритет должен быть от 1 до 3.")]
    public int? Priority { get; set; } = 2;

    public string? Tags { get; set; }

    public IFormFile? File { get; set; }
    public int? RouteTemplateId { get; set; }

    public string? SelectedTemplateName { get; set; }
    public string? SelectedTemplateDescription { get; set; }
    public string? SelectedTemplateTypeLabel { get; set; }
    public IReadOnlyList<DocumentTemplateViewModel> Templates { get; set; } = [];
    public IReadOnlyList<DocumentTemplateFieldViewModel> SelectedTemplateFields { get; set; } = [];
    public Dictionary<string, string> TemplateFieldValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<RouteTemplateOptionViewModel> RouteTemplateOptions { get; set; } = [];
}
