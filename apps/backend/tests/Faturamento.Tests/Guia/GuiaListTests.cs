using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaListTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeListTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var prestador = Prestador.Create(tenantId, "Dr. List " + uid, null);
        var operadora = Operadora.Create(tenantId, "UNIMED List " + uid, null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, uid, "Paciente List");
        var procedimento = Procedimento.Create(tenantId, uid, "Proc List", "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, beneficiario.Id, procedimento.Id);
    }

    private static CriarItemGuiaCommand ItemPadrao(Guid procedimentoId) =>
        new(procedimentoId, PosicaoExecutor.Cirurgiao, OrdemProcedimento.Unico,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);

    private static async Task<Guid> CriarGuiaAsync(
        GuiaService service,
        Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId,
        string senha, DateOnly data)
    {
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            senha, data, false, string.Empty,
            [ItemPadrao(procedimentoId)]);
        var result = await service.CriarAsync(cmd);
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Listar_FiltraPorPrestadorIdAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorA, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var prestadorB = Prestador.Create(tenantId, "Dr. B List", null);
        ctx.Add(prestadorB);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user);
        await CriarGuiaAsync(service, prestadorA, operadoraId, beneficiarioId, procedimentoId, "SFA001", new DateOnly(2025, 1, 10));
        await CriarGuiaAsync(service, prestadorA, operadoraId, beneficiarioId, procedimentoId, "SFA002", new DateOnly(2025, 1, 11));
        await CriarGuiaAsync(service, prestadorB.Id, operadoraId, beneficiarioId, procedimentoId, "SFB001", new DateOnly(2025, 1, 12));

        var result = await service.ListarAsync(new ListarGuiasQuery(prestadorA, null, null, null, 1, 20));

        Assert.Equal(2, result.Total);
        Assert.All(result.Itens, g => Assert.Equal(prestadorA, g.PrestadorId));
    }

    [Fact]
    public async Task Listar_FiltraPorPeriodoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFP001", new DateOnly(2025, 1, 15));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFP002", new DateOnly(2025, 3, 15));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, new DateOnly(2025, 2, 1), new DateOnly(2025, 4, 1), null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal(new DateOnly(2025, 3, 15), result.Itens[0].DataAtendimento);
    }

    [Fact]
    public async Task Listar_FiltraPorSituacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFS001", new DateOnly(2025, 6, 1));

        var comApresentada = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, SituacaoGuia.Apresentada, 1, 20));
        var comLiquidada = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, SituacaoGuia.Liquidada, 1, 20));

        Assert.Equal(1, comApresentada.Total);
        Assert.Equal(0, comLiquidada.Total);
    }

    [Fact]
    public async Task Listar_PaginacaoCorretaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user);

        for (var i = 0; i < 5; i++)
        {
            await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId,
                $"SFPAG{i:D2}", new DateOnly(2025, 1, i + 1));
        }

        var pagina1 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, 1, 2));
        var pagina2 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, 2, 2));
        var pagina3 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, 3, 2));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.Single(pagina3.Itens);
    }

    [Fact]
    public async Task Listar_SoRetornaGuiasDoPropriTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var (prestadorA, operadoraA, beneficiarioA, procedimentoA) = await SeedCatalogAsync(ctxA, tenantA);
        var serviceA = new GuiaService(ctxA, userA);
        await CriarGuiaAsync(serviceA, prestadorA, operadoraA, beneficiarioA, procedimentoA, "SFISO-A", new DateOnly(2025, 6, 1));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var (prestadorB, operadoraB, beneficiarioB, procedimentoB) = await SeedCatalogAsync(ctxB, tenantB);
        var serviceB = new GuiaService(ctxB, userB);
        await CriarGuiaAsync(serviceB, prestadorB, operadoraB, beneficiarioB, procedimentoB, "SFISO-B", new DateOnly(2025, 6, 2));

        var resultA = await serviceA.ListarAsync(new ListarGuiasQuery(null, null, null, null, 1, 100));

        Assert.Contains(resultA.Itens, g => g.Senha == "SFISO-A");
        Assert.DoesNotContain(resultA.Itens, g => g.Senha == "SFISO-B");
    }
}

file sealed class FakeListTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
