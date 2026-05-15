using App.Catalog;
using App.Data;
using App.Faturamento;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Conciliacao;

[Collection(nameof(PostgresCollection))]
public sealed class ConciliacaoTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, DemonstrativoService service) BuildService(Guid tenantId)
    {
        var user = new FakeTenantUserConc(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        return (ctx, new DemonstrativoService(ctx, user));
    }

    private static async Task<(Guid operadoraId, Guid prestadorId, Guid procedimentoId)> SeedCatalogAsync(
        AppDbContext ctx, Guid tenantId)
    {
        var op = Operadora.Create(tenantId, "OpConc" + tenantId.ToString("N")[..6], null, null, TipoRuleSet.Unimed);
        var pr = Prestador.Create(tenantId, "DrConc" + tenantId.ToString("N")[..6], null);
        var proc = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "ProcConc", "1", null, false, false);
        ctx.Add(op);
        ctx.Add(pr);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();
        return (op.Id, pr.Id, proc.Id);
    }

    private static async Task<(Guia guia, ItemGuia item)> SeedGuiaComUmItemAsync(
        AppDbContext ctx, Guid tenantId, Guid prestadorId, Guid operadoraId, Guid procedimentoId, string senha)
    {
        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            senha, new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item);
        await ctx.SaveChangesAsync();
        return (guia, item);
    }

    private static async Task<(Guid demId, Guid itemDemId)> SeedDemComItemAsync(
        DemonstrativoService service, Guid operadoraId, string codigoTuss, decimal valorPago = 500m)
    {
        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-01", new DateOnly(2025, 1, 1), null));
        var demId = criado.Value!.Header.Id;
        var addItem = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SENHAC", codigoTuss, null, valorPago, valorPago, null));
        return (demId, addItem.Value!.Itens[0].Id);
    }

    [Fact]
    public async Task Conciliar_SetaValorLiquidadoNoItemAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var (_, itemGuia) = await SeedGuiaComUmItemAsync(ctx, tenantId, prestadorId, operadoraId, procedimentoId,
            "CNC01" + tenantId.ToString("N")[..4]);
        var (demId, itemDemId) = await SeedDemComItemAsync(service, operadoraId, "40300390", 500m);

        var result = await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuia.Id));

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        var ig = await adminCtx.ItensGuia.FindAsync(itemGuia.Id);
        Assert.Equal(500m, ig!.ValorLiquidado);
    }

    [Fact]
    public async Task Conciliar_TodosItens_GuiaLiquidadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "CNC02" + tenantId.ToString("N")[..4], new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        var item1 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        var item2 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.PrimeiroAuxiliar,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item1);
        ctx.Add(item2);
        await ctx.SaveChangesAsync();

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-02", new DateOnly(2025, 2, 1), null));
        var demId = criado.Value!.Header.Id;
        var add1 = await service.AdicionarItemAsync(demId, new AdicionarItemCommand("SEN1", "40300391", null, 300m, 300m, null));
        var add2 = await service.AdicionarItemAsync(demId, new AdicionarItemCommand("SEN2", "40300392", null, 200m, 200m, null));
        var itemDemId1 = add1.Value!.Itens[0].Id;
        var itemDemId2 = add2.Value!.Itens[1].Id;

        await service.ConciliarItemAsync(demId, itemDemId1, new ConciliarItemCommand(item1.Id));
        await service.ConciliarItemAsync(demId, itemDemId2, new ConciliarItemCommand(item2.Id));

        await using var adminCtx = db.CreateContext();
        var g = await adminCtx.Guias.FindAsync(guia.Id);
        Assert.Equal(SituacaoGuia.Liquidada, g!.Situacao);
    }

    [Fact]
    public async Task Conciliar_ItemParcial_GuiaContinuaApresentadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "CNC03" + tenantId.ToString("N")[..4], new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        var item1 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        var item2 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.PrimeiroAuxiliar,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item1);
        ctx.Add(item2);
        await ctx.SaveChangesAsync();

        var (demId, itemDemId1) = await SeedDemComItemAsync(service, operadoraId, "40300393", 300m);

        await service.ConciliarItemAsync(demId, itemDemId1, new ConciliarItemCommand(item1.Id));

        await using var adminCtx = db.CreateContext();
        var g = await adminCtx.Guias.FindAsync(guia.Id);
        Assert.Equal(SituacaoGuia.Apresentada, g!.Situacao);
    }

    [Fact]
    public async Task Desconciliar_LimpaValorLiquidadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var (_, itemGuia) = await SeedGuiaComUmItemAsync(ctx, tenantId, prestadorId, operadoraId, procedimentoId,
            "CNC04" + tenantId.ToString("N")[..4]);
        var (demId, itemDemId) = await SeedDemComItemAsync(service, operadoraId, "40300394", 400m);

        await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuia.Id));
        var result = await service.DesconciliarItemAsync(demId, itemDemId);

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        var ig = await adminCtx.ItensGuia.FindAsync(itemGuia.Id);
        Assert.Null(ig!.ValorLiquidado);
    }

    [Fact]
    public async Task Desconciliar_ReverteLiquidacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "CNC05" + tenantId.ToString("N")[..4], new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        var item1 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        var item2 = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.PrimeiroAuxiliar,
            OrdemProcedimento.Unico, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item1);
        ctx.Add(item2);
        await ctx.SaveChangesAsync();

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-05", new DateOnly(2025, 5, 1), null));
        var demId = criado.Value!.Header.Id;
        var add1 = await service.AdicionarItemAsync(demId, new AdicionarItemCommand("SEN1D", "40300395", null, 300m, 300m, null));
        var add2 = await service.AdicionarItemAsync(demId, new AdicionarItemCommand("SEN2D", "40300396", null, 200m, 200m, null));
        var itemDemId1 = add1.Value!.Itens[0].Id;
        var itemDemId2 = add2.Value!.Itens[1].Id;

        await service.ConciliarItemAsync(demId, itemDemId1, new ConciliarItemCommand(item1.Id));
        await service.ConciliarItemAsync(demId, itemDemId2, new ConciliarItemCommand(item2.Id));

        await service.DesconciliarItemAsync(demId, itemDemId1);

        await using var adminCtx = db.CreateContext();
        var g = await adminCtx.Guias.FindAsync(guia.Id);
        Assert.Equal(SituacaoGuia.Apresentada, g!.Situacao);
    }

    [Fact]
    public async Task Conciliar_ItemDeTenantDiferente_NaoEncontradoAsync()
    {
        var tenantId = Guid.NewGuid();
        var outroTenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, _, _) = await SeedCatalogAsync(ctx, tenantId);
        var (demId, itemDemId) = await SeedDemComItemAsync(service, operadoraId, "40300397", 500m);

        await using var outroCtx = db.CreateTenantContext(outroTenantId);
        var (opOutro, prOutro, procOutro) = await SeedCatalogAsync(outroCtx, outroTenantId);
        var (_, itemGuiaOutro) = await SeedGuiaComUmItemAsync(outroCtx, outroTenantId,
            prOutro, opOutro, procOutro, "CNC06" + outroTenantId.ToString("N")[..4]);

        var result = await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuiaOutro.Id));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Conciliar_ItemJaConciliado_SubstituivelAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var (_, itemGuiaA) = await SeedGuiaComUmItemAsync(ctx, tenantId, prestadorId, operadoraId, procedimentoId,
            "CNC7A" + tenantId.ToString("N")[..4]);
        var (_, itemGuiaB) = await SeedGuiaComUmItemAsync(ctx, tenantId, prestadorId, operadoraId, procedimentoId,
            "CNC7B" + tenantId.ToString("N")[..4]);
        var (demId, itemDemId) = await SeedDemComItemAsync(service, operadoraId, "40300398", 600m);

        await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuiaA.Id));
        await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuiaB.Id));

        await using var adminCtx = db.CreateContext();
        var igA = await adminCtx.ItensGuia.FindAsync(itemGuiaA.Id);
        var igB = await adminCtx.ItensGuia.FindAsync(itemGuiaB.Id);
        Assert.Null(igA!.ValorLiquidado);
        Assert.Equal(600m, igB!.ValorLiquidado);
    }

    [Fact]
    public async Task Conciliar_GlosaTotal_ValorPagoZero_ItemLiquidadoComZeroAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, service) = BuildService(tenantId);
        await using var _ = ctx;
        var (operadoraId, prestadorId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var (_, itemGuia) = await SeedGuiaComUmItemAsync(ctx, tenantId, prestadorId, operadoraId, procedimentoId,
            "CNC08" + tenantId.ToString("N")[..4]);

        var criado = await service.CriarAsync(new CriarDemonstrativoCommand(
            operadoraId, "2025-08", new DateOnly(2025, 8, 1), null));
        var demId = criado.Value!.Header.Id;
        var addItem = await service.AdicionarItemAsync(demId, new AdicionarItemCommand(
            "SENHAG", "40300399", null, 500m, 0m, "Glosa total"));
        var itemDemId = addItem.Value!.Itens[0].Id;

        await service.ConciliarItemAsync(demId, itemDemId, new ConciliarItemCommand(itemGuia.Id));

        await using var adminCtx = db.CreateContext();
        var ig = await adminCtx.ItensGuia.FindAsync(itemGuia.Id);
        Assert.Equal(0m, ig!.ValorLiquidado);
    }
}

file sealed class FakeTenantUserConc(Guid tenantId) : App.Identity.ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
