using App.Identity;

namespace Identity.Tests.Entities;

public sealed class TenantTests
{
    [Fact]
    public void Create_SetsStatusAtivo()
    {
        var tenant = Tenant.Create("Clínica ABC");
        Assert.Equal(TenantStatus.Ativo, tenant.Status);
    }

    [Fact]
    public void Create_SetsName()
    {
        var tenant = Tenant.Create("Clínica ABC");
        Assert.Equal("Clínica ABC", tenant.Name);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Clínica ABC");
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(tenant.CreatedAt, before, after);
    }

    [Fact]
    public void Create_AssignsNewId()
    {
        var t1 = Tenant.Create("A");
        var t2 = Tenant.Create("B");
        Assert.NotEqual(t1.Id, t2.Id);
        Assert.NotEqual(Guid.Empty, t1.Id);
    }

    [Fact]
    public void Suspend_ChangesStatusToSuspenso()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Suspend();
        Assert.Equal(TenantStatus.Suspenso, tenant.Status);
    }

    [Fact]
    public void Cancel_ChangesStatusToCancelado()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Cancel();
        Assert.Equal(TenantStatus.Cancelado, tenant.Status);
    }

    [Fact]
    public void Cancel_CalledTwice_IsIdempotent()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Cancel();
        tenant.Cancel();
        Assert.Equal(TenantStatus.Cancelado, tenant.Status);
    }

    [Fact]
    public void Suspend_AfterCancel_ChangesStatusToSuspenso()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Cancel();
        tenant.Suspend();
        Assert.Equal(TenantStatus.Suspenso, tenant.Status);
    }
}
