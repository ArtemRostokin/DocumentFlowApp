using System.Security.Claims;
using DocumentFlowApp.Core.Security;
using DocumentFlowApp.Web.Security;

namespace DocumentFlowApp.Tests;

public class AuthorizationPoliciesTests
{
    [Theory]
    [InlineData("Admin", AppRoles.Admin, true)]
    [InlineData("Administrator", AppRoles.Admin, true)]
    [InlineData("User", AppRoles.Employee, true)]
    [InlineData("Executor", AppRoles.Employee, true)]
    [InlineData("Manager", AppRoles.Admin, false)]
    public void IsInAppRole_Uses_Normalized_Roles(string actualRole, string requiredRole, bool expected)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, actualRole)], "TestAuth"));

        var result = AuthorizationPolicies.IsInAppRole(principal, requiredRole);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsInAppRole_Reads_DfRole_Claim()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(AuthorizationPolicies.RoleClaimType, "Manager")], "TestAuth"));

        var result = AuthorizationPolicies.IsInAppRole(principal, AppRoles.Manager);

        Assert.True(result);
    }
}
