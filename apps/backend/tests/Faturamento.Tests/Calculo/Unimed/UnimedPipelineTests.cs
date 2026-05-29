using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.UnimedPipeline;

[Collection(nameof(PostgresCollection))]
public sealed class UnimedPipelineTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, GuiaService service) Build(Guid tenantId)
    {
        var user = new FakePipelineUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        var factory = new PricingRuleSetFactory(ctx);
        return (ctx, new GuiaService(ctx, user, factory));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Pipeline", null);
        var operadora = Operadora.Create(tenantId, "UNIMED-" + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Pipeline", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, proc.Id, 1000m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.PrimeiroAuxiliar, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.SegundoAuxiliar, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.TerceiroAuxiliar, 100m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, proc.Id);
    }

    private static CriarGuiaCommand Cmd(
        Guid prestadorId, Guid operadoraId, Guid procedimentoId, string senha,
        PosicaoExecutor posicao, decimal percentualOrdem,
        ViaAcesso via, Acomodacao acomodacao, bool ehUrgencia)
        => new(prestadorId, operadoraId, null, null, senha,
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, posicao, percentualOrdem, via, acomodacao, ehUrgencia, null)]);

    [Fact]
    public async Task Cirurgiao_Unico_Enfermaria_SemUrgenciaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P01", PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_Unico_ApartamentoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P02", PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(2000m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_Unico_Urgencia_NaoSadtAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P03", PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(1300m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_SecundarioMesmaViaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P04", PosicaoExecutor.Cirurgiao,
                0.5m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_Videolaparoscopia_SemPorteProprioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P05", PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Videolaparoscopia, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1500m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task PrimeiroAuxiliar_Unico_EnfermariaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P06", PosicaoExecutor.PrimeiroAuxiliar,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task SegundoAuxiliar_Unico_EnfermariaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P07", PosicaoExecutor.SegundoAuxiliar,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Anestesista_RetornaIndeterminadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P08", PosicaoExecutor.Anestesista,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_SecundarioMesmaVia_Apartamento_UrgenciaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P09", PosicaoExecutor.Cirurgiao,
                0.5m, ViaAcesso.Convencional, Acomodacao.Apartamento, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(1300m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Cirurgiao_Videolaparoscopia_ApartamentoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P10", PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Videolaparoscopia, Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(3000m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task PrimeiroAuxiliar_Apartamento_NaoDobra_PipelineAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P11", PosicaoExecutor.PrimeiroAuxiliar,
                1.0m, ViaAcesso.Convencional, Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, result.Value!.Itens[0].ValorApurado); // 1000 × 1.0 × 0.6
    }

    [Fact]
    public async Task SegundoAuxiliar_Apartamento_NaoDobra_PipelineAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P12", PosicaoExecutor.SegundoAuxiliar,
                1.0m, ViaAcesso.Convencional, Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, result.Value!.Itens[0].ValorApurado); // 1000 × 1.0 × 0.4
    }
}

file sealed class FakePipelineUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
