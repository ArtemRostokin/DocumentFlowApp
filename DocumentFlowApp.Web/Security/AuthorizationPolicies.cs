using System.Security.Claims;
using DocumentFlowApp.Core.Security;

namespace DocumentFlowApp.Web.Security;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string EmployeeOrHigher = "EmployeeOrHigher";
    public const string RoleClaimType = "df_role";

    public static bool IsInAppRole(ClaimsPrincipal user, string requiredRole)
    {
        var normalizedRequiredRole = AppRoles.Normalize(requiredRole);
        if (normalizedRequiredRole is null)
            return false;

        foreach (var claim in user.Claims)
        {
            if (claim.Type is ClaimTypes.Role or RoleClaimType)
            {
                var normalizedActualRole = AppRoles.Normalize(claim.Value);
                if (string.Equals(normalizedActualRole, normalizedRequiredRole, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
