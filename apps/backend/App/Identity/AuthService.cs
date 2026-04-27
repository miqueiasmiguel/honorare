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
            user.Id, tokenHash, DateTimeOffset.UtcNow.AddDays(refreshTokenDays));
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var expiresIn = _config.GetValue<int>("Jwt:AccessTokenMinutes", 15) * 60;
        return Result<AuthTokens>.Ok(new AuthTokens(accessToken, rawRefreshToken, expiresIn));
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

    private string CreateAccessToken(ApplicationUser user, string role)
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
            new(ClaimTypes.Role, role)
        };

        if (user.TenantId is not null)
        {
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
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
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return (raw, hash);
    }
}

internal sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
