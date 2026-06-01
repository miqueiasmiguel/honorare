using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.AnestesiaPipeline;

[Collection(nameof(PostgresCollection))]
public sealed class UnimedAnestesiaPipelineTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, GuiaService service) Build(Guid tenantId)
    {
        var user = new FakeAnestesiaUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        var factory = new PricingRuleSetFactory(ctx);
        return (ctx, new GuiaService(ctx, user, factory));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedCompletoAsync(AppDbContext ctx, Guid tenantId, decimal deflatorPercentual = 100m,
            string? porteAnestesico = "J")
    {
        var prestador = Prestador.Create(tenantId, "Dr. Anestesia", null);
        var operadora = Operadora.Create(tenantId, "UNIMED-AN" + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Anestesia", "1", porteAnestesico, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaPorteAnestesico.Create(tenantId, operadora.Id, "J", 526.50m, 842.40m, null));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Anestesista, deflatorPercentual));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, proc.Id);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedSemTabelaPorteAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. SemTabela", null);
        var operadora = Operadora.Create(tenantId, "UNIMED-ST" + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc SemTabela", "1", "J", false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Anestesista, 100m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, proc.Id);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedSemDeflatorAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. SemDeflator", null);
        var operadora = Operadora.Create(tenantId, "UNIMED-SD" + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc SemDeflator", "1", "J", false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaPorteAnestesico.Create(tenantId, operadora.Id, "J", 526.50m, 842.40m, null));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, proc.Id);
    }

    private static CriarGuiaCommand Cmd(
        Guid prestadorId, Guid operadoraId, Guid procedimentoId, string senha,
        Acomodacao acomodacao, bool ehUrgencia, int? tempoAnestesicoMin = null)
        => new(prestadorId, operadoraId, null, null, senha,
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(
                procedimentoId, PosicaoExecutor.Anestesista,
                1.0m, ViaAcesso.Convencional,
                acomodacao, ehUrgencia, null, tempoAnestesicoMin)]);

    [Fact]
    public async Task Anestesista_PorteJ_Enfermaria_CalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN01", Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(526.50m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Anestesista_PorteJ_Apartamento_CalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN02", Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(842.40m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Anestesista_PorteJ_Urgencia_Enfermaria_CalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN03", Acomodacao.Enfermaria, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(684.45m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Anestesista_SemTabelaPorte_CriacaoRejeitadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedSemTabelaPorteAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN04", Acomodacao.Enfermaria, false));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Anestesista_PorteAnestesicoNulo_CriacaoRejeitadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId, porteAnestesico: null);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN05", Acomodacao.Enfermaria, false));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Anestesista_SemDeflator_CriacaoRejeitadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedSemDeflatorAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN06", Acomodacao.Enfermaria, false));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Anestesista_Deflator80_Enfermaria_CalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId, deflatorPercentual: 80m);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN07", Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(421.20m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Guia_ComAnestesista_TracePersistePassosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedCompletoAsync(ctx, tenantId);

        var createResult = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-AN08", Acomodacao.Enfermaria, false));

        Assert.True(createResult.IsSuccess);

        var guiaId = createResult.Value!.Id;
        var calculoResult = await service.ObterCalculoAsync(guiaId);

        Assert.True(calculoResult.IsSuccess);
        var item = calculoResult.Value!.Itens[0];
        Assert.True(item.Passos.Count >= 1);
        Assert.Contains(item.Passos, p => p.Regra == "ValorBase");
    }
}

file sealed class FakeAnestesiaUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
