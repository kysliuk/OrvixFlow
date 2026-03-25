using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        var normalizedEmail = email.ToLowerInvariant();
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail))
            return new AuthResult(false, Error: "An account with this email already exists.");

        var tenant = new Tenant
        {
            Name = displayName,
            Plan = "Trialing",
            SubscriptionStatus = "Trialing"
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = normalizedEmail,
            DisplayName = displayName,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.CompanyOwner.ToClaimValue()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await EnsureOwnerMembershipAsync(user.Id, tenant.Id);

        var token = await MintJwtAsync(user, tenant.Id);
        var profile = await BuildProfileAsync(user, tenant.Id);
        return new AuthResult(true, Token: token, Profile: profile);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.OAuthProvider == "local");

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, Error: "Invalid email or password.");

        var activeCompanyId = user.TenantId;
        var token = await MintJwtAsync(user, activeCompanyId);
        var profile = await BuildProfileAsync(user, activeCompanyId);
        return new AuthResult(true, Token: token, Profile: profile);
    }

    public async Task<AuthResult> ProvisionOAuthUserAsync(string email, string displayName, string provider, string externalId)
    {
        var normalizedEmail = email.ToLowerInvariant();

        // Idempotent: return existing user if OAuth account already provisioned
        var existing = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.OAuthProvider == provider && u.ExternalId == externalId);

        if (existing != null)
        {
            await EnsureOwnerMembershipAsync(existing.Id, existing.TenantId);
            var existingToken = await MintJwtAsync(existing, existing.TenantId);
            var existingProfile = await BuildProfileAsync(existing, existing.TenantId);
            return new AuthResult(true, Token: existingToken, Profile: existingProfile);
        }

        // Check if email already exists under a different provider – link accounts
        var byEmail = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (byEmail != null)
        {
            // Upgrade account to OAuth if it was previously local
            byEmail.OAuthProvider = provider;
            byEmail.ExternalId = externalId;
            await _db.SaveChangesAsync();
            await EnsureOwnerMembershipAsync(byEmail.Id, byEmail.TenantId);
            var linkedToken = await MintJwtAsync(byEmail, byEmail.TenantId);
            var linkedProfile = await BuildProfileAsync(byEmail, byEmail.TenantId);
            return new AuthResult(true, Token: linkedToken, Profile: linkedProfile);
        }

        // Brand new user — auto-provision tenant
        var tenant = new Tenant { Name = displayName, Plan = "Free", SubscriptionStatus = "Active" };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = normalizedEmail,
            DisplayName = displayName,
            OAuthProvider = provider,
            ExternalId = externalId,
            Role = UserRole.CompanyOwner.ToClaimValue()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await EnsureOwnerMembershipAsync(user.Id, tenant.Id);

        var token = await MintJwtAsync(user, tenant.Id);
        var profile = await BuildProfileAsync(user, tenant.Id);
        return new AuthResult(true, Token: token, Profile: profile);
    }

    public async Task<AuthResult> SwitchCompanyAsync(Guid userId, Guid companyId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return new AuthResult(false, Error: "User not found.");
        }

        var membership = await _db.UserCompanyMemberships
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active");
        if (!membership)
        {
            return new AuthResult(false, Error: "You do not belong to this company.");
        }

        var token = await MintJwtAsync(user, companyId);
        var profile = await BuildProfileAsync(user, companyId);
        return new AuthResult(true, Token: token, Profile: profile);
    }

    private async Task<string> MintJwtAsync(User user, Guid activeCompanyId)
    {
        var secret = _config["Jwt:Secret"] ?? throw new Exception("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId);
        // Parse role at boundary; emit canonical string into JWT claim
        var roleString = await _db.UserCompanyMemberships
            .Where(m => m.UserId == user.Id && m.CompanyId == activeCompanyId && m.Status == "Active")
            .Select(m => m.CompanyRole)
            .FirstOrDefaultAsync() ?? user.Role;
        var parsedRole = UserRoleExtensions.ParseRole(roleString);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("TenantId", activeCompanyId.ToString()),
            new("ActiveCompanyId", activeCompanyId.ToString()),
            new("Plan", company.Plan),
            new("Role", parsedRole.ToClaimValue()),
            new("DisplayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "orvixflow",
            audience: _config["Jwt:Audience"] ?? "orvixflow-web",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserProfile> BuildProfileAsync(User user, Guid activeCompanyId)
    {
        var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId);
        var memberships = await _db.UserCompanyMemberships
            .Where(m => m.UserId == user.Id && m.Status == "Active")
            .Join(_db.Tenants, m => m.CompanyId, c => c.Id, (m, c) => new CompanyMembershipSummary(c.Id, c.Name, m.CompanyRole))
            .ToListAsync();
        var activeRole = memberships.FirstOrDefault(m => m.CompanyId == activeCompanyId)?.Role ?? user.Role;

        return new UserProfile(
            user.Id,
            activeCompanyId,
            activeCompanyId,
            user.Email,
            user.DisplayName,
            activeRole,
            company.Plan,
            memberships
        );
    }

    private async Task EnsureOwnerMembershipAsync(Guid userId, Guid companyId)
    {
        var exists = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId);
        if (exists)
        {
            return;
        }

        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = UserRole.CompanyOwner.ToClaimValue(),
            Status = "Active",
            InvitedAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow
        });

        var defaultDepartment = await _db.Departments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Code == "general");
        if (defaultDepartment == null)
        {
            defaultDepartment = new Department
            {
                CompanyId = companyId,
                Name = "General",
                Code = "general",
                IsActive = true
            };
            _db.Departments.Add(defaultDepartment);
            await _db.SaveChangesAsync();
        }

        var departmentMembershipExists = await _db.UserDepartmentMemberships
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId && m.DepartmentId == defaultDepartment.Id);
        if (!departmentMembershipExists)
        {
            _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
            {
                UserId = userId,
                CompanyId = companyId,
                DepartmentId = defaultDepartment.Id,
                DepartmentRole = "Manager",
                Status = "Active"
            });
        }

        await _db.SaveChangesAsync();
    }

    // ── Invitation ────────────────────────────────────────────────────────────

    public async Task<InviteResult> InviteUserAsync(InviteRequest request)
    {
        // Validate role is canonical
        var role = UserRoleExtensions.ParseRole(request.AssignedRole);
        if (!UserRoleExtensions.AllRoles.Contains(role))
            return new InviteResult(false, Error: $"Invalid role: {request.AssignedRole}");

        // Revoke any existing pending invite for this email+company
        var existing = await _db.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.Email == request.Email.ToLowerInvariant()
                        && i.CompanyId == request.CompanyId
                        && i.Status == "Pending")
            .ToListAsync();
        foreach (var old in existing)
            old.Status = "Revoked";

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                           .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var invitation = new Invitation
        {
            Email          = request.Email.ToLowerInvariant(),
            CompanyId      = request.CompanyId,
            AssignedRole   = role.ToClaimValue(),
            DepartmentId   = request.DepartmentId,
            Token          = token,
            Status         = "Pending",
            InvitedByUserId = request.InvitedByUserId,
            ExpiresAt      = DateTime.UtcNow.AddDays(7),
        };
        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        return new InviteResult(true, Token: token);
    }

    public async Task<AuthResult> AcceptInvitationAsync(string token, string? displayName, string? password)
    {
        var invitation = await _db.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == token && i.Status == "Pending");

        if (invitation == null)
            return new AuthResult(false, Error: "Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            await _db.SaveChangesAsync();
            return new AuthResult(false, Error: "Invitation has expired. Please request a new one.");
        }

        var normalizedEmail = invitation.Email;

        // Find or create the user
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return new AuthResult(false, Error: "displayName is required for new accounts.");

            user = new User
            {
                Email        = normalizedEmail,
                DisplayName  = displayName,
                OAuthProvider = "local",
                PasswordHash = string.IsNullOrWhiteSpace(password)
                               ? null
                               : BCrypt.Net.BCrypt.HashPassword(password),
                Role         = invitation.AssignedRole,
                // Assign a TenantId - use the invited company as primary tenant
                TenantId     = invitation.CompanyId,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        // Apply company membership with pre-assigned role
        var membershipExists = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == user.Id && m.CompanyId == invitation.CompanyId);

        if (!membershipExists)
        {
            _db.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId        = user.Id,
                CompanyId     = invitation.CompanyId,
                CompanyRole   = invitation.AssignedRole,
                Status        = "Active",
                InvitedAt     = invitation.CreatedAt,
                JoinedAt      = DateTime.UtcNow,
                InvitedByUserId = invitation.InvitedByUserId,
            });
        }

        // Apply department membership if specified
        if (invitation.DepartmentId.HasValue)
        {
            var deptExists = await _db.UserDepartmentMemberships
                .IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == user.Id
                               && m.CompanyId == invitation.CompanyId
                               && m.DepartmentId == invitation.DepartmentId.Value);
            if (!deptExists)
            {
                _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
                {
                    UserId       = user.Id,
                    CompanyId    = invitation.CompanyId,
                    DepartmentId = invitation.DepartmentId.Value,
                    DepartmentRole = invitation.AssignedRole,
                    Status       = "Active",
                });
            }
        }

        invitation.Status = "Accepted";
        await _db.SaveChangesAsync();

        var jwtToken = await MintJwtAsync(user, invitation.CompanyId);
        var profile  = await BuildProfileAsync(user, invitation.CompanyId);
        return new AuthResult(true, Token: jwtToken, Profile: profile);
    }
}
