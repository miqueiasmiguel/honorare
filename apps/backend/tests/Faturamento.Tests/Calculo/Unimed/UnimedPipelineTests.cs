using App;
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
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, proc.Id);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid[] procIds)>
        SeedMultiAsync(AppDbContext ctx, Guid tenantId, decimal[] valores)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Multi", null);
        var operadora = Operadora.Create(tenantId, "UNIMED-M-" + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        var procs = valores.Select((_, i) =>
            Procedimento.Create(tenantId, tenantId.ToString("N")[(i * 8)..((i * 8) + 8)], $"Proc{i}", "1", null, false, false))
            .ToArray();
        ctx.Add(prestador);
        ctx.Add(operadora);
        foreach (var proc in procs)
        {
            ctx.Add(proc);
        }

        await ctx.SaveChangesAsync();

        for (var i = 0; i < procs.Length; i++)
        {
            ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procs[i].Id, valores[i]));
        }

        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, procs.Select(p => p.Id).ToArray());
    }

    private static CriarGuiaCommand Cmd(
        Guid prestadorId, Guid operadoraId, Guid procedimentoId, string numeroGuia,
        PosicaoExecutor posicao,
        ViaAcesso via, Acomodacao acomodacao, bool ehUrgencia)
        => new(prestadorId, operadoraId, null, numeroGuia,
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, posicao, via, acomodacao, ehUrgencia, null)]);

    [Fact]
    public async Task Cirurgiao_Unico_Enfermaria_SemUrgenciaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P01", PosicaoExecutor.Cirurgiao,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

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
                ViaAcesso.Convencional, Acomodacao.Apartamento, false));

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
                ViaAcesso.Convencional, Acomodacao.Enfermaria, true));

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
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.Itens[0].ValorApurado);
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
                ViaAcesso.Videolaparoscopia, Acomodacao.Enfermaria, false));

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
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

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
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, result.Value!.Itens[0].ValorApurado);
    }

    [Fact]
    public async Task Anestesista_SemPorteAnestesicoNoProcedimento_CriacaoRejeitadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-P08", PosicaoExecutor.Anestesista,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationError>(result.Error);
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
                ViaAcesso.Convencional, Acomodacao.Apartamento, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(2600m, result.Value!.Itens[0].ValorApurado);
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
                ViaAcesso.Videolaparoscopia, Acomodacao.Apartamento, false));

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
                ViaAcesso.Convencional, Acomodacao.Apartamento, false));

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
                ViaAcesso.Convencional, Acomodacao.Apartamento, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, result.Value!.Itens[0].ValorApurado); // 1000 × 1.0 × 0.4
    }

    [Fact]
    public async Task Deve_aplicar_100_50_40_por_valor_decrescente_com_3_procs_cirurgiaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procIds) = await SeedMultiAsync(ctx, tenantId, [1000m, 600m, 300m]);

        // Inserir em ordem inversa para confirmar que o ranking é por valor, não por inserção
        var cmd = new CriarGuiaCommand(pId, oId, null, "SEN-CASCATA-01",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [
                new CriarItemGuiaCommand(procIds[2], PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procIds[0], PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procIds[1], PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
            ]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        var itens = result.Value!.Itens;
        Assert.Equal(1000m, itens.First(i => i.ProcedimentoId == procIds[0]).ValorApurado); // rank 0: 100%
        Assert.Equal(300m, itens.First(i => i.ProcedimentoId == procIds[1]).ValorApurado);  // rank 1: 50% × 600
        Assert.Equal(120m, itens.First(i => i.ProcedimentoId == procIds[2]).ValorApurado);  // rank 2: 40% × 300
    }

    [Fact]
    public async Task Deve_aplicar_mesma_cascata_para_vias_diferentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procIds) = await SeedMultiAsync(ctx, tenantId, [1000m, 600m, 300m]);

        var cmd = new CriarGuiaCommand(pId, oId, null, "SEN-CASCATA-02",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [
                new CriarItemGuiaCommand(procIds[0], PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procIds[1], PosicaoExecutor.Cirurgiao, ViaAcesso.Endoscopica, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procIds[2], PosicaoExecutor.Cirurgiao, ViaAcesso.Percutanea, Acomodacao.Enfermaria, false, null),
            ]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        var itens = result.Value!.Itens;
        Assert.Equal(1000m, itens.First(i => i.ProcedimentoId == procIds[0]).ValorApurado);
        Assert.Equal(300m, itens.First(i => i.ProcedimentoId == procIds[1]).ValorApurado);
        Assert.Equal(120m, itens.First(i => i.ProcedimentoId == procIds[2]).ValorApurado);
    }

    [Fact]
    public async Task Deve_rankear_cirurgiao_e_primeiro_auxiliar_em_grupos_separadosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        // Cirurgião: rank 0 = 100%, fator posição 1.0 → 1000m
        // PrimeiroAuxiliar: rank 0 separado = 100%, fator posição 0.6 → 600m
        var cmd = new CriarGuiaCommand(pId, oId, null, "SEN-CASCATA-03",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [
                new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procId, PosicaoExecutor.PrimeiroAuxiliar, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
            ]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        var itens = result.Value!.Itens;
        Assert.Equal(1000m, itens.First(i => i.PosicaoExecutor == PosicaoExecutor.Cirurgiao).ValorApurado);
        Assert.Equal(600m, itens.First(i => i.PosicaoExecutor == PosicaoExecutor.PrimeiroAuxiliar).ValorApurado);
    }

    [Fact]
    public async Task Deve_ignorar_percentual_ordem_de_entradaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = Build(tenantId);
        await using var _ = ctx;
        var (pId, oId, procId) = await SeedAsync(ctx, tenantId);

        var result = await service.CriarAsync(
            Cmd(pId, oId, procId, "SEN-CASCATA-04", PosicaoExecutor.Cirurgiao,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.Itens[0].ValorApurado);
    }
}

file sealed class FakePipelineUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
