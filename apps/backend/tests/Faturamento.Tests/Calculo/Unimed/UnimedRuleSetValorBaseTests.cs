using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Faturamento.Motor.Unimed;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Motor.Unimed;

[Collection(nameof(PostgresCollection))]
public sealed class UnimedRuleSetValorBaseTests(PostgresContainerFixture db)
{
    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId, Guid guiaId)>
        SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Unimed", null);
        var operadora = Operadora.Create(tenantId, "UNIMED Teste " + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Unimed", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        var guia = Guia.Create(tenantId, prestador.Id, operadora.Id, null, "SEN-U", new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, procedimento.Id, guia.Id);
    }

    [Fact]
    public async Task ApurarAsync_TabelaEDeflatorPresentes_RetornaCalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId, procedimentoId, _) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestadorId, operadoraId, PosicaoExecutor.Cirurgiao, 80m));
        await ctx.SaveChangesAsync();

        var itemId = Guid.NewGuid();
        var sut = new UnimedRuleSet(ctx);
        var input = new ApurarGuiaContext(tenantId, prestadorId, operadoraId,
        [
            new ApurarItemInput(itemId, procedimentoId, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false)
        ]);

        var resultados = await sut.ApurarAsync(input);

        Assert.Single(resultados);
        var r = resultados[0];
        Assert.Equal(SituacaoApuracao.Calculado, r.Situacao);
        Assert.Equal(80m, r.ValorApurado);
        Assert.Single(r.Passos);
        Assert.Equal("ValorBase", r.Passos[0].Regra);
        Assert.Equal(0.8m, r.Passos[0].Fator);
        Assert.Equal(80m, r.Passos[0].ValorResultante);
    }

    [Fact]
    public async Task ApurarAsync_SemTabela_RetornaSemTabelaAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId, procedimentoId, _) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(DeflatorPrestador.Create(tenantId, prestadorId, operadoraId, PosicaoExecutor.Cirurgiao, 80m));
        await ctx.SaveChangesAsync();

        var itemId = Guid.NewGuid();
        var sut = new UnimedRuleSet(ctx);
        var input = new ApurarGuiaContext(tenantId, prestadorId, operadoraId,
        [
            new ApurarItemInput(itemId, procedimentoId, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false)
        ]);

        var resultados = await sut.ApurarAsync(input);

        Assert.Single(resultados);
        Assert.Equal(SituacaoApuracao.SemTabela, resultados[0].Situacao);
        Assert.Null(resultados[0].ValorApurado);
    }

    [Fact]
    public async Task ApurarAsync_SemDeflator_RetornaSemDeflatorAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId, procedimentoId, _) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 100m));
        await ctx.SaveChangesAsync();

        var itemId = Guid.NewGuid();
        var sut = new UnimedRuleSet(ctx);
        var input = new ApurarGuiaContext(tenantId, prestadorId, operadoraId,
        [
            new ApurarItemInput(itemId, procedimentoId, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false)
        ]);

        var resultados = await sut.ApurarAsync(input);

        Assert.Single(resultados);
        Assert.Equal(SituacaoApuracao.SemDeflator, resultados[0].Situacao);
        Assert.Null(resultados[0].ValorApurado);
    }

    [Fact]
    public async Task ApurarAsync_Anestesista_RetornaIndeterminadoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId, procedimentoId, _) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestadorId, operadoraId, PosicaoExecutor.Anestesista, 80m));
        await ctx.SaveChangesAsync();

        var itemId = Guid.NewGuid();
        var sut = new UnimedRuleSet(ctx);
        var input = new ApurarGuiaContext(tenantId, prestadorId, operadoraId,
        [
            new ApurarItemInput(itemId, procedimentoId, PosicaoExecutor.Anestesista,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false)
        ]);

        var resultados = await sut.ApurarAsync(input);

        Assert.Single(resultados);
        Assert.Equal(SituacaoApuracao.Indeterminado, resultados[0].Situacao);
        Assert.Null(resultados[0].ValorApurado);
    }

    [Fact]
    public async Task ApurarAsync_DoisItens_UmSemTabelaOutroCalculado_RetornaAmbosAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId, procedimentoId, _) = await SeedBaseAsync(ctx, tenantId);

        var procedimento2 = Procedimento.Create(tenantId, tenantId.ToString("N")[8..16], "Proc2", "2", null, false, false);
        ctx.Add(procedimento2);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 100m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestadorId, operadoraId, PosicaoExecutor.Cirurgiao, 80m));
        await ctx.SaveChangesAsync();

        var sut = new UnimedRuleSet(ctx);
        var input = new ApurarGuiaContext(tenantId, prestadorId, operadoraId,
        [
            new ApurarItemInput(Guid.NewGuid(), procedimentoId, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false),
            new ApurarItemInput(Guid.NewGuid(), procedimento2.Id, PosicaoExecutor.Cirurgiao,
                0.5m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false)
        ]);

        var resultados = await sut.ApurarAsync(input);

        Assert.Equal(2, resultados.Count);
        Assert.Contains(resultados, r => r.Situacao == SituacaoApuracao.Calculado);
        Assert.Contains(resultados, r => r.Situacao == SituacaoApuracao.SemTabela);
    }
}
