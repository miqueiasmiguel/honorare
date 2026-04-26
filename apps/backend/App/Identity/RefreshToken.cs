namespace App.Identity;

internal sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = expiresAt,
        CreatedAt = DateTimeOffset.UtcNow,
        IsRevoked = false
    };

    public Result Revoke(string? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return Result.Fail(new ConflictError("Token já foi revogado."));
        }
        IsRevoked = true;
        ReplacedByTokenId = replacedByTokenId;
        return Result.Ok();
    }
}
