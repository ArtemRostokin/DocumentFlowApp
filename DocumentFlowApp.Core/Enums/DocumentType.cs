using System.ComponentModel.DataAnnotations;

namespace DocumentFlowApp.Core.Enums
{
    // Типы документов для классификации и фильтрации
    public enum DocumentType
    {
        [Display(Name = "Договор")]
        Contract = 1,

        [Display(Name = "Счет")]
        Invoice = 2,

        [Display(Name = "Отчет")]
        Report = 3,

        [Display(Name = "Приказ")]
        Order = 4,

        [Display(Name = "Заявление")]
        Application = 5,

        [Display(Name = "Акт")]
        Act = 6,

        [Display(Name = "Прочее")]
        Other = 7
    }
}