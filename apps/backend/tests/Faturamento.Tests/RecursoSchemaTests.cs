using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.RecursoSchema;

[Collection(nameof(PostgresCollection))]
public sealed class RecursoSchemaTests(PostgresContainerFixture db)
{
    private static async Task<(Guid operadoraId, Guid prestadorId)> SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. RE01 " + tenantId.ToString("N")[..4], null);
        var operadora = Operadora.Create(tenantId, "UNIMED RE01 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        ctx.Add(prestador);
        ctx.Add(operadora);
        await ctx.SaveChangesAsync();
        return (operadora.Id, prestador.Id);
    }

    [Fact]
    public async Task Recurso_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid recursoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);
            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 2, 15), "Obs recurso");
            ctx.Add(recurso);
            await ctx.SaveChangesAsync();
            recursoId = recurso.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.Recursos.Where(r => r.Id == recursoId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal("202602", salvo.Numero);
        Assert.Equal(new DateOnly(2026, 2, 15), salvo.DataEmissao);
        Assert.Equal("Obs recurso", salvo.Observacao);
    }

    [Fact]
    public async Task Recurso_Numero_GeradoDaDataEmissaoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid recursoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);
            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 2, 15), null);
            ctx.Add(recurso);
            await ctx.SaveChangesAsync();
            recursoId = recurso.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.Recursos.Where(r => r.Id == recursoId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal("202602", salvo.Numero);
    }

    [Fact]
    public async Task Guia_MarcarEmRecurso_MudaSituacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid guiaId;
        Guid recursoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);

            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 1, 10), null);
            ctx.Add(recurso);

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
                null, "RE01" + tenantId.ToString("N")[..6], new DateOnly(2026, 1, 5), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();

            guia.MarcarEmRecurso(recurso.Id);
            await ctx.SaveChangesAsync();

            guiaId = guia.Id;
            recursoId = recurso.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salva = await adminCtx.Guias.Where(g => g.Id == guiaId).SingleOrDefaultAsync();

        Assert.NotNull(salva);
        Assert.Equal(SituacaoGuia.EmRecurso, salva.Situacao);
        Assert.Equal(recursoId, salva.RecursoId);
    }

    [Fact]
    public async Task Guia_RemoverDoRecurso_SemLiquidacao_VoltaApresentadaAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid guiaId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);

            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 1, 10), null);
            ctx.Add(recurso);

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
                null, "RE02" + tenantId.ToString("N")[..6], new DateOnly(2026, 1, 5), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();

            guia.MarcarEmRecurso(recurso.Id);
            await ctx.SaveChangesAsync();

            guia.RemoverDoRecurso(todosItensLiquidados: false);
            await ctx.SaveChangesAsync();

            guiaId = guia.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salva = await adminCtx.Guias.Where(g => g.Id == guiaId).SingleOrDefaultAsync();

        Assert.NotNull(salva);
        Assert.Equal(SituacaoGuia.Apresentada, salva.Situacao);
        Assert.Null(salva.RecursoId);
    }

    [Fact]
    public async Task Guia_RemoverDoRecurso_Liquidada_VoltaLiquidadaAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid guiaId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);

            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 1, 10), null);
            ctx.Add(recurso);

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
                null, "RE03" + tenantId.ToString("N")[..6], new DateOnly(2026, 1, 5), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();

            guia.MarcarEmRecurso(recurso.Id);
            await ctx.SaveChangesAsync();

            guia.RemoverDoRecurso(todosItensLiquidados: true);
            await ctx.SaveChangesAsync();

            guiaId = guia.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salva = await adminCtx.Guias.Where(g => g.Id == guiaId).SingleOrDefaultAsync();

        Assert.NotNull(salva);
        Assert.Equal(SituacaoGuia.Liquidada, salva.Situacao);
        Assert.Null(salva.RecursoId);
    }

    [Fact]
    public async Task Recurso_Delete_Restrict_ComGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid recursoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (operadoraId, prestadorId) = await SeedBaseAsync(ctx, tenantId);

            var recurso = Recurso.Create(tenantId, operadoraId, prestadorId,
                new DateOnly(2026, 3, 1), null);
            ctx.Add(recurso);

            var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
                null, "RE04" + tenantId.ToString("N")[..6], new DateOnly(2026, 3, 1), false, string.Empty);
            ctx.Add(guia);
            await ctx.SaveChangesAsync();

            guia.MarcarEmRecurso(recurso.Id);
            await ctx.SaveChangesAsync();

            recursoId = recurso.Id;
        }

        await using var ctx2 = db.CreateContext();
        var recursoParaExcluir = await ctx2.Recursos.FindAsync(recursoId);
        ctx2.Remove(recursoParaExcluir!);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());
    }
}
