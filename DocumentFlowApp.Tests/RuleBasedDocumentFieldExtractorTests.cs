using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Infrastructure.Services;

namespace DocumentFlowApp.Tests;

public class RuleBasedDocumentFieldExtractorTests
{
    private readonly RuleBasedDocumentFieldExtractor _extractor = new();

    [Fact]
    public void Extract_Contract_Returns_TemplateAligned_Fields()
    {
        const string text = """
            Договор № 42/2026
            Дата договора: 28.04.2026
            Контрагент: ООО Ромашка
            Сумма договора: 150000,00 руб.
            Предмет договора: Оказание услуг по сопровождению системы.
            """;

        var result = _extractor.Extract(DocumentType.Contract, text, "doc1.docx");

        Assert.Equal(DocumentType.Contract, result.DocumentType);
        Assert.Contains(result.Fields, x => x.FieldKey == "contract_number" && x.SuggestedValue == "42/2026");
        Assert.Contains(result.Fields, x => x.FieldKey == "contract_date" && x.SuggestedValue == "2026-04-28");
        Assert.Contains(result.Fields, x => x.FieldKey == "counterparty" && x.SuggestedValue == "ООО Ромашка");
        Assert.Contains(result.Fields, x => x.FieldKey == "amount" && x.SuggestedValue == "150000");
        Assert.Contains(result.Fields, x => x.FieldKey == "subject" && x.SuggestedValue.Contains("Оказание услуг", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_Invoice_Returns_TemplateAligned_Fields()
    {
        const string text = """
            Счет № СЧ-2026-015
            Дата счета: 30.04.2026
            Поставщик: ООО Поставщик
            К оплате: 98000 руб.
            Срок оплаты: 10.05.2026
            """;

        var result = _extractor.Extract(DocumentType.Invoice, text, "invoice.pdf");

        Assert.Contains(result.Fields, x => x.FieldKey == "invoice_number" && x.SuggestedValue == "СЧ-2026-015");
        Assert.Contains(result.Fields, x => x.FieldKey == "invoice_date" && x.SuggestedValue == "2026-04-30");
        Assert.Contains(result.Fields, x => x.FieldKey == "supplier" && x.SuggestedValue == "ООО Поставщик");
        Assert.Contains(result.Fields, x => x.FieldKey == "amount" && x.SuggestedValue == "98000");
        Assert.Contains(result.Fields, x => x.FieldKey == "payment_due" && x.SuggestedValue == "2026-05-10");
    }

    [Fact]
    public void Extract_Invoice_Ignores_Bank_Details_And_Trims_Supplier()
    {
        const string text = """
            Счет на оплату № СЧ-2026-015
            Поставщик: ИП Иванов Анатолий Иванович, ИНН: 773576240338, адрес: Москва
            Банк получателя 30101810400000000225 БИК 407028104002600...
            Всего к оплате: 12500,00 руб.
            """;

        var result = _extractor.Extract(DocumentType.Invoice, text, "invoice.pdf");

        Assert.Contains(result.Fields, x => x.FieldKey == "invoice_number" && x.SuggestedValue == "СЧ-2026-015");
        Assert.Contains(result.Fields, x => x.FieldKey == "supplier" && x.SuggestedValue == "ИП Иванов Анатолий Иванович");
        Assert.Contains(result.Fields, x => x.FieldKey == "amount" && x.SuggestedValue == "12500");
    }

    [Fact]
    public void Extract_Invoice_Does_Not_Use_Account_Number_As_Amount()
    {
        const string text = """
            Банк получателя 30101810400000000225 БИК 044525600
            Получатель Счет № 453/34К от 30.09.2020 г.
            Сч. № 40702810400260004426
            Всего к оплате: 346,11 руб.
            """;

        var result = _extractor.Extract(DocumentType.Invoice, text, "invoice.pdf");

        Assert.Contains(result.Fields, x => x.FieldKey == "invoice_number" && x.SuggestedValue == "453/34К");
        Assert.Contains(result.Fields, x => x.FieldKey == "amount" && x.SuggestedValue == "346.11");
    }

    [Fact]
    public void BuildSuggestions_Shows_Unresolved_Template_Fields_When_NotExtracted()
    {
        // Covered indirectly in UI-building logic; extractor intentionally returns no amount here.
        const string text = """
            Счет № 453/34К от 30.09.2020 г.
            Поставщик: ИП Иванов Анатолий Иванович
            """;

        var result = _extractor.Extract(DocumentType.Invoice, text, "invoice.pdf");

        Assert.DoesNotContain(result.Fields, x => x.FieldKey == "amount");
    }

    [Fact]
    public void Extract_Application_Returns_TemplateAligned_Fields()
    {
        const string text = """
            Заявление
            ФИО сотрудника: Иванов Иван Иванович
            Подразделение: Отдел документооборота
            Тема обращения: На отпуск
            Прошу предоставить ежегодный оплачиваемый отпуск с 15.06.2026.
            """;

        var result = _extractor.Extract(DocumentType.Application, text, "application.docx");

        Assert.Contains(result.Fields, x => x.FieldKey == "employee_name" && x.SuggestedValue == "Иванов Иван Иванович");
        Assert.Contains(result.Fields, x => x.FieldKey == "department" && x.SuggestedValue == "Отдел документооборота");
        Assert.Contains(result.Fields, x => x.FieldKey == "application_topic" && x.SuggestedValue == "На отпуск");
        Assert.Contains(result.Fields, x => x.FieldKey == "application_text" && x.SuggestedValue.Contains("ежегодный оплачиваемый отпуск", StringComparison.Ordinal));
    }
}
