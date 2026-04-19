using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
