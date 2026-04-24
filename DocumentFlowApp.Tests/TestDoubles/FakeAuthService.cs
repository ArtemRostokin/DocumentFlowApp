using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class FakeAuthService : IAuthService
{
    public AuthResult Result { get; set; } = AuthResult.Failed("not configured");
    public string? LastEmail { get; private set; }
    public string? LastPassword { get; private set; }

    public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        LastEmail = email;
        LastPassword = password;
        return Task.FromResult(Result);
    }
}
