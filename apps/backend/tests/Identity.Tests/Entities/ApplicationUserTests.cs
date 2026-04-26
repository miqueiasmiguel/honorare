using App;
using App.Identity;

namespace Identity.Tests.Entities;

public sealed class ApplicationUserTests
{
    [Fact]
    public void Create_SetsIsActiveTrue()
    {
        var user = ApplicationUser.Create("test@example.com");
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create("test@example.com");
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(user.CreatedAt, before, after);
    }

    [Fact]
    public void Create_SetsEmailAndUserName()
    {
        var user = ApplicationUser.Create("test@example.com");
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("test@example.com", user.UserName);
    }

    [Fact]
    public void Create_WithTenantIdAndMedicoId_SetsProperties()
    {
        var tenantId = Guid.NewGuid();
        var medicoId = Guid.NewGuid();
        var user = ApplicationUser.Create("medico@example.com", tenantId, medicoId);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(medicoId, user.MedicoId);
    }

    [Fact]
    public void Create_SaasAdmin_HasNullTenantIdAndMedicoId()
    {
        var user = ApplicationUser.Create("admin@example.com");
        Assert.Null(user.TenantId);
        Assert.Null(user.MedicoId);
    }

    [Fact]
    public void AssociateGoogleId_WhenNotSet_Succeeds()
    {
        var user = ApplicationUser.Create("test@example.com");
        var result = user.AssociateGoogleId("google-123");
        Assert.True(result.IsSuccess);
        Assert.Equal("google-123", user.GoogleId);
    }

    [Fact]
    public void AssociateGoogleId_WhenAlreadySet_ReturnsConflictError()
    {
        var user = ApplicationUser.Create("test@example.com");
        user.AssociateGoogleId("google-123");
        var result = user.AssociateGoogleId("google-456");
        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public void AssociateGoogleId_WhenAlreadySet_DoesNotChangeGoogleId()
    {
        var user = ApplicationUser.Create("test@example.com");
        user.AssociateGoogleId("google-123");
        user.AssociateGoogleId("google-456");
        Assert.Equal("google-123", user.GoogleId);
    }
}
