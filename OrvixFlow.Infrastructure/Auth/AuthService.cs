using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;
    private readonly ICompanyBootstrapService _companyBootstrapService;

    public AuthService(
        AppDbContext db, 
        IConfiguration config, 
        ILogger<AuthService> logger,
        IEmailService emailService,
        ICompanyBootstrapService companyBootstrapService)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _emailService = emailService;
        _companyBootstrapService = companyBootstrapService;
    }

    public AuthService(
        AppDbContext db,
        IConfiguration config,
        ILogger<AuthService> logger,
        IEmailService emailService)
        : this(db, config, logger, emailService, new CompanyBootstrapService(db, NullLogger<CompanyBootstrapService>.Instance))
    {
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

        var useTransaction = _db.Database.IsRelational();
        await using var transaction = useTransaction
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var tenant = new Tenant
        {
            Name = displayName,
            Plan = "Free",
            SubscriptionStatus = "Active"
        };
        _db.Tenants.Add(tenant);

        var verificationToken = GenerateOpaqueToken();
        var user = new User
        {
            TenantId = tenant.Id,
            Email = normalizedEmail,
            DisplayName = displayName,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailVerified = false,
            VerificationToken = ComputeTokenHash(verificationToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddHours(48),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _companyBootstrapService.EnsureOwnerBootstrapAsync(user.Id, tenant.Id);
        await _companyBootstrapService.EnsureDefaultSubscriptionAsync(tenant.Id);

        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var verificationLink = $"{frontendUrl}/verify?token={verificationToken}";
        QueueEmailNotification(
            tenant.Id,
            user.Email,
            "Verify your OrvixFlow account",
            $@"<div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                <h2 style='color: #6366f1; text-align: center;'>Welcome to OrvixFlow</h2>
                <p>Hello {user.DisplayName},</p>
                <p>Thank you for registering. Please click the button below to verify your email address and activate your account:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{verificationLink}' style='background-color: #6366f1; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Verify Email Address</a>
                </div>
                <p style='font-size: 0.875rem; color: #64748b;'>This link expires in 48 hours.</p>
                <p style='font-size: 0.875rem; color: #64748b;'>If the button doesn't work, copy and paste this link into your browser:</p>
                <p style='font-size: 0.875rem; color: #6366f1; word-break: break-all;'>{verificationLink}</p>
                <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 0.75rem; color: #94a3b8; text-align: center;'>&copy; {DateTime.UtcNow.Year} OrvixFlow Enterprise. All rights reserved.</p>
            </div>");

        await _db.SaveChangesAsync();
        if (transaction != null)
            await transaction.CommitAsync();

        _logger.LogInformation("Queued verification email for {Email}", normalizedEmail);

        // F-33: Return success without tokens, user must verify email before logging in
        return new AuthResult(true);
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

        return await CreateAuthenticatedSessionAsync(
            user,
            user.TenantId,
            noCompanyScopeLogMessage: $"Login continuing without company scope for {normalizedEmail}");
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
            await _companyBootstrapService.EnsureOwnerBootstrapAsync(existing.Id, existing.TenantId);
            await _companyBootstrapService.EnsureDefaultSubscriptionAsync(existing.TenantId);
            return await CreateAuthenticatedSessionAsync(
                existing,
                existing.TenantId,
                noCompanyScopeLogMessage: $"OAuth provision continuing without company scope for existing user {existing.Id}");
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

                await _companyBootstrapService.EnsureOwnerBootstrapAsync(byEmail.Id, byEmail.TenantId);
                return await CreateAuthenticatedSessionAsync(
                    byEmail,
                    byEmail.TenantId,
                    noCompanyScopeLogMessage: $"OAuth provision continuing without company scope for migrated user {byEmail.Id}");
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
            EmailVerified = true // OAuth users provided by trusted providers are pre-verified
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _companyBootstrapService.EnsureOwnerBootstrapAsync(user.Id, tenant.Id);
        await _companyBootstrapService.EnsureDefaultSubscriptionAsync(tenant.Id);

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

        var company = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == companyId);

        if (company == null || company.LifecycleStatus == "Archived")
            return new AuthResult(false, Error: "This company has been archived and cannot be accessed.");

        _logger.LogInformation("[DEBUG][CompanySwitch][AuthService] Membership validated successfully with role: {Role}. Minting new JWT.", membership.CompanyRole);

        var token = await MintJwtAsync(user, companyId);
        var profile = await BuildProfileAsync(user, companyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    private async Task<string> MintJwtAsync(User user, Guid? activeCompanyId)
    {
        var secret = _config["Jwt:Secret"] ?? throw new Exception("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Global roles (platform-level) always override company-level roles.
        // A SuperAdmin or InternalOperator should have their global role in the JWT
        // regardless of what their UserCompanyMembership.CompanyRole says.
        var parsedUserRole = UserRoleExtensions.ParseRole(user.Role);
        var roleClaimValue = parsedUserRole.IsPlatformAdmin()
            ? parsedUserRole.ToClaimValue()
            : string.Empty;
        var planClaimValue = "Free";

        if (activeCompanyId.HasValue)
        {
            var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId.Value);
            if (company.LifecycleStatus == "Archived")
                throw new InvalidOperationException("Cannot mint a session for an archived company.");

            planClaimValue = company.Plan;

            if (!parsedUserRole.IsPlatformAdmin())
            {
                var roleString = await _db.UserCompanyMemberships
                    .IgnoreQueryFilters()
                    .Where(m => m.UserId == user.Id && m.CompanyId == activeCompanyId.Value && m.Status == "Active")
                    .Select(m => m.CompanyRole)
                    .FirstOrDefaultAsync() ?? user.Role;
                roleClaimValue = UserRoleExtensions.ParseRole(roleString).ToClaimValue();
            }
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("Plan", planClaimValue),
            new("Role", roleClaimValue),
            new("DisplayName", user.DisplayName)
        };

        if (activeCompanyId.HasValue)
        {
            claims.Add(new Claim("TenantId", activeCompanyId.Value.ToString()));
            claims.Add(new Claim("ActiveCompanyId", activeCompanyId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "orvixflow",
            audience: _config["Jwt:Audience"] ?? "orvixflow-web",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60), // F-01 FIX: Shortened from 7 days to 60 minutes to reduce token exposure window
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserProfile> BuildProfileAsync(User user, Guid? activeCompanyId)
    {
        var memberships = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id && m.Status == "Active")
            .Join(
                _db.Tenants.IgnoreQueryFilters().Where(t => t.LifecycleStatus != "Archived"),
                m => m.CompanyId,
                c => c.Id,
                (m, c) => new CompanyMembershipSummary(c.Id, c.Name, m.CompanyRole))
            .ToListAsync();

        var globalRole = string.IsNullOrWhiteSpace(user.Role) ? null : user.Role;
        var parsedGlobalRole = UserRoleExtensions.ParseRole(user.Role);
        var activeRole = parsedGlobalRole.IsPlatformAdmin()
            ? parsedGlobalRole.ToClaimValue()
            : string.Empty;
        var plan = "Free";

        if (activeCompanyId.HasValue)
        {
            var company = await _db.Tenants.FirstAsync(t => t.Id == activeCompanyId.Value);
            plan = company.Plan;
            activeRole = memberships.FirstOrDefault(m => m.CompanyId == activeCompanyId.Value)?.Role ?? activeRole;
        }

        return new UserProfile(
            user.Id,
            activeCompanyId,
            activeCompanyId,
            user.Email,
            user.DisplayName,
            activeRole,
            plan,
            memberships,
            globalRole
        );
    }

    public async Task<AuthResult> RefreshSessionAsync(string refreshToken, Guid? activeCompanyId = null)
    {
        var tokenRecord = await FindRefreshTokenAsync(refreshToken);

        if (tokenRecord == null)
            return new AuthResult(false, Error: "Invalid refresh token.");

        if (tokenRecord.RevokedAt != null)
            return new AuthResult(false, Error: "Refresh token has been revoked.");

        if (tokenRecord.IsExpired)
            return new AuthResult(false, Error: "Refresh token has expired.");

        // Revoke the old token
        tokenRecord.RevokedAt = DateTime.UtcNow;

        var user = tokenRecord.User!;

        // FIX: Determine the active company based on provided context or validate existing company
        return await CreateAuthenticatedSessionAsync(
            user,
            activeCompanyId,
            tokenRecord.FamilyId,
            $"RefreshSession continuing without company scope for user {user.Id}; requested company {activeCompanyId}");
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, Guid? familyId = null)
    {
        var lookupKey = Guid.NewGuid().ToString("N");
        var secret = GenerateOpaqueToken();
        var token = $"{lookupKey}.{secret}";
             
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            LookupKey = lookupKey,
            FamilyId = familyId ?? Guid.NewGuid(),
            Token = ComputeTokenHash(token),
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
        if (!role.IsCompanyScopedRole() || !UserRoleExtensions.CompanyRoleNames.Contains(request.AssignedRole))
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

        var token = GenerateOpaqueToken();

        var invitation = new Invitation
        {
            Email          = request.Email.ToLowerInvariant(),
            CompanyId      = request.CompanyId,
            AssignedRole   = role.ToClaimValue(),
            DepartmentId   = request.DepartmentId,
            Token          = ComputeTokenHash(token),
            Status         = "Pending",
            InvitedByUserId = request.InvitedByUserId,
            ExpiresAt      = DateTime.UtcNow.AddDays(7),
        };
        _db.Invitations.Add(invitation);

        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var inviteLink = $"{frontendUrl}/invite?token={token}";
        QueueEmailNotification(
            request.CompanyId,
            request.Email,
            "Verify your OrvixFlow invitation",
            $@"<div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                <h2 style='color: #6366f1; text-align: center;'>Welcome to OrvixFlow</h2>
                <p>Hello,</p>
                <p>You have been invited to join a company workspace in OrvixFlow as <strong>{role.ToClaimValue()}</strong>. Please click the button below to accept your invitation and finish setting up your account:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{inviteLink}' style='background-color: #6366f1; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Accept Invitation</a>
                </div>
                <p style='font-size: 0.875rem; color: #64748b;'>This invitation expires in 7 days.</p>
                <p style='font-size: 0.875rem; color: #64748b;'>If the button doesn't work, copy and paste this link into your browser:</p>
                <p style='font-size: 0.875rem; color: #6366f1; word-break: break-all;'>{inviteLink}</p>
                <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 0.75rem; color: #94a3b8; text-align: center;'>&copy; {DateTime.UtcNow.Year} OrvixFlow Enterprise. All rights reserved.</p>
            </div>");
        await _db.SaveChangesAsync();

        return new InviteResult(true, Token: token, InvitationId: invitation.Id);
    }

    public async Task<AuthResult> AcceptInvitationAsync(string token, string? displayName, string? password)
    {
        var tokenHash = ComputeTokenHash(token);
        var invitation = await _db.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == tokenHash && i.Status == "Pending");

        if (invitation == null)
        {
            invitation = await _db.Invitations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Token == token && i.Status == "Pending");

            if (invitation != null)
            {
                invitation.Token = tokenHash;
            }
        }

        if (invitation == null)
            return new AuthResult(false, Error: "Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            await _db.SaveChangesAsync();
            return new AuthResult(false, Error: "Invitation has expired. Please request a new one.");
        }

        var invitedRole = UserRoleExtensions.ParseRole(invitation.AssignedRole);
        if (!invitedRole.IsCompanyScopedRole() || invitedRole == UserRole.CompanyOwner)
        {
            invitation.Status = "Revoked";
            await _db.SaveChangesAsync();
            return new AuthResult(false, Error: "Invitation is no longer valid. Please request a new one.");
        }

        if (invitation.DepartmentId.HasValue)
        {
            var departmentIsValid = await _db.Departments
                .IgnoreQueryFilters()
                .AnyAsync(d => d.Id == invitation.DepartmentId.Value && d.CompanyId == invitation.CompanyId);

            if (!departmentIsValid)
            {
                invitation.Status = "Revoked";
                await _db.SaveChangesAsync();
                return new AuthResult(false, Error: "Invitation is no longer valid. Please request a new one.");
            }
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
            if (string.IsNullOrWhiteSpace(password))
                return new AuthResult(false, Error: "password is required for new local accounts.");

            var passwordValidation = ValidatePasswordComplexity(password);
            if (!passwordValidation.IsValid)
                return new AuthResult(false, Error: passwordValidation.ErrorMessage);

            user = new User
            {
                Email        = normalizedEmail,
                DisplayName  = displayName,
                OAuthProvider = "local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                TenantId     = invitation.CompanyId,
                EmailVerified = true,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (!user.EmailVerified)
        {
            user.EmailVerified = true;
        }

        // Apply company membership with pre-assigned role
        var membership = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.CompanyId == invitation.CompanyId);

        if (membership == null)
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
        else
        {
            membership.CompanyRole = invitedRole.ToClaimValue();
            membership.Status = "Active";
            membership.InvitedAt = invitation.CreatedAt;
            membership.JoinedAt = DateTime.UtcNow;
            membership.InvitedByUserId = invitation.InvitedByUserId;
        }

        // Apply department membership if specified
        if (invitation.DepartmentId.HasValue)
        {
            var departmentMembership = await _db.UserDepartmentMemberships
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.UserId == user.Id
                                          && m.CompanyId == invitation.CompanyId
                                          && m.DepartmentId == invitation.DepartmentId.Value);
            if (departmentMembership == null)
            {
                _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
                {
                    UserId       = user.Id,
                    CompanyId    = invitation.CompanyId,
                    DepartmentId = invitation.DepartmentId.Value,
                    DepartmentRole = invitedRole.ToDepartmentRoleValue(),
                    Status       = "Active",
                });
            }
            else
            {
                departmentMembership.DepartmentRole = invitedRole.ToDepartmentRoleValue();
                departmentMembership.Status = "Active";
            }
        }

        invitation.Status = "Accepted";
        await _db.SaveChangesAsync();

        var jwtToken = await MintJwtAsync(user, invitation.CompanyId);
        var profile  = await BuildProfileAsync(user, invitation.CompanyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(true, Token: jwtToken, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task<AuthResult> UpdateUserAsync(Guid userId, string? displayName, Guid? activeCompanyId = null)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return new AuthResult(false, Error: "User not found.");

        if (!string.IsNullOrWhiteSpace(displayName))
            user.DisplayName = displayName;

        await _db.SaveChangesAsync();

        return await CreateAuthenticatedSessionAsync(user, activeCompanyId);
    }


    private async Task<AuthResult> CreateAuthenticatedSessionAsync(
        User user,
        Guid? requestedCompanyId,
        Guid? refreshTokenFamilyId = null,
        string? noCompanyScopeLogMessage = null)
    {
        var resolvedCompanyId = await ResolveActiveCompanyIdAsync(user, requestedCompanyId);
        if (resolvedCompanyId == null && !string.IsNullOrWhiteSpace(noCompanyScopeLogMessage))
        {
            _logger.LogInformation(noCompanyScopeLogMessage);
        }

        var token = await MintJwtAsync(user, resolvedCompanyId);
        var profile = await BuildProfileAsync(user, resolvedCompanyId);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, refreshTokenFamilyId);
        return new AuthResult(true, Token: token, Profile: profile, RefreshToken: refreshToken);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var tokenRecord = await FindRefreshTokenAsync(refreshToken, includeUser: false);

        if (tokenRecord == null || tokenRecord.RevokedAt != null)
            return;

        await RevokeRefreshTokenFamilyAsync(tokenRecord.FamilyId);
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();

        if (activeTokens.Count == 0)
            return;

        foreach (var token in activeTokens)
            token.RevokedAt = now;

        await _db.SaveChangesAsync();
    }

    // F-33: Verify email with token
    public async Task<AuthResult> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AuthResult(false, Error: "Verification token is required.");

        var tokenHash = ComputeTokenHash(token);

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.VerificationToken == tokenHash);

        if (user == null)
        {
            user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user != null)
            {
                user.VerificationToken = tokenHash;
            }
        }

        if (user == null)
        {
            _logger.LogWarning("Email verification failed: invalid token");
            return new AuthResult(false, Error: "Invalid or expired verification token.");
        }

        if (user.VerificationTokenExpiresAt.HasValue && user.VerificationTokenExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Email verification failed: expired token for {Email}", user.Email);
            user.VerificationToken = null;
            user.VerificationTokenExpiresAt = null;
            await _db.SaveChangesAsync();
            return new AuthResult(false, Error: "Invalid or expired verification token.");
        }

        user.EmailVerified = true;
        user.VerificationToken = null; // Invalidate after use
        user.VerificationTokenExpiresAt = null;
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

    private void QueueEmailNotification(Guid companyId, string recipientEmail, string subject, string body)
    {
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = recipientEmail,
            Subject = subject,
            Body = body,
            MetricType = string.Empty,
            CurrentUsage = 0,
            Limit = 0,
            Percentage = 0,
            Processed = false
        });
    }

    private static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeTokenHash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private async Task<RefreshToken?> FindRefreshTokenAsync(string presentedToken, bool includeUser = true)
    {
        IQueryable<RefreshToken> query = _db.RefreshTokens;
        if (includeUser)
        {
            query = query
                .Include(r => r.User)
                .ThenInclude(u => u!.Tenant);
        }

        var separatorIndex = presentedToken.IndexOf('.');
        if (separatorIndex > 0)
        {
            var lookupKey = presentedToken[..separatorIndex];
            var hashedToken = ComputeTokenHash(presentedToken);
            var tokenRecord = await query.FirstOrDefaultAsync(r => r.LookupKey == lookupKey);
            if (tokenRecord != null && FixedTimeEquals(tokenRecord.Token, hashedToken))
                return tokenRecord;
        }

        return await query.FirstOrDefaultAsync(r => r.LookupKey == null && r.Token == presentedToken);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private async Task RevokeRefreshTokenFamilyAsync(Guid familyId)
    {
        var now = DateTime.UtcNow;
        var familyTokens = await _db.RefreshTokens
            .Where(r => r.FamilyId == familyId && r.RevokedAt == null)
            .ToListAsync();

        if (familyTokens.Count == 0)
            return;

        foreach (var token in familyTokens)
            token.RevokedAt = now;

        await _db.SaveChangesAsync();
    }

    private async Task<Guid?> ResolveActiveCompanyIdAsync(User user, Guid? requestedCompanyId)
    {
        var parsedUserRole = UserRoleExtensions.ParseRole(user.Role);
        if (parsedUserRole.IsPlatformAdmin())
        {
            var targetCompanyId = requestedCompanyId ?? user.TenantId;
            var companyExists = await _db.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == targetCompanyId && t.LifecycleStatus != "Archived");

            return companyExists ? targetCompanyId : null;
        }

        var activeMemberships = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id && m.Status == "Active")
            .Join(_db.Tenants.IgnoreQueryFilters().Where(t => t.LifecycleStatus != "Archived"), m => m.CompanyId, t => t.Id, (m, t) => m.CompanyId)
            .ToListAsync();

        if (activeMemberships.Count == 0)
            return null;

        if (requestedCompanyId.HasValue && activeMemberships.Contains(requestedCompanyId.Value))
            return requestedCompanyId.Value;

        if (activeMemberships.Contains(user.TenantId))
            return user.TenantId;

        return activeMemberships[0];
    }
}
