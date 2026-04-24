using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Entities;

public class NomenclatureCase
{
    [Key]
    public int NomenclatureCaseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Index { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string RetentionPeriod { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? LegalBasis { get; set; }

    [MaxLength(150)]
    public string? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<NomenclatureRule> Rules { get; set; } = new List<NomenclatureRule>();
}
