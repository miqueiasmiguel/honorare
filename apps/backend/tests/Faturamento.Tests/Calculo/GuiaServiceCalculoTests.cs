using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.GuiaCalculo;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaServiceCalculoTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeCalcUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedBaseAsync(AppDbContext ctx, Guid tenantId, TipoRuleSet tipoRuleSet = TipoRuleSet.Unimed)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Calculo", null);
        var operadora = Operadora.Create(tenantId, "Op " + tenantId.ToString("N")[..6], null, null, tipoRuleSet);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Calculo", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();
        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    [Fact]
    public async Task CriarGuia_Unimed_ItemCalculado_ValorApuradoPreenchidoAsync()
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

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-CAL01",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(200m, result.Value!.Itens[0].ValorApurado);

        await using var adminCtx = db.CreateContext();
        var calculo = await adminCtx.Calculos.FirstOrDefaultAsync(c => c.GuiaId == result.Value.Id);
        Assert.NotNull(calculo);
        Assert.True(await adminCtx.PassosCalculo.AnyAsync(p => p.CalculoId == calculo.Id && p.Regra == "ValorBase"));
    }

    [Fact]
    public async Task CriarGuia_Unimed_SemTabela_ValorApuradoNuloAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-CAL02",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Itens[0].ValorApurado);

        await using var adminCtx = db.CreateContext();
        Assert.True(await adminCtx.Calculos.AnyAsync(c => c.GuiaId == result.Value.Id));
    }

    [Fact]
    public async Task CriarGuia_Pacote_NaoInvocaMotor_ValorApuradoManualPreservadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-CAL03",
            new DateOnly(2025, 1, 1), true, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 500m)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value!.Itens[0].ValorApurado);

        await using var adminCtx = db.CreateContext();
        Assert.False(await adminCtx.Calculos.AnyAsync(c => c.GuiaId == result.Value.Id));
    }

    [Fact]
    public async Task AtualizarGuia_RecalculaESubstituiCalculoAsync()
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

        var criar = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-CAL04",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);
        var criado = await service.CriarAsync(criar);
        Assert.True(criado.IsSuccess);

        var atualizar = new AtualizarGuiaCommand(operadoraId, null, "SEN-CAL04-UPD",
            new DateOnly(2025, 2, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var atualizado = await service.AtualizarAsync(criado.Value!.Id, atualizar);

        Assert.True(atualizado.IsSuccess);

        await using var adminCtx = db.CreateContext();
        var count = await adminCtx.Calculos.CountAsync(c => c.GuiaId == criado.Value.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CriarGuia_OperadoraSemUnimed_NullRuleSetExecuta_SemValorApuradoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId, TipoRuleSet.Nulo);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-CAL05",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Itens[0].ValorApurado);

        await using var adminCtx = db.CreateContext();
        Assert.True(await adminCtx.Calculos.AnyAsync(c => c.GuiaId == result.Value.Id));
        var calculo = await adminCtx.Calculos.FirstAsync(c => c.GuiaId == result.Value.Id);
        Assert.False(await adminCtx.PassosCalculo.AnyAsync(p => p.CalculoId == calculo.Id));
    }
}

file sealed class FakeCalcUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
