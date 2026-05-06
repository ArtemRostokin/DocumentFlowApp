using System.ComponentModel.DataAnnotations;
using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class DocumentTemplatesAdminPageViewModel
{
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<DocumentTemplateAdminItemViewModel> Templates { get; init; } = [];
    public CreateDocumentTemplateAdminInputModel NewTemplate { get; init; } = new();
}

public sealed class DocumentTemplateAdminItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DocumentType DocumentType { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public DateTime? CreatedDateUtc { get; init; }
    public IReadOnlyList<DocumentTemplateFieldViewModel> Fields { get; init; } = [];
    public UpdateDocumentTemplateAdminInputModel EditTemplate { get; init; } = new();
}

public sealed class CreateDocumentTemplateAdminInputModel
{
    [Required(ErrorMessage = "Введите название шаблона.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите тип документа.")]
    public DocumentType? DocumentType { get; set; }

    public string? Description { get; set; }
    public List<DocumentTemplateFieldAdminInputModel> Fields { get; set; } = CreateEmptyFieldRows();

    public static List<DocumentTemplateFieldAdminInputModel> CreateEmptyFieldRows(int count = 4)
    {
        var rows = new List<DocumentTemplateFieldAdminInputModel>(count);
        for (var index = 0; index < count; index++)
            rows.Add(new DocumentTemplateFieldAdminInputModel());

        return rows;
    }
}

public sealed class UpdateDocumentTemplateAdminInputModel
{
    public int TemplateId { get; set; }

    [Required(ErrorMessage = "Введите название шаблона.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите тип документа.")]
    public DocumentType? DocumentType { get; set; }

    public string? Description { get; set; }
    public List<DocumentTemplateFieldAdminInputModel> Fields { get; set; } = [];
}

public sealed class DocumentTemplateFieldAdminInputModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string InputType { get; set; } = "text";
}
