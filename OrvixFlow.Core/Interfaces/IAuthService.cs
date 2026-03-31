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
    Task<AuthResult> UpdateUserAsync(Guid userId, string? displayName);

    /// <summary>Create a pending invitation; returns the one-time token.</summary>
    Task<InviteResult> InviteUserAsync(InviteRequest request);

    /// <summary>Accept a pending invitation; provisions the user and returns auth tokens.</summary>
    Task<AuthResult> AcceptInvitationAsync(string token, string? displayName, string? password);
}

public record AuthResult(bool IsSuccess, string? Token = null, string? Error = null, UserProfile? Profile = null);

public record InviteRequest(
    Guid InvitedByUserId,
    Guid CompanyId,
    string Email,
    string AssignedRole,
    Guid? DepartmentId = null
);

public record InviteResult(bool IsSuccess, string? Token = null, string? Error = null);

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
