using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.AnestesiaSchema;

[Collection(nameof(PostgresCollection))]
public sealed class AnestesiaSchemaTests(PostgresContainerFixture db)
{
    private static async Task<(Guid guiaId, Guid procedimentoId)> SeedAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. AN-01", null);
        var operadora = Operadora.Create(tenantId, "UNIMED AN-01", null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc AN-01", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        var guia = Guia.Create(tenantId, prestador.Id, operadora.Id, null, "AN01-" + tenantId.ToString("N")[..4], new DateOnly(2025, 6, 1), false, string.Empty);
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        return (guia.Id, procedimento.Id);
    }

    [Fact]
    public async Task TempoAnestesicoMin_NulloPorPadraoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (guiaId, procedimentoId) = await SeedAsync(ctx, tenantId);
            var item = ItemGuia.Create(guiaId, procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensGuia.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Null(salvo.TempoAnestesicoMin);
    }

    [Fact]
    public async Task TempoAnestesicoMin_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (guiaId, procedimentoId) = await SeedAsync(ctx, tenantId);
            var item = ItemGuia.Create(guiaId, procedimentoId, PosicaoExecutor.Anestesista,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null,
                tempoAnestesicoMin: 180);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensGuia.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(180, salvo.TempoAnestesicoMin);
    }

    [Fact]
    public async Task TempoAnestesicoMin_NaoObrigatorioParaNaoAnestesistaAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (guiaId, procedimentoId) = await SeedAsync(ctx, tenantId);
            var item = ItemGuia.Create(guiaId, procedimentoId, PosicaoExecutor.Cirurgiao,
                OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensGuia.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(PosicaoExecutor.Cirurgiao, salvo.PosicaoExecutor);
        Assert.Null(salvo.TempoAnestesicoMin);
    }
}
