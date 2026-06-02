using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Impersonacao;

[Collection(nameof(PostgresCollection))]
public sealed class TenantFilterImpersonacaoTests(PostgresContainerFixture db)
{
    [Fact]
    public async Task SaasGlobal_VeRegistrosDeTodosOsTenantsAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var insertCtx = db.CreateContext();
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantA, "Prestador-A", null));
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantB, "Prestador-B", null));
        await insertCtx.SaveChangesAsync();

        await using var readCtx = db.CreateContext();
        var nomes = await readCtx.Prestadores
            .Where(p => p.TenantId == tenantA || p.TenantId == tenantB)
            .Select(p => p.Nome)
            .ToListAsync();

        Assert.Contains("Prestador-A", nomes);
        Assert.Contains("Prestador-B", nomes);
    }

    [Fact]
    public async Task Impersonacao_VeApenasRegistrosDoTenantAtivoAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var insertCtx = db.CreateContext();
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantA, $"Prestador-A-{tenantA:N}", null));
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantB, $"Prestador-B-{tenantB:N}", null));
        await insertCtx.SaveChangesAsync();

        await using var impCtx = db.CreateImpersonationContext(tenantA);
        var itens = await impCtx.Prestadores
            .Where(p => p.TenantId == tenantA || p.TenantId == tenantB)
            .ToListAsync();

        Assert.All(itens, p => Assert.Equal(tenantA, p.TenantId));
    }

    [Fact]
    public async Task Impersonacao_NaoVeRegistrosDeOutroTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var insertCtx = db.CreateContext();
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantA, $"Prestador-A-{tenantA:N}", null));
        insertCtx.Prestadores.Add(global::App.Catalog.Prestador.Create(tenantB, $"Prestador-B-{tenantB:N}", null));
        await insertCtx.SaveChangesAsync();

        await using var impCtx = db.CreateImpersonationContext(tenantA);
        var temB = await impCtx.Prestadores
            .AnyAsync(p => p.TenantId == tenantB);

        Assert.False(temB);
    }
}
