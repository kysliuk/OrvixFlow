using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> ProvisionOAuthUserAsync(string email, string displayName, string provider, string externalId);
}

public record AuthResult(bool IsSuccess, string? Token = null, string? Error = null, UserProfile? Profile = null);

public record UserProfile(Guid UserId, Guid TenantId, string Email, string DisplayName, string Role, string Plan);
