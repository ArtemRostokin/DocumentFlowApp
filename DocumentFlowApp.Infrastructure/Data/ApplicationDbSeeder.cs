using System.Text.Json;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentFlowApp.Infrastructure.Data;

public static class ApplicationDbSeeder
{
    private sealed class TemplateSeedField
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Placeholder { get; init; } = string.Empty;
        public bool Required { get; init; }
        public string InputType { get; init; } = "text";
    }

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        var adminRole = await EnsureRoleAsync(context, AppRoles.Admin, "Администратор системы", cancellationToken);
        var managerRole = await EnsureRoleAsync(context, AppRoles.Manager, "Менеджер документов", cancellationToken);
        var employeeRole = await EnsureRoleAsync(context, AppRoles.Employee, "Исполнитель", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(context, adminRole.RoleId, "admin", "admin@docflow.local", "Admin123!", "Системный", "Администратор", cancellationToken);
        await EnsureUserAsync(context, managerRole.RoleId, "manager", "manager@docflow.local", "Manager123!", "Ирина", "Менеджер", cancellationToken);
        await EnsureUserAsync(context, employeeRole.RoleId, "employee", "employee@docflow.local", "Employee123!", "Алексей", "Исполнитель", cancellationToken);

        await EnsureTemplateAsync(
            context,
            name: "Шаблон договора",
            category: "Contract",
            description: "Используется для подготовки договоров с контрагентами и фиксации ключевых условий сделки.",
            fields:
            [
                new TemplateSeedField { Key = "contract_number", Label = "Номер договора", Placeholder = "Например: 42/2026", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "contract_date", Label = "Дата договора", Placeholder = string.Empty, Required = true, InputType = "date" },
                new TemplateSeedField { Key = "counterparty", Label = "Контрагент", Placeholder = "ООО Ромашка", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "amount", Label = "Сумма договора", Placeholder = "150000", Required = true, InputType = "number" },
                new TemplateSeedField { Key = "subject", Label = "Предмет договора", Placeholder = "Кратко опишите предмет договора", Required = true, InputType = "textarea" }
            ],
            cancellationToken);

        await EnsureTemplateAsync(
            context,
            name: "Шаблон счета",
            category: "Invoice",
            description: "Используется для выставления и регистрации счетов на оплату.",
            fields:
            [
                new TemplateSeedField { Key = "invoice_number", Label = "Номер счета", Placeholder = "СЧ-2026-015", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "invoice_date", Label = "Дата счета", Placeholder = string.Empty, Required = true, InputType = "date" },
                new TemplateSeedField { Key = "supplier", Label = "Поставщик", Placeholder = "ООО Поставщик", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "amount", Label = "Сумма", Placeholder = "98000", Required = true, InputType = "number" },
                new TemplateSeedField { Key = "payment_due", Label = "Срок оплаты", Placeholder = string.Empty, Required = false, InputType = "date" }
            ],
            cancellationToken);

        await EnsureTemplateAsync(
            context,
            name: "Шаблон заявления",
            category: "Application",
            description: "Используется для внутренних заявлений сотрудников и служебных обращений.",
            fields:
            [
                new TemplateSeedField { Key = "employee_name", Label = "ФИО сотрудника", Placeholder = "Иванов Иван Иванович", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "department", Label = "Подразделение", Placeholder = "Отдел документооборота", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "application_topic", Label = "Тема обращения", Placeholder = "На отпуск / на закупку / служебная записка", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "application_text", Label = "Текст заявления", Placeholder = "Опишите суть обращения", Required = true, InputType = "textarea" }
            ],
            cancellationToken);

        var contractCase = await EnsureNomenclatureCaseAsync(
            context,
            "01-01",
            "Договоры с контрагентами",
            "5 лет",
            "Перечень типовых управленческих документов",
            "Юридический отдел",
            cancellationToken);

        var invoiceCase = await EnsureNomenclatureCaseAsync(
            context,
            "02-03",
            "Счета и платежные документы",
            "5 лет",
            "Перечень типовых управленческих документов",
            "Бухгалтерия",
            cancellationToken);

        var applicationCase = await EnsureNomenclatureCaseAsync(
            context,
            "03-02",
            "Заявления и внутренние обращения сотрудников",
            "3 года",
            "Локальная номенклатура организации",
            "Отдел кадров",
            cancellationToken);

        await EnsureNomenclatureRuleAsync(context, contractCase.NomenclatureCaseId, "Contract", null, "Автопривязка для договоров", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, invoiceCase.NomenclatureCaseId, "Invoice", null, "Автопривязка для счетов", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, applicationCase.NomenclatureCaseId, "Application", null, "Автопривязка для заявлений", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(ApplicationDbContext context, string roleName, string description, CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName, cancellationToken);
        if (role is not null)
            return role;

        role = new Role
        {
            RoleName = roleName,
            Description = description
        };

        context.Roles.Add(role);
        return role;
    }

    private static async Task EnsureUserAsync(
        ApplicationDbContext context,
        int roleId,
        string userName,
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existingUser is not null)
            return;

        var user = new User
        {
            RoleId = roleId,
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var passwordHasher = new PasswordHasher<User>();
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        context.Users.Add(user);
    }

    private static async Task EnsureTemplateAsync(
        ApplicationDbContext context,
        string name,
        string category,
        string description,
        IReadOnlyList<TemplateSeedField> fields,
        CancellationToken cancellationToken)
    {
        var serializedFields = JsonSerializer.Serialize(fields);
        var template = await context.Templates.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

        if (template is null)
        {
            context.Templates.Add(new Template
            {
                Name = name,
                Category = category,
                Content = description,
                AiSuggestedFields = serializedFields,
                CreatedDate = DateTime.UtcNow,
                UsageCount = 0,
                SuccessRate = 0
            });
            return;
        }

        template.Category = category;
        template.Content = description;
        template.AiSuggestedFields = serializedFields;
    }

    private static async Task<NomenclatureCase> EnsureNomenclatureCaseAsync(
        ApplicationDbContext context,
        string index,
        string title,
        string retentionPeriod,
        string legalBasis,
        string department,
        CancellationToken cancellationToken)
    {
        var item = await context.NomenclatureCases.FirstOrDefaultAsync(x => x.Index == index, cancellationToken);
        if (item is null)
        {
            item = new NomenclatureCase
            {
                Index = index,
                Title = title,
                RetentionPeriod = retentionPeriod,
                LegalBasis = legalBasis,
                Department = department,
                IsActive = true
            };
            context.NomenclatureCases.Add(item);
            await context.SaveChangesAsync(cancellationToken);
            return item;
        }

        item.Title = title;
        item.RetentionPeriod = retentionPeriod;
        item.LegalBasis = legalBasis;
        item.Department = department;
        item.IsActive = true;
        return item;
    }

    private static async Task EnsureNomenclatureRuleAsync(
        ApplicationDbContext context,
        int nomenclatureCaseId,
        string? documentType,
        string? department,
        string? note,
        CancellationToken cancellationToken)
    {
        var rule = await context.NomenclatureRules.FirstOrDefaultAsync(
            x => x.NomenclatureCaseId == nomenclatureCaseId &&
                 x.DocumentType == documentType &&
                 x.Department == department,
            cancellationToken);

        if (rule is null)
        {
            context.NomenclatureRules.Add(new NomenclatureRule
            {
                NomenclatureCaseId = nomenclatureCaseId,
                DocumentType = documentType,
                Department = department,
                Note = note,
                IsActive = true
            });
            return;
        }

        rule.Note = note;
        rule.IsActive = true;
    }
}
