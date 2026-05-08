using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.CalculoEndpoints;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaCalculoEndpointTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeVisUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var suffix = tenantId.ToString("N")[..8];
        var prestador = Prestador.Create(tenantId, "Dr. Vis " + suffix, null);
        var operadora = Operadora.Create(tenantId, "Op Vis " + suffix, null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, suffix, "Proc Vis " + suffix, "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();
        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    [Fact]
    public async Task ObterCalculo_GuiaComCalculoCompleto_RetornaItemCalculadoComPassosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 200m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestadorId, operadoraId, PosicaoExecutor.Cirurgiao, 100m));
        await ctx.SaveChangesAsync();

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-VIS01",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var result = await service.ObterCalculoAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.EhPacote);
        Assert.NotNull(result.Value.RealizadoEm);
        Assert.Single(result.Value.Itens);
        var item = result.Value.Itens[0];
        Assert.Equal("Calculado", item.Situacao);
        Assert.NotNull(item.ValorApurado);
        Assert.Contains(item.Passos, p => p.Regra == "ValorBase");
    }

    [Fact]
    public async Task ObterCalculo_GuiaSemTabela_RetornaItemSemTabelaSemPassosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-VIS02",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var result = await service.ObterCalculoAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var item = result.Value!.Itens[0];
        Assert.Null(item.ValorApurado);
        Assert.Empty(item.Passos);
        Assert.Equal("SemTabela", item.Situacao);
    }

    [Fact]
    public async Task ObterCalculo_GuiaPacote_RetornaEhPacoteTrueComSituacaoPacoteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-VIS03",
            new DateOnly(2025, 1, 1), true, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 350m)]);

        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var result = await service.ObterCalculoAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.EhPacote);
        Assert.Null(result.Value.RealizadoEm);
        var item = result.Value.Itens[0];
        Assert.Equal("Pacote", item.Situacao);
        Assert.Equal(350m, item.ValorApurado);
    }
}

file sealed class FakeVisUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
