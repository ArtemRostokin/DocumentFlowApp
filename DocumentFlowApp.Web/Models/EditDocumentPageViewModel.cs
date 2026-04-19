using System.ComponentModel.DataAnnotations;
using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class EditDocumentPageViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите название документа.")]
    [StringLength(200, ErrorMessage = "Название документа не должно превышать 200 символов.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите тип документа.")]
    public DocumentType? Type { get; set; }

    [Required(ErrorMessage = "Введите описание документа.")]
    public string Description { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    [Range(1, 3, ErrorMessage = "Приоритет должен быть от 1 до 3.")]
    public int? Priority { get; set; }

    public string? Tags { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? FileUrl { get; set; }
    public string FileKind { get; set; } = "none";

    public IReadOnlyList<AiSuggestionViewModel> AiSuggestions { get; set; } = [];
}

