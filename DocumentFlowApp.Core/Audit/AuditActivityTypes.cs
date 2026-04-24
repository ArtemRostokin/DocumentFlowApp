namespace DocumentFlowApp.Core.Audit;

public static class AuditActivityTypes
{
    public const string DocumentCreated = "document-created";
    public const string IncomingUploaded = "incoming-uploaded";
    public const string DocumentUpdated = "document-updated";
    public const string ApprovalApproved = "approval-approved";
    public const string ApprovalRework = "approval-rework";
    public const string NomenclatureAssigned = "nomenclature-assigned";
    public const string ExecutorAssigned = "executor-assigned";
    public const string StatusChanged = "status-changed";
    public const string WorkStarted = "work-started";
    public const string WorkCompleted = "work-completed";
    public const string ExecutionSaved = "execution-saved";
    public const string ExecutionFileGenerated = "execution-file-generated";
    public const string ExecutionFileUploaded = "execution-file-uploaded";
    public const string UserLogin = "user-login";
    public const string UserLogout = "user-logout";
    public const string NomenclatureCaseCreated = "nomenclature-case-created";
    public const string NomenclatureRuleCreated = "nomenclature-rule-created";

    public static IReadOnlyList<string> All { get; } =
    [
        DocumentCreated,
        IncomingUploaded,
        DocumentUpdated,
        ApprovalApproved,
        ApprovalRework,
        NomenclatureAssigned,
        ExecutorAssigned,
        StatusChanged,
        WorkStarted,
        WorkCompleted,
        ExecutionSaved,
        ExecutionFileGenerated,
        ExecutionFileUploaded,
        UserLogin,
        UserLogout,
        NomenclatureCaseCreated,
        NomenclatureRuleCreated
    ];

    public static string GetDisplayName(string? activityType) => activityType switch
    {
        DocumentCreated => "Создание документа",
        IncomingUploaded => "Загрузка входящего документа",
        DocumentUpdated => "Редактирование карточки",
        ApprovalApproved => "Утверждение",
        ApprovalRework => "Возврат на доработку",
        NomenclatureAssigned => "Привязка к номенклатуре",
        ExecutorAssigned => "Назначение исполнителя",
        StatusChanged => "Смена статуса",
        WorkStarted => "Начало исполнения",
        WorkCompleted => "Завершение исполнения",
        ExecutionSaved => "Сохранение хода исполнения",
        ExecutionFileGenerated => "Формирование итогового файла",
        ExecutionFileUploaded => "Загрузка итогового файла",
        UserLogin => "Вход в систему",
        UserLogout => "Выход из системы",
        NomenclatureCaseCreated => "Создание дела номенклатуры",
        NomenclatureRuleCreated => "Создание правила номенклатуры",
        _ => "Прочее действие"
    };
}
