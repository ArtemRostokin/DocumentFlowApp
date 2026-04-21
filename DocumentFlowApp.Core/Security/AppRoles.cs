namespace DocumentFlowApp.Core.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static string? Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        var value = role.Trim();

        if (MatchesAny(value, Admin, "Administrator"))
            return Admin;

        if (MatchesAny(value, Manager))
            return Manager;

        if (MatchesAny(value, Employee, "User", "Executor"))
            return Employee;

        return value;
    }

    private static bool MatchesAny(string value, params string[] variants)
    {
        return variants.Any(variant => string.Equals(value, variant, StringComparison.OrdinalIgnoreCase));
    }
}
