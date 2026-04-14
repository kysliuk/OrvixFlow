using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly Microsoft.Extensions.Logging.ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, IConfiguration config, Microsoft.Extensions.Logging.ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        // F-04 FIX: Enforce password complexity
        var passwordValidation = ValidatePasswordComplexity(password);
        if (!passwordValidation.IsValid)
            return new AuthResult(false, Error: passwordValidation.ErrorMessage);

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
            EmailVerified = false,
            VerificationToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        // F-33: TODO - Send verification email here (integration point for email service)
        _logger.LogInformation("Verification token for {Email}: {Token}", normalizedEmail, user.VerificationToken);

        await EnsureOwnerMembershipAsync(user.Id, tenant.Id);

        var token = await MintJwtAsync(user, tenant.Id);
        var profile = await BuildProfileAsync(user, tenant.Id);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.OAuthProvider == "local");

        if (user == null)
        {
            _logger.LogWarning("Login failed: no local user found for email {Email}", normalizedEmail);
            return new AuthResult(false, Error: "Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: password mismatch for email {Email}", normalizedEmail);
            return new AuthResult(false, Error: "Invalid email or password.");
        }

        // F-33: Block login if email not verified
        if (!user.EmailVerified)
        {
            _logger.LogWarning("Login blocked: email not verified for {Email}", normalizedEmail);
            return new AuthResult(false, Error: "Please verify your email address before logging in.");
        }

        var activeCompanyId = user.TenantId;
        var token = await MintJwtAsync(user, activeCompanyId);
        var profile = await BuildProfileAsync(user, activeCompanyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task<AuthResult> ProvisionOAuthUserAsync(string email, string displayName, string provider, string externalId)
    {
        var normalizedEmail = email.ToLowerInvariant();

        // Idempotent: return existing user if OAuth account already provisioned
        var existing = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.OAuthProvider == provider && u.ExternalId == externalId);

        if (existing != null)
        {
            await EnsureOwnerMembershipAsync(existing.Id, existing.TenantId);
            var existingToken = await MintJwtAsync(existing, existing.TenantId);
            var existingProfile = await BuildProfileAsync(existing, existing.TenantId);
            var existingRefreshToken = await CreateRefreshTokenAsync(existing.Id);
            return new AuthResult(true, Token: existingToken, Profile: existingProfile, RefreshToken: existingRefreshToken);
        }

        // F-02 FIX: Check if email already exists.
        // SECURITY: Do NOT link accounts from different providers - this enables account takeover.
        var byEmail = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (byEmail != null)
        {
            // Auto-migrate polluted ExternalIds from previous NextAuth bug (which used random GUIDs instead of provider ID)
            if (byEmail.OAuthProvider == provider && byEmail.ExternalId != externalId)
            {
                byEmail.ExternalId = externalId;
                await _db.SaveChangesAsync();

                await EnsureOwnerMembershipAsync(byEmail.Id, byEmail.TenantId);
                var existingToken = await MintJwtAsync(byEmail, byEmail.TenantId);
                var existingProfile = await BuildProfileAsync(byEmail, byEmail.TenantId);
                var existingRefreshToken = await CreateRefreshTokenAsync(byEmail.Id);
                return new AuthResult(true, Token: existingToken, Profile: existingProfile, RefreshToken: existingRefreshToken);
            }

            // F-02 FIX: Reject the OAuth login attempt.
            // An account with this email already exists - user should sign in with their original provider.
            return new AuthResult(false, Error: "An account with this email already exists. Please sign in with your original authentication method.");
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
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await EnsureOwnerMembershipAsync(user.Id, tenant.Id);

        var token = await MintJwtAsync(user, tenant.Id);
        var profile = await BuildProfileAsync(user, tenant.Id);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task<AuthResult> SwitchCompanyAsync(Guid userId, Guid companyId)
    {
        _logger.LogInformation("[DEBUG][CompanySwitch][AuthService] Evaluating switch request for UserId: {UserId}, TargetCompanyId: {CompanyId}", userId, companyId);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            _logger.LogWarning("[DEBUG][CompanySwitch][AuthService] Rejected: User object not found in DB.");
            return new AuthResult(false, Error: "User not found.");
        }

        _logger.LogInformation("[DEBUG][CompanySwitch][AuthService] Querying UserCompanyMemberships for Active status...");
        var membership = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId);
            
        if (membership == null)
        {
            _logger.LogWarning("[DEBUG][CompanySwitch][AuthService] Rejected: No membership record exists for UserId {UserId} in CompanyId {CompanyId}.", userId, companyId);
            return new AuthResult(false, Error: "You do not belong to this company.");
        }
        
        if (membership.Status != "Active")
        {
            _logger.LogWarning("[DEBUG][CompanySwitch][AuthService] Rejected: Membership exists but status is '{Status}', expected 'Active'.", membership.Status);
            return new AuthResult(false, Error: "You do not belong to this company.");
        }

        _logger.LogInformation("[DEBUG][CompanySwitch][AuthService] Membership validated successfully with role: {Role}. Minting new JWT.", membership.CompanyRole);

        var token = await MintJwtAsync(user, companyId);
        var profile = await BuildProfileAsync(user, companyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    private async Task<string> MintJwtAsync(User user, Guid activeCompanyId)
    {
        var secret = _config["Jwt:Secret"] ?? throw new Exception("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId);

        // Global roles (platform-level) always override company-level roles.
        // A SuperAdmin or InternalOperator should have their global role in the JWT
        // regardless of what their UserCompanyMembership.CompanyRole says.
        var parsedUserRole = UserRoleExtensions.ParseRole(user.Role);
        string roleClaimValue;
        if (parsedUserRole.IsPlatformAdmin())
        {
            roleClaimValue = parsedUserRole.ToClaimValue();
        }
        else
        {
            var roleString = await _db.UserCompanyMemberships
                .IgnoreQueryFilters()
                .Where(m => m.UserId == user.Id && m.CompanyId == activeCompanyId && m.Status == "Active")
                .Select(m => m.CompanyRole)
                .FirstOrDefaultAsync() ?? user.Role;
            roleClaimValue = UserRoleExtensions.ParseRole(roleString).ToClaimValue();
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("TenantId", activeCompanyId.ToString()),
            new("ActiveCompanyId", activeCompanyId.ToString()),
            new("Plan", company.Plan),
            new("Role", roleClaimValue),
            new("DisplayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "orvixflow",
            audience: _config["Jwt:Audience"] ?? "orvixflow-web",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60), // F-01 FIX: Shortened from 7 days to 60 minutes to reduce token exposure window
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserProfile> BuildProfileAsync(User user, Guid activeCompanyId)
    {
        var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId);
        var memberships = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
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
            memberships,
            user.Role // GlobalRole
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

    public async Task<AuthResult> RefreshSessionAsync(string refreshToken)
    {
        var tokenRecord = await _db.RefreshTokens
            .Include(r => r.User)
            .ThenInclude(u => u!.Tenant)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (tokenRecord == null)
            return new AuthResult(false, Error: "Invalid refresh token.");

        if (tokenRecord.RevokedAt != null)
            return new AuthResult(false, Error: "Refresh token has been revoked.");

        if (tokenRecord.IsExpired)
            return new AuthResult(false, Error: "Refresh token has expired.");

        // Revoke the old token
        tokenRecord.RevokedAt = DateTime.UtcNow;

        var user = tokenRecord.User!;
        var activeCompanyId = user.TenantId;

        var jwt = await MintJwtAsync(user, activeCompanyId);
        var profile = await BuildProfileAsync(user, activeCompanyId);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

        await _db.SaveChangesAsync();

        return new AuthResult(true, Token: jwt, Profile: profile, RefreshToken: newRefreshToken);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();
        
        return token;
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

        return new InviteResult(true, Token: token, InvitationId: invitation.Id);
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
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: jwtToken, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task<AuthResult> UpdateUserAsync(Guid userId, string? displayName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return new AuthResult(false, Error: "User not found.");

        if (!string.IsNullOrWhiteSpace(displayName))
            user.DisplayName = displayName;

        await _db.SaveChangesAsync();

        var activeCompanyId = user.TenantId;
        var token = await MintJwtAsync(user, activeCompanyId);
        var profile = await BuildProfileAsync(user, activeCompanyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    // F-33: Verify email with token
    public async Task<AuthResult> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AuthResult(false, Error: "Verification token is required.");

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.VerificationToken == token);

        if (user == null)
        {
            _logger.LogWarning("Email verification failed: invalid token");
            return new AuthResult(false, Error: "Invalid or expired verification token.");
        }

        user.EmailVerified = true;
        user.VerificationToken = null; // Invalidate after use
        await _db.SaveChangesAsync();

        _logger.LogInformation("Email verified for user {Email}", user.Email);
        return new AuthResult(true);
    }

    // F-04 FIX: Validate password complexity
    private static (bool IsValid, string ErrorMessage) ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "Password is required.");

        if (password.Length < 12)
            return (false, "Password must be at least 12 characters long.");

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        var missing = new List<string>();
        if (!hasLower) missing.Add("lowercase letter");
        if (!hasUpper) missing.Add("uppercase letter");
        if (!hasDigit) missing.Add("number");
        if (!hasSpecial) missing.Add("special character");

        if (missing.Count > 0)
            return (false, $"Password must contain at least one {string.Join(", ", missing)}.");

        return (true, string.Empty);
    }
}
