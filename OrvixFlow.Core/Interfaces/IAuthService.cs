using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> ProvisionOAuthUserAsync(string email, string displayName, string provider, string externalId);
    Task<AuthResult> SwitchCompanyAsync(Guid userId, Guid companyId);
}

public record AuthResult(bool IsSuccess, string? Token = null, string? Error = null, UserProfile? Profile = null);

public record CompanyMembershipSummary(Guid CompanyId, string CompanyName, string Role);

public record UserProfile(
    Guid UserId,
    Guid TenantId,
    Guid ActiveCompanyId,
    string Email,
    string DisplayName,
    string Role,
    string Plan,
    IReadOnlyList<CompanyMembershipSummary> Companies
);
