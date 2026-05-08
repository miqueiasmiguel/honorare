using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.CalculoSchema;

[Collection(nameof(PostgresCollection))]
public sealed class CalculoSchemaTests(PostgresContainerFixture db)
{
    private static async Task<(Guid guiaId, Guid itemGuiaId)> SeedGuiaAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Calculo", null);
        var operadora = Operadora.Create(tenantId, "UNIMED Calculo", null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Schema", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        var guia = Guia.Create(tenantId, prestador.Id, operadora.Id, null, "CALC-SCHEMA", new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        var item = ItemGuia.Create(guia.Id, procedimento.Id, PosicaoExecutor.Cirurgiao, OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item);
        await ctx.SaveChangesAsync();

        return (guia.Id, item.Id);
    }

    [Fact]
    public async Task Calculo_PersistidoERecuperadoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid calculoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (guiaId, _) = await SeedGuiaAsync(ctx, tenantId);
            var calculo = Calculo.Create(tenantId, guiaId);
            ctx.Add(calculo);
            await ctx.SaveChangesAsync();
            calculoId = calculo.Id;
        }

        await using var ctx2 = db.CreateTenantContext(tenantId);
        var salvo = await ctx2.Calculos.Where(c => c.Id == calculoId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(tenantId, salvo.TenantId);
        Assert.NotEqual(Guid.Empty, salvo.GuiaId);
        Assert.NotEqual(default, salvo.RealizadoEm);
    }

    [Fact]
    public async Task PassoCalculo_PersistidoERecuperadoAsync()
    {
        var tenantId = Guid.NewGuid();
        Guid passoId;

        await using (var ctx = db.CreateTenantContext(tenantId))
        {
            var (guiaId, itemGuiaId) = await SeedGuiaAsync(ctx, tenantId);
            var calculo = Calculo.Create(tenantId, guiaId);
            ctx.Add(calculo);
            await ctx.SaveChangesAsync();

            var passo = PassoCalculo.Create(calculo.Id, itemGuiaId, 1, "ValorBase", 0.8m, 80m);
            ctx.Add(passo);
            await ctx.SaveChangesAsync();
            passoId = passo.Id;
        }

        await using var adminCtx = db.CreateContext();
        var salvo = await adminCtx.PassosCalculo.Where(p => p.Id == passoId).SingleOrDefaultAsync();

        Assert.NotNull(salvo);
        Assert.Equal(1, salvo.Sequencia);
        Assert.Equal("ValorBase", salvo.Regra);
        Assert.Equal(0.8m, salvo.Fator);
        Assert.Equal(80m, salvo.ValorResultante);
    }

    [Fact]
    public async Task PassoCalculo_SequenciaUnicaPorCalculoAsync()
    {
        var tenantId = Guid.NewGuid();

        await using var ctx = db.CreateTenantContext(tenantId);
        var (guiaId, itemGuiaId) = await SeedGuiaAsync(ctx, tenantId);
        var calculo = Calculo.Create(tenantId, guiaId);
        ctx.Add(calculo);
        await ctx.SaveChangesAsync();

        var passo1 = PassoCalculo.Create(calculo.Id, itemGuiaId, 1, "ValorBase", 1m, 100m);
        var passo2 = PassoCalculo.Create(calculo.Id, itemGuiaId, 1, "Duplicado", 1m, 100m);
        ctx.Add(passo1);
        ctx.Add(passo2);

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Calculo_QueryFilterIsolaTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        Guid calculoId;

        await using (var ctxA = db.CreateTenantContext(tenantA))
        {
            var (guiaId, _) = await SeedGuiaAsync(ctxA, tenantA);
            var calculo = Calculo.Create(tenantA, guiaId);
            ctxA.Add(calculo);
            await ctxA.SaveChangesAsync();
            calculoId = calculo.Id;
        }

        await using var ctxB = db.CreateTenantContext(tenantB);
        var count = await ctxB.Calculos.CountAsync(c => c.Id == calculoId);

        Assert.Equal(0, count);
    }
}
