using App;
using App.Identity;

namespace Identity.Tests.Entities;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Create_SetsIsRevokedFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(token.CreatedAt, before, after);
    }

    [Fact]
    public void Create_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var token = RefreshToken.Create(userId, "myhash", expiresAt);
        Assert.Equal(userId, token.UserId);
        Assert.Equal("myhash", token.TokenHash);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Null(token.ReplacedByTokenId);
        Assert.NotEqual(Guid.Empty, token.Id);
    }

    [Fact]
    public void Revoke_WhenNotRevoked_Succeeds()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        var result = token.Revoke();
        Assert.True(result.IsSuccess);
        Assert.True(token.IsRevoked);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoke_WithReplacedByTokenId_SetsReplacedByTokenId()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        var result = token.Revoke("new-token-id");
        Assert.True(result.IsSuccess);
        Assert.Equal("new-token-id", token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ReturnsConflictError()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        token.Revoke();
        var result = token.Revoke();
        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_DoesNotChangeReplacedByTokenId()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash123", DateTimeOffset.UtcNow.AddDays(7));
        token.Revoke("first-replacement");
        token.Revoke("second-replacement");
        Assert.Equal("first-replacement", token.ReplacedByTokenId);
    }
}
