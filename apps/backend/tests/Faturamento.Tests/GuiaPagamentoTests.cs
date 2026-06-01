using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Pagamento;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaPagamentoTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user, PricingRuleSetFactory factory) BuildTenant(Guid tenantId)
    {
        var user = new FakeTenantUserPgt(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        return (ctx, user, new PricingRuleSetFactory(ctx));
    }

    private static async Task<(Guid guiaId, Guid itemId)> SeedGuiaAsync(
        AppDbContext ctx, GuiaService service, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Pgt", null);
        var operadora = Operadora.Create(tenantId, "Op. Pgt", null, null, TipoRuleSet.Nulo);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..10], "PROC PGT", "1", null, false, false);
        ctx.AddRange(prestador, operadora, proc);
        await ctx.SaveChangesAsync();

        var cmd = new CriarGuiaCommand(
            prestador.Id, operadora.Id, null,
            null, "PGT" + tenantId.ToString("N")[..5], new DateOnly(2025, 1, 1),
            false, string.Empty,
            [new CriarItemGuiaCommand(
                proc.Id, PosicaoExecutor.Cirurgiao, 1.0m,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var result = await service.CriarAsync(cmd);
        var guia = result.Value!;
        return (guia.Id, guia.Itens[0].Id);
    }

    [Fact]
    public async Task AtualizarPagamentoItem_SetsValorLiquidadoAndMotivoGlosaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new GuiaService(ctx, user, factory);
        var (guiaId, itemId) = await SeedGuiaAsync(ctx, service, tenantId);

        var result = await service.AtualizarPagamentoItemAsync(guiaId, itemId, 100m, "CB");

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value!.ValorLiquidado);
        Assert.Equal("CB", result.Value.MotivoGlosa);

        await using var check = db.CreateTenantContext(tenantId);
        var item = await check.ItensGuia.FirstOrDefaultAsync(i => i.Id == itemId);
        Assert.NotNull(item);
        Assert.Equal(100m, item.ValorLiquidado);
        Assert.Equal("CB", item.MotivoGlosa);
    }

    [Fact]
    public async Task AtualizarPagamentoItem_TodosItensComValor_GuiaFicaLiquidadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new GuiaService(ctx, user, factory);
        var (guiaId, itemId) = await SeedGuiaAsync(ctx, service, tenantId);

        var result = await service.AtualizarPagamentoItemAsync(guiaId, itemId, 200m, null);

        Assert.True(result.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(SituacaoGuia.Liquidada, guia.Situacao);
    }

    [Fact]
    public async Task AtualizarPagamentoItem_ValorLiquidadoNull_GuiaFicaApresentadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new GuiaService(ctx, user, factory);
        var (guiaId, itemId) = await SeedGuiaAsync(ctx, service, tenantId);

        await service.AtualizarPagamentoItemAsync(guiaId, itemId, 300m, null);

        var result = await service.AtualizarPagamentoItemAsync(guiaId, itemId, null, null);

        Assert.True(result.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(SituacaoGuia.Apresentada, guia.Situacao);
    }
}

file sealed class FakeTenantUserPgt(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
