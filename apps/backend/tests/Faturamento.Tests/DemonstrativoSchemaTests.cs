using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.DemonstrativoSchema;

[Collection(nameof(PostgresCollection))]
public sealed class DemonstrativoSchemaTests(PostgresContainerFixture db)
{
    private static async Task<(Guid operadoraId, Guid prestadorId)> SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. DM-01 " + tenantId.ToString("N")[..4], null);
        var operadora = Operadora.Create(tenantId, "UNIMED DM-01 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        ctx.Add(prestador);
        ctx.Add(operadora);
        await ctx.SaveChangesAsync();
        return (operadora.Id, prestador.Id);
    }

    [Fact]
    public async Task Demonstrativo_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid demId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, _) = await SeedBaseAsync(ctx, tenantId);
            var dem = Demonstrativo.Create(tenantId, operadoraId, "2025-12",
                new DateOnly(2025, 12, 10), "Obs teste");
            ctx.Add(dem);
            await ctx.SaveChangesAsync();
            demId = dem.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.Demonstrativos.Where(d => d.Id == demId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal("2025-12", salvo.Competencia);
        Assert.Equal(new DateOnly(2025, 12, 10), salvo.DataRecebimento);
        Assert.Equal("Obs teste", salvo.Observacao);
    }

    [Fact]
    public async Task ItemDemonstrativo_ValorGlosado_CalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, _) = await SeedBaseAsync(ctx, tenantId);
            var dem = Demonstrativo.Create(tenantId, operadoraId, "2025-12",
                new DateOnly(2025, 12, 10), null);
            ctx.Add(dem);
            await ctx.SaveChangesAsync();

            var item = ItemDemonstrativo.Create(dem.Id, "SENHA01", "40300390",
                null, 1000m, 700m, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensDemonstrativo.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(300m, salvo.ValorGlosado);
    }

    [Fact]
    public async Task ItemDemonstrativo_SemConciliacao_ItemGuiaIdNuloAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, _) = await SeedBaseAsync(ctx, tenantId);
            var dem = Demonstrativo.Create(tenantId, operadoraId, "2025-11",
                new DateOnly(2025, 11, 5), null);
            ctx.Add(dem);
            await ctx.SaveChangesAsync();

            var item = ItemDemonstrativo.Create(dem.Id, "SENHA02", "40300390",
                null, 500m, 500m, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.ItensDemonstrativo.Where(i => i.Id == itemId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Null(salvo.ItemGuiaId);
    }

    [Fact]
    public async Task ItemDemonstrativo_CascadeDeleteAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid demId;
        Guid itemId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, _) = await SeedBaseAsync(ctx, tenantId);
            var dem = Demonstrativo.Create(tenantId, operadoraId, "2025-10",
                new DateOnly(2025, 10, 1), null);
            ctx.Add(dem);
            await ctx.SaveChangesAsync();

            var item = ItemDemonstrativo.Create(dem.Id, "SENHA03", "40300390",
                null, 800m, 600m, null);
            ctx.Add(item);
            await ctx.SaveChangesAsync();
            demId = dem.Id;
            itemId = item.Id;
        }

        await using (var ctx = db.CreateContext())
        {
            var dem = await ctx.Demonstrativos.FindAsync(demId);
            ctx.Remove(dem!);
            await ctx.SaveChangesAsync();
        }

        await using var adminCtx = db.CreateContext();
        var itemSalvo = await adminCtx.ItensDemonstrativo.Where(i => i.Id == itemId).SingleOrDefaultAsync();
        Assert.Null(itemSalvo);
    }

    [Fact]
    public async Task ItemDemonstrativo_Restrict_NaoExcluiItemGuiaComItemAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid itemGuiaId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);
            var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc DM-01", "1", null, false, false);
            ctx.Add(procedimento);
            await ctx.SaveChangesAsync();

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
                "DM01" + tenantId.ToString("N")[..6], new DateOnly(2025, 6, 1), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();

            var itemGuia = ItemGuia.Create(guia.Id, procedimento.Id, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
            ctx.Add(itemGuia);
            await ctx.SaveChangesAsync();
            itemGuiaId = itemGuia.Id;

            var dem = Demonstrativo.Create(tenantId, operadoraId, "2025-09",
                new DateOnly(2025, 9, 1), null);
            ctx.Add(dem);
            await ctx.SaveChangesAsync();

            var itemDem = ItemDemonstrativo.Create(dem.Id, "SENHA04", "40300390",
                null, 1000m, 1000m, null);
            itemDem.Conciliar(itemGuiaId);
            ctx.Add(itemDem);
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = db.CreateContext();
        var itemGuiaParaExcluir = await ctx2.ItensGuia.FindAsync(itemGuiaId);
        ctx2.Remove(itemGuiaParaExcluir!);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());
    }
}
