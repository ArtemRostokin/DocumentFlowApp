using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Web.Models;

public sealed class NomenclatureAdminPageViewModel
{
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public CreateNomenclatureCaseInputModel NewCase { get; init; } = new();
    public CreateNomenclatureRuleInputModel NewRule { get; init; } = new();
    public IReadOnlyList<NomenclatureCaseItemViewModel> Cases { get; init; } = [];
    public IReadOnlyList<NomenclatureRuleItemViewModel> Rules { get; init; } = [];
}

public sealed class CreateNomenclatureCaseInputModel
{
    [Required(ErrorMessage = "Введите индекс дела.")]
    [StringLength(50)]
    public string Index { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите название дела.")]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите срок хранения.")]
    [StringLength(100)]
    public string RetentionPeriod { get; set; } = string.Empty;

    [StringLength(300)]
    public string? LegalBasis { get; set; }

    [StringLength(150)]
    public string? Department { get; set; }
}

public sealed class CreateNomenclatureRuleInputModel
{
    [Required(ErrorMessage = "Выберите дело номенклатуры.")]
    public int? NomenclatureCaseId { get; set; }

    [StringLength(100)]
    public string? DocumentType { get; set; }

    [StringLength(150)]
    public string? Department { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

public sealed class NomenclatureCaseItemViewModel
{
    public int Id { get; init; }
    public string Index { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string RetentionPeriod { get; init; } = string.Empty;
    public string? LegalBasis { get; init; }
    public string? Department { get; init; }
    public bool IsActive { get; init; }
    public int DocumentsCount { get; init; }
}

public sealed class NomenclatureRuleItemViewModel
{
    public int Id { get; init; }
    public string CaseLabel { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string? Note { get; init; }
    public bool IsActive { get; init; }
}

public sealed class NomenclatureCaseOptionViewModel
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
}
