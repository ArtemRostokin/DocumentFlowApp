namespace DocumentFlowApp.Core.Security;

public static class ApprovalSpecializations
{
    public const string Manager = "ManagerReview";
    public const string Accountant = "Accountant";
    public const string Lawyer = "Lawyer";
    public const string Hr = "Hr";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Manager] = "Общее согласование",
        [Accountant] = "Бухгалтер",
        [Lawyer] = "Юрист",
        [Hr] = "Кадры"
    };

    public static IReadOnlyList<string> All { get; } =
    [
        Manager,
        Accountant,
        Lawyer,
        Hr
    ];

    public static string? Normalize(string? specialization)
    {
        if (string.IsNullOrWhiteSpace(specialization))
            return null;

        return Labels.Keys.FirstOrDefault(key => string.Equals(key, specialization.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string GetLabel(string? specialization)
    {
        var normalized = Normalize(specialization);
        return normalized is not null && Labels.TryGetValue(normalized, out var label)
            ? label
            : "Не задан";
    }
}
