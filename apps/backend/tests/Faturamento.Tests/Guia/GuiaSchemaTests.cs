using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Schema;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaSchemaTests(PostgresContainerFixture db)
{
    private static async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)> SeedCatalogAsync(
        AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Schema Teste", null);
        var operadora = Operadora.Create(tenantId, "UNIMED Schema", null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, "SCHEMA001", "Paciente Schema");
        var procedimento = Procedimento.Create(tenantId, "10101012", "Consulta Médica", "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, beneficiario.Id, procedimento.Id);
    }

    [Fact]
    public async Task Schema_CriaTabela_GuiasComColunasCorretasAsync()
    {
        var tenantId = Guid.NewGuid();

        Guid guiaId;
        var numeroGuia = "GUIA-" + tenantId.ToString("N")[..6].ToUpperInvariant();
        var data = new DateOnly(2025, 1, 15);

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (prestadorId, operadoraId, beneficiarioId, _) = await SeedCatalogAsync(ctx, tenantId);
            var guia = Guia.Create(tenantId, prestadorId, operadoraId, beneficiarioId, numeroGuia, data, false, "Observação de teste");
            ctx.Add(guia);
            await ctx.SaveChangesAsync();
            guiaId = guia.Id;
        }

        await using var ctx2 = db.CreateTenantContext(tenantId);
        var salva = await ctx2.Guias.Where(g => g.Id == guiaId).SingleOrDefaultAsync();

        Assert.NotNull(salva);
        Assert.Equal(guiaId, salva.Id);
        Assert.Equal(tenantId, salva.TenantId);
        Assert.Equal(numeroGuia, salva.NumeroGuia);
        Assert.Equal(data, salva.DataAtendimento);
        Assert.Equal(SituacaoGuia.Apresentada, salva.Situacao);
        Assert.False(salva.EhPacote);
        Assert.Equal("Observação de teste", salva.Observacao);
    }

    [Fact]
    public async Task Schema_CriaTabela_ItensGuiaComFkParaGuiasAsync()
    {
        var tenantId = Guid.NewGuid();

        Guid itemId;
        Guid guiaId;
        Guid procedimentoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (prestadorId, operadoraId, beneficiarioId, procId) = await SeedCatalogAsync(ctx, tenantId);
            procedimentoId = procId;

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, beneficiarioId, "ITEM-01", new DateOnly(2025, 2, 10), true, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();
            guiaId = guia.Id;

            var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 150m);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensGuia.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(guiaId, salvo.GuiaId);
        Assert.Equal(procedimentoId, salvo.ProcedimentoId);
        Assert.Equal(PosicaoExecutor.Cirurgiao, salvo.PosicaoExecutor);
        Assert.Equal(1.0m, salvo.PercentualOrdem);
        Assert.Equal(ViaAcesso.Convencional, salvo.ViaAcesso);
        Assert.Equal(Acomodacao.Enfermaria, salvo.Acomodacao);
        Assert.False(salvo.EhUrgencia);
        Assert.Equal(150m, salvo.ValorApurado);
        Assert.Null(salvo.ValorLiquidado);
    }

    [Fact]
    public async Task ItemGuia_DeletadoEmCascadeQuandoGuiaDeletadaAsync()
    {
        var tenantId = Guid.NewGuid();

        Guid guiaId;
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, beneficiarioId, "CASCADE-01", new DateOnly(2025, 3, 5), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();
            guiaId = guia.Id;

            var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Anestesista, 1.0m, ViaAcesso.Videolaparoscopia, Acomodacao.Apartamento, true, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;

            ctx.Remove(guia);
            await ctx.SaveChangesAsync();
        }

        await using var adminCtx = db.CreateContext();
        Assert.Null(await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId));
        Assert.Null(await adminCtx.ItensGuia.FirstOrDefaultAsync(i => i.Id == itemId));
    }

    [Fact]
    public async Task QueryFilter_TenantANaoVeGuiaDoTenantBAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid guiaId;

        await using (var ctxA = db.CreateTenantContext(tenantA))
        {
            var (prestadorId, operadoraId, beneficiarioId, _) = await SeedCatalogAsync(ctxA, tenantA);
            var guia = Guia.Create(tenantA, prestadorId, operadoraId, beneficiarioId, "ISOLADO-A", new DateOnly(2025, 4, 1), false, string.Empty);
            ctxA.Add(guia);
            await ctxA.SaveChangesAsync();
            guiaId = guia.Id;
        }

        await using var ctxB = db.CreateTenantContext(tenantB);
        var count = await ctxB.Guias.CountAsync(g => g.Id == guiaId);

        Assert.Equal(0, count);
    }
}
