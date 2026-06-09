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

    [Fact]
    public void Activate_SuspendedTenant_SetsStatusToAtivo()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Suspend();
        tenant.Activate();
        Assert.Equal(TenantStatus.Ativo, tenant.Status);
    }

    [Fact]
    public void Activate_CancelledTenant_SetsStatusToAtivo()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Cancel();
        tenant.Activate();
        Assert.Equal(TenantStatus.Ativo, tenant.Status);
    }

    [Fact]
    public void Rename_UpdatesNameWithTrim()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.Rename("  Nova Clínica  ");
        Assert.Equal("Nova Clínica", tenant.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_ThrowsWhenNameIsEmptyOrWhitespace(string name)
    {
        var tenant = Tenant.Create("Clínica ABC");
        Assert.Throws<ArgumentException>(() => tenant.Rename(name));
    }

    [Fact]
    public void SetLogoKey_SetsLogoKey_AndClearLogoKey_SetsNull()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.SetLogoKey("logos/tenant-123.png");
        Assert.Equal("logos/tenant-123.png", tenant.LogoKey);
        tenant.ClearLogoKey();
        Assert.Null(tenant.LogoKey);
    }

    [Fact]
    public void Create_DeveIniciarComListaVazia()
    {
        var tenant = Tenant.Create("Clínica ABC");
        Assert.Empty(tenant.CodigosNaoRecorriveis);
    }

    [Fact]
    public void DefinirCodigosNaoRecorriveis_DeveArmazenarLista()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.DefinirCodigosNaoRecorriveis(["10101012", "20202024"]);
        Assert.Equal(["10101012", "20202024"], tenant.CodigosNaoRecorriveis);
    }

    [Fact]
    public void DefinirCodigosNaoRecorriveis_DeveRemoverDuplicatasEEspacos()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.DefinirCodigosNaoRecorriveis([" 10101012 ", "10101012"]);
        Assert.Equal(["10101012"], tenant.CodigosNaoRecorriveis);
    }

    [Fact]
    public void DefinirCodigosNaoRecorriveis_DeveIgnorarVazios()
    {
        var tenant = Tenant.Create("Clínica ABC");
        tenant.DefinirCodigosNaoRecorriveis(["", "  "]);
        Assert.Empty(tenant.CodigosNaoRecorriveis);
    }
}
