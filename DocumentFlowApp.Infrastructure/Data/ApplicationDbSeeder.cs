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

        var adminUser = await EnsureUserAsync(context, adminRole.RoleId, "admin", "admin@docflow.local", "Admin123!", "Системный", "Администратор", null, cancellationToken);
        var managerUser = await EnsureUserAsync(context, managerRole.RoleId, "manager", "manager@docflow.local", "Manager123!", "Ирина", "Менеджер", null, cancellationToken);
        var secondManagerUser = await EnsureUserAsync(context, managerRole.RoleId, "manager2", "manager2@docflow.local", "Manager123!", "Олег", "Согласующий", ApprovalSpecializations.Manager, cancellationToken);
        var employeeUser = await EnsureUserAsync(context, employeeRole.RoleId, "employee", "employee@docflow.local", "Employee123!", "Алексей", "Исполнитель", null, cancellationToken);
        var lawyerUser = await EnsureUserAsync(context, employeeRole.RoleId, "lawyer", "lawyer@docflow.local", "Employee123!", "Мария", "Юрист", ApprovalSpecializations.Lawyer, cancellationToken);
        var accountantUser = await EnsureUserAsync(context, employeeRole.RoleId, "accountant", "accountant@docflow.local", "Employee123!", "Светлана", "Бухгалтер", ApprovalSpecializations.Accountant, cancellationToken);
        var hrUser = await EnsureUserAsync(context, employeeRole.RoleId, "hr", "hr@docflow.local", "Employee123!", "Елена", "Кадры", ApprovalSpecializations.Hr, cancellationToken);

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

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут договоров",
            "Contract",
            "Согласование договоров юристом.",
            ApprovalSpecializations.Lawyer,
            lawyerUser.UserId,
            lawyerUser.Role?.RoleName ?? AppRoles.Employee,
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут счетов",
            "Invoice",
            "Согласование счетов бухгалтером.",
            ApprovalSpecializations.Accountant,
            accountantUser.UserId,
            accountantUser.Role?.RoleName ?? AppRoles.Employee,
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут заявлений",
            "Application",
            "Согласование заявлений кадровой службой.",
            ApprovalSpecializations.Hr,
            hrUser.UserId,
            hrUser.Role?.RoleName ?? AppRoles.Employee,
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Общий маршрут по умолчанию",
            null,
            "Используется для типов документов без отдельного шаблона маршрута.",
            ApprovalSpecializations.Manager,
            secondManagerUser.UserId,
            secondManagerUser.Role?.RoleName ?? AppRoles.Manager,
            cancellationToken);

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

    private static async Task<User> EnsureUserAsync(
        ApplicationDbContext context,
        int roleId,
        string userName,
        string email,
        string password,
        string firstName,
        string lastName,
        string? approvalSpecialization,
        CancellationToken cancellationToken)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existingUser is not null)
        {
            existingUser.ApprovalSpecialization = ApprovalSpecializations.Normalize(approvalSpecialization);
            await context.SaveChangesAsync(cancellationToken);
            return existingUser;
        }

        var user = new User
        {
            RoleId = roleId,
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            ApprovalSpecialization = ApprovalSpecializations.Normalize(approvalSpecialization),
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var passwordHasher = new PasswordHasher<User>();
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user;
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

    private static async Task EnsureRouteTemplateAsync(
        ApplicationDbContext context,
        string name,
        string? documentType,
        string description,
        string approvalSpecialization,
        int approverUserId,
        string approverRole,
        CancellationToken cancellationToken)
    {
        var template = await context.RouteTemplates
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (template is null)
        {
            template = new RouteTemplate
            {
                Name = name,
                DocumentType = documentType,
                Description = description,
                IsActive = true,
                IsDefault = true,
                CreatedDate = DateTime.UtcNow
            };
            context.RouteTemplates.Add(template);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            template.DocumentType = documentType;
            template.Description = description;
            template.IsActive = true;
            template.IsDefault = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        var existingStep = await context.RouteSteps.FirstOrDefaultAsync(x => x.RouteTemplateId == template.RouteTemplateId && x.StepOrder == 1, cancellationToken);
        if (existingStep is null)
        {
            context.RouteSteps.Add(new RouteStep
            {
                RouteTemplateId = template.RouteTemplateId,
                StepOrder = 1,
                Title = "Согласование",
                ApproverRole = approverRole,
                ApproverSpecialization = approvalSpecialization,
                ApproverUserId = approverUserId,
                IsRequired = true
            });
            return;
        }

        existingStep.Title = "Согласование";
        existingStep.ApproverRole = approverRole;
        existingStep.ApproverSpecialization = approvalSpecialization;
        existingStep.ApproverUserId = approverUserId;
        existingStep.IsRequired = true;
    }
}
