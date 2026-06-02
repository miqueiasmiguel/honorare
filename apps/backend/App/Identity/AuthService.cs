using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App.Identity;

internal sealed class AuthService(AppDbContext db, IConfiguration config)
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;

    internal async Task<Result<AuthTokens>> ProcessGoogleCallbackAsync(
        string googleId, string email, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(googleId, email, ct);
        if (user is null)
        {
            return Result<AuthTokens>.Fail(new UnauthorizedError("Usuário não cadastrado."));
        }

        if (!user.IsActive)
        {
            return Result<AuthTokens>.Fail(new ForbiddenError("Usuário inativo."));
        }

        if (user.TenantId is not null)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, ct);

            if (tenant is null || tenant.Status != TenantStatus.Ativo)
            {
                return Result<AuthTokens>.Fail(new ForbiddenError("Tenant suspenso ou cancelado."));
            }
        }

        var role = DeriveRole(user);
        var accessToken = CreateAccessToken(user, role);
        var (rawRefreshToken, tokenHash) = GenerateRefreshTokenPair();

        var refreshTokenDays = _config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var refreshToken = RefreshToken.Create(
            user.Id, tokenHash, DateTimeOffset.UtcNow.AddDays(refreshTokenDays), actingTenantId: null);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var expiresIn = _config.GetValue<int>("Jwt:AccessTokenMinutes", 15) * 60;
        return Result<AuthTokens>.Ok(new AuthTokens(accessToken, rawRefreshToken, expiresIn));
    }

    internal async Task<Result<AuthTokens>> RefreshTokenAsync(
        string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.IsRevoked || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result<AuthTokens>.Fail(new UnauthorizedError("Token inválido."));
        }

        var user = await _db.Users.FindAsync([token.UserId], ct);
        if (user is null || !user.IsActive)
        {
            return Result<AuthTokens>.Fail(new UnauthorizedError("Token inválido."));
        }

        var refreshTokenDays = _config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var expiresIn = _config.GetValue<int>("Jwt:AccessTokenMinutes", 15) * 60;

        if (token.ActingTenantId is not null)
        {
            if (user.TenantId is not null)
            {
                return Result<AuthTokens>.Fail(new UnauthorizedError("Token inválido."));
            }

            var (rawNewImp, newHashImp) = GenerateRefreshTokenPair();
            var newImpToken = RefreshToken.Create(
                user.Id, newHashImp, DateTimeOffset.UtcNow.AddDays(refreshTokenDays),
                actingTenantId: token.ActingTenantId);

            token.Revoke(newImpToken.Id.ToString());
            _db.RefreshTokens.Add(newImpToken);

            var impAccessToken = CreateAccessToken(user, "SaasAdmin", tenantOverride: token.ActingTenantId);
            await _db.SaveChangesAsync(ct);

            return Result<AuthTokens>.Ok(new AuthTokens(impAccessToken, rawNewImp, expiresIn));
        }

        if (user.TenantId is not null)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, ct);
            if (tenant is null || tenant.Status != TenantStatus.Ativo)
            {
                return Result<AuthTokens>.Fail(new UnauthorizedError("Token inválido."));
            }
        }

        var (rawNew, newHash) = GenerateRefreshTokenPair();
        var newToken = RefreshToken.Create(
            user.Id, newHash, DateTimeOffset.UtcNow.AddDays(refreshTokenDays), actingTenantId: null);

        token.Revoke(newToken.Id.ToString());
        _db.RefreshTokens.Add(newToken);

        var role = DeriveRole(user);
        var accessToken = CreateAccessToken(user, role);
        await _db.SaveChangesAsync(ct);

        return Result<AuthTokens>.Ok(new AuthTokens(accessToken, rawNew, expiresIn));
    }

    internal async Task<Result<AuthTokens>> CreateImpersonationTokensAsync(
        Guid saasUserId, Guid tenantId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([saasUserId], ct);
        if (user is null || !user.IsActive || user.TenantId is not null)
        {
            return Result<AuthTokens>.Fail(new ForbiddenError("Usuário não autorizado para impersonação."));
        }

        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
        {
            return Result<AuthTokens>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        var accessToken = CreateAccessToken(user, "SaasAdmin", tenantOverride: tenantId);
        var (rawRefreshToken, tokenHash) = GenerateRefreshTokenPair();

        var refreshTokenDays = _config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var refreshToken = RefreshToken.Create(
            user.Id, tokenHash, DateTimeOffset.UtcNow.AddDays(refreshTokenDays), actingTenantId: tenantId);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var expiresIn = _config.GetValue<int>("Jwt:AccessTokenMinutes", 15) * 60;
        return Result<AuthTokens>.Ok(new AuthTokens(accessToken, rawRefreshToken, expiresIn));
    }

    internal async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _db.SaveChangesAsync(ct);
    }

    // Looks up the user by GoogleId; if not found, tries by email and associates the GoogleId
    // on first login (TASK-AUTH-10). Returns null when the user does not exist at all.
    private async Task<ApplicationUser?> ResolveUserAsync(
        string googleId, string email, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId, ct);
        if (user is not null)
        {
            return user;
        }

        user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return null;
        }

        var result = user.AssociateGoogleId(googleId);
        if (result.IsFailure)
        {
            // Email matched but user has a different GoogleId — treat as unauthorized
            return null;
        }

        return user;
    }

    internal static string DeriveRole(ApplicationUser user) =>
        user.TenantId is null ? "SaasAdmin" :
        user.MedicoId is not null ? "Medico" : "TenantAdmin";

    private string CreateAccessToken(ApplicationUser user, string role, Guid? tenantOverride = null)
    {
        var secret = _config["Jwt:Secret"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var minutes = _config.GetValue<int>("Jwt:AccessTokenMinutes", 15);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Use the short JWT claim name "role" so the frontend can read it directly.
            // ClaimTypes.Role would emit the long URI form; the inbound mapper converts
            // "role" → ClaimTypes.Role on validation, so RequireRole() still works.
            new("role", role)
        };

        var effectiveTenantId = tenantOverride ?? user.TenantId;
        if (effectiveTenantId is not null)
        {
            claims.Add(new Claim("tenant_id", effectiveTenantId.Value.ToString()));
        }

        if (tenantOverride is not null)
        {
            claims.Add(new Claim("act_as_saas", "true"));
        }

        if (user.MedicoId is not null)
        {
            claims.Add(new Claim("medico_id", user.MedicoId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string raw, string hash) GenerateRefreshTokenPair()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashToken(raw));
    }

    private static string HashToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}

internal sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
