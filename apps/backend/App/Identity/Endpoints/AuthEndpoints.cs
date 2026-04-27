using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Identity.Endpoints;

internal static class AuthEndpoints
{
    // Origins allowed as redirect targets after OAuth completes.
    // Angular dev servers on 4200 (admin-web) and 4201 (medico-pwa), plus relative paths for production.
    private static readonly string[] _allowedReturnUrlPrefixes =
        ["/admin/", "/app/", "http://localhost:4200/", "http://localhost:4201/"];

    internal static void MapAuthEndpoints(this WebApplication app)
    {
        // Initiates the Google OAuth redirect flow.
        // Pass returnUrl to know where to redirect back to after login:
        //   /api/v1/auth/google?returnUrl=/admin/auth/callback   (admin-web)
        //   /api/v1/auth/google?returnUrl=/app/auth/callback     (medico-pwa)
        // When returnUrl is absent, the finalize endpoint returns JSON (useful for Swagger/httpie).
        app.MapGet("/api/v1/auth/google", InitiateGoogleLogin)
            .AllowAnonymous();

        // Called by the Google middleware after it finishes the code exchange.
        // Reads the external cookie, calls AuthService, and either redirects or returns JSON.
        app.MapGet("/api/v1/auth/google/finalize", FinalizeGoogleLoginAsync)
            .AllowAnonymous();

        app.MapPost("/api/v1/auth/refresh", RefreshAsync)
            .AllowAnonymous();

        app.MapPost("/api/v1/auth/logout", LogoutAsync)
            .RequireAuthorization();
    }

    private static ChallengeHttpResult InitiateGoogleLogin(
        HttpContext ctx, string? returnUrl)
    {
        var finalizeUrl = "/api/v1/auth/google/finalize";
        if (returnUrl is not null)
        {
            finalizeUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var properties = new AuthenticationProperties { RedirectUri = finalizeUrl };
        return TypedResults.Challenge(properties, ["Google"]);
    }

    private static async Task<IResult> FinalizeGoogleLoginAsync(
        HttpContext ctx, AuthService authService, string? returnUrl)
    {
        var authResult = await ctx.AuthenticateAsync("External");
        if (!authResult.Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Autenticação Google falhou.");
        }

        var googleId = authResult.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = authResult.Principal!.FindFirstValue(ClaimTypes.Email)!;

        await ctx.SignOutAsync("External");

        var result = await authService.ProcessGoogleCallbackAsync(googleId, email, ctx.RequestAborted);

        if (result.IsFailure)
        {
            var status = result.Error is UnauthorizedError
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;

            return Results.Problem(statusCode: status, detail: result.Error!.Message);
        }

        var tokens = result.Value!;

        if (returnUrl is not null && IsAllowedReturnUrl(returnUrl))
        {
            var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            var redirectTarget =
                $"{returnUrl}{separator}" +
                $"accessToken={Uri.EscapeDataString(tokens.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(tokens.RefreshToken)}" +
                $"&expiresIn={tokens.ExpiresIn}";

            return Results.Redirect(redirectTarget);
        }

        return Results.Ok(new
        {
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresIn
        });
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest body, AuthService authService, CancellationToken ct)
    {
        var result = await authService.RefreshTokenAsync(body.RefreshToken, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "Token inválido.");
        }

        var t = result.Value!;
        return Results.Ok(new { t.AccessToken, t.RefreshToken, t.ExpiresIn });
    }

    private static async Task<IResult> LogoutAsync(
        ICurrentUser currentUser, AuthService authService, CancellationToken ct)
    {
        await authService.LogoutAsync(currentUser.UserId, ct);
        return Results.NoContent();
    }

    private static bool IsAllowedReturnUrl(string url) =>
        _allowedReturnUrlPrefixes.Any(p => url.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}

internal sealed record RefreshRequest(string RefreshToken);
