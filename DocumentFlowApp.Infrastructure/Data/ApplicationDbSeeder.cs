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

    private sealed class RouteSeedStep
    {
        public int StepOrder { get; init; }
        public string Title { get; init; } = string.Empty;
        public string ApprovalSpecialization { get; init; } = string.Empty;
        public int ApproverUserId { get; init; }
        public string ApproverRole { get; init; } = string.Empty;
        public bool IsRequired { get; init; } = true;
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

        await EnsureTemplateAsync(
            context,
            name: "Шаблон служебной записки",
            category: "ServiceMemo",
            description: "Используется для внутренних служебных записок, уведомлений и пояснений по рабочим вопросам.",
            fields:
            [
                new TemplateSeedField { Key = "memo_number", Label = "Номер записки", Placeholder = "СЗ-2026-014", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "memo_date", Label = "Дата записки", Placeholder = string.Empty, Required = true, InputType = "date" },
                new TemplateSeedField { Key = "initiator", Label = "Инициатор", Placeholder = "ФИО сотрудника", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "department", Label = "Подразделение", Placeholder = "Например: отдел сопровождения", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "memo_topic", Label = "Тема записки", Placeholder = "Краткая тема обращения", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "memo_text", Label = "Содержание записки", Placeholder = "Опишите обстоятельства, предложение или пояснение", Required = true, InputType = "textarea" }
            ],
            cancellationToken);

        await EnsureTemplateAsync(
            context,
            name: "Шаблон заявки на закупку",
            category: "PurchaseRequest",
            description: "Используется для инициации закупки товаров, работ или услуг с указанием суммы и обоснования.",
            fields:
            [
                new TemplateSeedField { Key = "request_number", Label = "Номер заявки", Placeholder = "ЗЗ-2026-008", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "request_date", Label = "Дата заявки", Placeholder = string.Empty, Required = true, InputType = "date" },
                new TemplateSeedField { Key = "initiator", Label = "Инициатор", Placeholder = "ФИО сотрудника", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "department", Label = "Подразделение", Placeholder = "Например: отдел снабжения", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "purchase_item", Label = "Предмет закупки", Placeholder = "Оборудование / услуги / материалы", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "quantity", Label = "Количество", Placeholder = "1", Required = false, InputType = "number" },
                new TemplateSeedField { Key = "amount", Label = "Плановая сумма", Placeholder = "250000", Required = true, InputType = "number" },
                new TemplateSeedField { Key = "justification", Label = "Обоснование закупки", Placeholder = "Почему закупка необходима", Required = true, InputType = "textarea" }
            ],
            cancellationToken);

        await EnsureTemplateAsync(
            context,
            name: "Шаблон акта выполненных работ",
            category: "Act",
            description: "Используется для фиксации факта выполнения работ или оказания услуг и подготовки к закрытию обязательств.",
            fields:
            [
                new TemplateSeedField { Key = "act_number", Label = "Номер акта", Placeholder = "АВР-2026-021", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "act_date", Label = "Дата акта", Placeholder = string.Empty, Required = true, InputType = "date" },
                new TemplateSeedField { Key = "counterparty", Label = "Контрагент", Placeholder = "ООО Подрядчик", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "basis_document", Label = "Основание", Placeholder = "Договор / заказ / этап работ", Required = true, InputType = "text" },
                new TemplateSeedField { Key = "amount", Label = "Сумма акта", Placeholder = "180000", Required = true, InputType = "number" },
                new TemplateSeedField { Key = "work_description", Label = "Описание работ", Placeholder = "Какие работы или услуги приняты", Required = true, InputType = "textarea" }
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

        var memoCase = await EnsureNomenclatureCaseAsync(
            context,
            "03-03",
            "Служебные записки и внутренние пояснения",
            "3 года",
            "Локальная номенклатура организации",
            "Канцелярия",
            cancellationToken);

        var purchaseRequestCase = await EnsureNomenclatureCaseAsync(
            context,
            "02-04",
            "Заявки на закупку и согласование расходов",
            "5 лет",
            "Перечень типовых управленческих документов",
            "Финансовый отдел",
            cancellationToken);

        var actCase = await EnsureNomenclatureCaseAsync(
            context,
            "01-04",
            "Акты выполненных работ и закрывающие документы",
            "5 лет",
            "Перечень типовых управленческих документов",
            "Бухгалтерия",
            cancellationToken);

        await EnsureNomenclatureRuleAsync(context, contractCase.NomenclatureCaseId, "Contract", null, "Автопривязка для договоров", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, invoiceCase.NomenclatureCaseId, "Invoice", null, "Автопривязка для счетов", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, applicationCase.NomenclatureCaseId, "Application", null, "Автопривязка для заявлений", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, memoCase.NomenclatureCaseId, "ServiceMemo", null, "Автопривязка для служебных записок", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, purchaseRequestCase.NomenclatureCaseId, "PurchaseRequest", null, "Автопривязка для заявок на закупку", cancellationToken);
        await EnsureNomenclatureRuleAsync(context, actCase.NomenclatureCaseId, "Act", null, "Автопривязка для актов выполненных работ", cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут договоров",
            "Contract",
            "Согласование договоров юристом.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Юридическая проверка",
                    ApprovalSpecialization = ApprovalSpecializations.Lawyer,
                    ApproverUserId = lawyerUser.UserId,
                    ApproverRole = lawyerUser.Role?.RoleName ?? AppRoles.Employee
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут счетов",
            "Invoice",
            "Согласование счетов бухгалтером.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Финансовая проверка",
                    ApprovalSpecialization = ApprovalSpecializations.Accountant,
                    ApproverUserId = accountantUser.UserId,
                    ApproverRole = accountantUser.Role?.RoleName ?? AppRoles.Employee
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут заявлений",
            "Application",
            "Согласование заявлений кадровой службой.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Проверка кадровой службой",
                    ApprovalSpecialization = ApprovalSpecializations.Hr,
                    ApproverUserId = hrUser.UserId,
                    ApproverRole = hrUser.Role?.RoleName ?? AppRoles.Employee
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут служебных записок",
            "ServiceMemo",
            "Согласование служебных записок руководителем подразделения.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Согласование руководителем",
                    ApprovalSpecialization = ApprovalSpecializations.Manager,
                    ApproverUserId = secondManagerUser.UserId,
                    ApproverRole = secondManagerUser.Role?.RoleName ?? AppRoles.Manager
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут заявок на закупку",
            "PurchaseRequest",
            "Проверка необходимости закупки и согласование планового бюджета.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Согласование руководителем",
                    ApprovalSpecialization = ApprovalSpecializations.Manager,
                    ApproverUserId = secondManagerUser.UserId,
                    ApproverRole = secondManagerUser.Role?.RoleName ?? AppRoles.Manager
                },
                new RouteSeedStep
                {
                    StepOrder = 2,
                    Title = "Финансовое согласование",
                    ApprovalSpecialization = ApprovalSpecializations.Accountant,
                    ApproverUserId = accountantUser.UserId,
                    ApproverRole = accountantUser.Role?.RoleName ?? AppRoles.Employee
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Базовый маршрут актов выполненных работ",
            "Act",
            "Подтверждение выполнения работ и финансовая проверка закрывающих документов.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Подтверждение результата",
                    ApprovalSpecialization = ApprovalSpecializations.Manager,
                    ApproverUserId = secondManagerUser.UserId,
                    ApproverRole = secondManagerUser.Role?.RoleName ?? AppRoles.Manager
                },
                new RouteSeedStep
                {
                    StepOrder = 2,
                    Title = "Проверка бухгалтерией",
                    ApprovalSpecialization = ApprovalSpecializations.Accountant,
                    ApproverUserId = accountantUser.UserId,
                    ApproverRole = accountantUser.Role?.RoleName ?? AppRoles.Employee
                }
            ],
            cancellationToken);

        await EnsureRouteTemplateAsync(
            context,
            "Общий маршрут по умолчанию",
            null,
            "Используется для типов документов без отдельного шаблона маршрута.",
            [
                new RouteSeedStep
                {
                    StepOrder = 1,
                    Title = "Общее согласование",
                    ApprovalSpecialization = ApprovalSpecializations.Manager,
                    ApproverUserId = secondManagerUser.UserId,
                    ApproverRole = secondManagerUser.Role?.RoleName ?? AppRoles.Manager
                }
            ],
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
        IReadOnlyList<RouteSeedStep> steps,
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

        foreach (var stepSeed in steps)
        {
            var existingStep = await context.RouteSteps.FirstOrDefaultAsync(
                x => x.RouteTemplateId == template.RouteTemplateId && x.StepOrder == stepSeed.StepOrder,
                cancellationToken);

            if (existingStep is null)
            {
                context.RouteSteps.Add(new RouteStep
                {
                    RouteTemplateId = template.RouteTemplateId,
                    StepOrder = stepSeed.StepOrder,
                    Title = stepSeed.Title,
                    ApproverRole = stepSeed.ApproverRole,
                    ApproverSpecialization = stepSeed.ApprovalSpecialization,
                    ApproverUserId = stepSeed.ApproverUserId,
                    IsRequired = stepSeed.IsRequired
                });
                continue;
            }

            existingStep.Title = stepSeed.Title;
            existingStep.ApproverRole = stepSeed.ApproverRole;
            existingStep.ApproverSpecialization = stepSeed.ApprovalSpecialization;
            existingStep.ApproverUserId = stepSeed.ApproverUserId;
            existingStep.IsRequired = stepSeed.IsRequired;
        }
    }
}
