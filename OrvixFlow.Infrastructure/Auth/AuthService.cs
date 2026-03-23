using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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
        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
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
            Role = "Owner"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = MintJwt(user, tenant);
        return new AuthResult(true, Token: token, Profile: BuildProfile(user, tenant));
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.OAuthProvider == "local");

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, Error: "Invalid email or password.");

        var token = MintJwt(user, user.Tenant!);
        return new AuthResult(true, Token: token, Profile: BuildProfile(user, user.Tenant!));
    }

    public async Task<AuthResult> ProvisionOAuthUserAsync(string email, string displayName, string provider, string externalId)
    {
        var normalizedEmail = email.ToLowerInvariant();

        // Idempotent: return existing user if OAuth account already provisioned
        var existing = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.OAuthProvider == provider && u.ExternalId == externalId);

        if (existing != null)
        {
            var existingToken = MintJwt(existing, existing.Tenant!);
            return new AuthResult(true, Token: existingToken, Profile: BuildProfile(existing, existing.Tenant!));
        }

        // Check if email already exists under a different provider – link accounts
        var byEmail = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (byEmail != null)
        {
            // Upgrade account to OAuth if it was previously local
            byEmail.OAuthProvider = provider;
            byEmail.ExternalId = externalId;
            await _db.SaveChangesAsync();
            var linkedToken = MintJwt(byEmail, byEmail.Tenant!);
            return new AuthResult(true, Token: linkedToken, Profile: BuildProfile(byEmail, byEmail.Tenant!));
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
            Role = "Owner"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = MintJwt(user, tenant);
        return new AuthResult(true, Token: token, Profile: BuildProfile(user, tenant));
    }

    private string MintJwt(User user, Tenant tenant)
    {
        var secret = _config["Jwt:Secret"] ?? throw new Exception("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("TenantId", user.TenantId.ToString()),
            new("Plan", tenant.Plan),
            new("Role", user.Role),
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

    private static UserProfile BuildProfile(User user, Tenant tenant) =>
        new(user.Id, user.TenantId, user.Email, user.DisplayName, user.Role, tenant.Plan);
}
