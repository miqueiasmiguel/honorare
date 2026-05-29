using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.TabelaOrdemOperadora;

[Collection(nameof(PostgresCollection))]
public sealed class TabelaOrdemOperadoraTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, CatalogService service) BuildTenant(Guid tenantId)
    {
        var user = new FakeOrdemUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        return (ctx, new CatalogService(ctx, user));
    }

    private static async Task<Guid> SeedOperadoraAsync(CatalogService service)
    {
        var result = await service.CriarAsync(
            new CriarOperadoraCommand("UNIMED TEST", null, null, TipoRuleSet.Unimed));
        return result.Value!.Id;
    }

    [Fact]
    public async Task SalvarTabela_NovosRegistros_PersisteTodosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        var itens = new List<SalvarOrdemItem>
        {
            new(1, TipoViaOrdem.MesmaVia, 1.00m),
            new(2, TipoViaOrdem.MesmaVia, 0.50m),
            new(3, TipoViaOrdem.MesmaVia, 0.40m),
            new(4, TipoViaOrdem.MesmaVia, 0.30m),
            new(5, TipoViaOrdem.MesmaVia, 0.20m),
            new(6, TipoViaOrdem.MesmaVia, 0.10m),
        };

        await service.SalvarTabelaOrdemAsync(oid, itens);

        await using var verify = db.CreateContext();
        var count = await verify.TabelasOrdemOperadora
            .IgnoreQueryFilters()
            .CountAsync(t => t.OperadoraId == oid);
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task SalvarTabela_UpsertExistente_NaoDuplicaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        await service.SalvarTabelaOrdemAsync(oid,
            [new SalvarOrdemItem(2, TipoViaOrdem.MesmaVia, 0.50m)]);

        await service.SalvarTabelaOrdemAsync(oid,
            [new SalvarOrdemItem(2, TipoViaOrdem.MesmaVia, 0.60m)]);

        await using var verify = db.CreateContext();
        var entries = await verify.TabelasOrdemOperadora
            .IgnoreQueryFilters()
            .Where(t => t.OperadoraId == oid
                     && t.NumeroProcedimento == 2
                     && t.TipoVia == TipoViaOrdem.MesmaVia)
            .ToListAsync();

        Assert.Single(entries);
        Assert.Equal(0.60m, entries[0].Percentual);
    }

    [Fact]
    public async Task ResolverPercentual_TabelaExiste_RetornaPercentualConfiguradoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        await service.SalvarTabelaOrdemAsync(oid,
            [new SalvarOrdemItem(3, TipoViaOrdem.MesmaVia, 0.40m)]);

        var result = await service.ResolverPercentualOrdemAsync(oid, 3, TipoViaOrdem.MesmaVia);
        Assert.Equal(0.40m, result);
    }

    [Fact]
    public async Task ResolverPercentual_TabelaNaoExiste_RetornaPadraoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        var resultMesma = await service.ResolverPercentualOrdemAsync(oid, 2, TipoViaOrdem.MesmaVia);
        var resultDif = await service.ResolverPercentualOrdemAsync(oid, 2, TipoViaOrdem.ViaDiferente);

        Assert.Equal(0.50m, resultMesma);
        Assert.Equal(0.70m, resultDif);
    }

    [Fact]
    public async Task ResolverPercentual_AlemUltimaPosicao_RetornaUltimoDefinidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        var itens = Enumerable.Range(1, 6)
            .Select(n => new SalvarOrdemItem(n, TipoViaOrdem.MesmaVia, n == 6 ? 0.10m : 1.00m / n))
            .ToList();
        await service.SalvarTabelaOrdemAsync(oid, itens);

        var result = await service.ResolverPercentualOrdemAsync(oid, 9, TipoViaOrdem.MesmaVia);
        Assert.Equal(0.10m, result);
    }

    [Fact]
    public async Task ExcluirTabela_Remove_TodasAsColunasDaOperadoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildTenant(tenantId);
        await using var _ = ctx;

        var oid = await SeedOperadoraAsync(service);

        var itens = Enumerable.Range(1, 6)
            .Select(n => new SalvarOrdemItem(n, TipoViaOrdem.MesmaVia, 0.10m))
            .ToList();
        await service.SalvarTabelaOrdemAsync(oid, itens);

        await service.ExcluirTabelaOrdemAsync(oid);

        await using var verify = db.CreateContext();
        var count = await verify.TabelasOrdemOperadora
            .IgnoreQueryFilters()
            .CountAsync(t => t.OperadoraId == oid);
        Assert.Equal(0, count);
    }
}

file sealed class FakeOrdemUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
