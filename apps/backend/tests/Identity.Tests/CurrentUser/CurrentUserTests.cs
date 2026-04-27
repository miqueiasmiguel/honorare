namespace Identity.Tests.CurrentUser;

public sealed class CurrentUserTests
{
    [Fact]
    public void SaasAdmin_HasNullTenantId_AndIsSaasAdminTrue()
    {
        var userId = Guid.NewGuid();
        var accessor = FakeHttpContextAccessor.ForSaasAdmin(userId);
        var currentUser = new App.Identity.CurrentUser(accessor);

        Assert.True(currentUser.IsAuthenticated);
        Assert.True(currentUser.IsSaasAdmin);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Null(currentUser.TenantId);
        Assert.Null(currentUser.MedicoId);
    }

    [Fact]
    public void TenantAdmin_HasCorrectTenantId_AndIsSaasAdminFalse()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var accessor = FakeHttpContextAccessor.ForTenantAdmin(userId, tenantId);
        var currentUser = new App.Identity.CurrentUser(accessor);

        Assert.True(currentUser.IsAuthenticated);
        Assert.False(currentUser.IsSaasAdmin);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(tenantId, currentUser.TenantId);
        Assert.Null(currentUser.MedicoId);
    }

    [Fact]
    public void Medico_HasTenantIdAndMedicoId()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var medicoId = Guid.NewGuid();
        var accessor = FakeHttpContextAccessor.ForMedico(userId, tenantId, medicoId);
        var currentUser = new App.Identity.CurrentUser(accessor);

        Assert.True(currentUser.IsAuthenticated);
        Assert.False(currentUser.IsSaasAdmin);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(tenantId, currentUser.TenantId);
        Assert.Equal(medicoId, currentUser.MedicoId);
    }

    [Fact]
    public void Unauthenticated_IsAuthenticatedFalse_AllIdsDefault()
    {
        var accessor = FakeHttpContextAccessor.ForUnauthenticated();
        var currentUser = new App.Identity.CurrentUser(accessor);

        Assert.False(currentUser.IsAuthenticated);
        Assert.False(currentUser.IsSaasAdmin);
        Assert.Equal(Guid.Empty, currentUser.UserId);
        Assert.Null(currentUser.TenantId);
        Assert.Null(currentUser.MedicoId);
    }
}
