using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class NomenclatureRule
{
    [Key]
    public int NomenclatureRuleId { get; set; }

    public int NomenclatureCaseId { get; set; }
    public NomenclatureCase? NomenclatureCase { get; set; }

    [MaxLength(100)]
    public string? DocumentType { get; set; }

    [MaxLength(150)]
    public string? Department { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;
}
