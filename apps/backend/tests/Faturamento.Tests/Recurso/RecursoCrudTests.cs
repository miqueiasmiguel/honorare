using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class RecursoCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeRecursoTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        return (ctx, currentUser);
    }

    private static async Task<(Guid operadoraId, Guid prestadorId, Guid procedimentoId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. RE02 " + tenantId.ToString("N")[..4], null);
        var operadora = Operadora.Create(tenantId, "UNIMED RE02 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc RE02", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await ctx.SaveChangesAsync();

        return (operadora.Id, prestador.Id, procedimento.Id);
    }

    private static async Task<Guid> CriarGuiaAsync(
        AppDbContext ctx, ICurrentUser user,
        Guid prestadorId, Guid operadoraId, Guid procedimentoId,
        string numeroGuia)
    {
        return await CriarGuiaAsync(ctx, user, prestadorId, operadoraId, procedimentoId, numeroGuia,
            new DateOnly(2026, 1, 10));
    }

    private static async Task<Guid> CriarGuiaAsync(
        AppDbContext ctx, ICurrentUser user,
        Guid prestadorId, Guid operadoraId, Guid procedimentoId,
        string numeroGuia, DateOnly dataAtendimento)
    {
        var factory = new PricingRuleSetFactory(ctx);
        var svc = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, null, numeroGuia,
            dataAtendimento, false, string.Empty,
            [new CriarItemGuiaCommand(
                procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);
        var result = await svc.CriarAsync(cmd);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Criar_PersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), "Obs teste"));

        Assert.True(result.IsSuccess);
        Assert.Equal(opId, result.Value!.OperadoraId);
        Assert.Equal(prestId, result.Value.PrestadorId);
        Assert.Equal("202602", result.Value.Numero);
        Assert.Equal("Obs teste", result.Value.Observacao);
    }

    [Fact]
    public async Task Listar_FiltroPorOperadoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (op1Id, prestId, _) = await SeedCatalogAsync(ctx, tenantId);

        var op2 = Operadora.Create(tenantId, "UNIMED2 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        ctx.Add(op2);
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user);
        await service.CriarAsync(new CriarRecursoCommand(op1Id, prestId, new DateOnly(2026, 1, 1), null));
        await service.CriarAsync(new CriarRecursoCommand(op1Id, prestId, new DateOnly(2026, 2, 1), null));
        await service.CriarAsync(new CriarRecursoCommand(op2.Id, prestId, new DateOnly(2026, 3, 1), null));

        var result = await service.ListarAsync(new ListarRecursosQuery(op1Id, null, 1, 10));

        Assert.Equal(2, result.Total);
        Assert.All(result.Itens, r => Assert.Equal(op1Id, r.OperadoraId));
    }

    [Fact]
    public async Task Listar_FiltroPorPrestadorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prest1Id, _) = await SeedCatalogAsync(ctx, tenantId);

        var prest2 = Prestador.Create(tenantId, "Dr. RE02b " + tenantId.ToString("N")[..4], null);
        ctx.Add(prest2);
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user);
        await service.CriarAsync(new CriarRecursoCommand(opId, prest1Id, new DateOnly(2026, 1, 1), null));
        await service.CriarAsync(new CriarRecursoCommand(opId, prest2.Id, new DateOnly(2026, 2, 1), null));

        var result = await service.ListarAsync(new ListarRecursosQuery(null, prest1Id, 1, 10));

        Assert.Equal(1, result.Total);
        Assert.Equal(prest1Id, result.Itens[0].PrestadorId);
    }

    [Fact]
    public async Task Listar_PaginacaoFuncionaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        for (var i = 1; i <= 5; i++)
        {
            await service.CriarAsync(new CriarRecursoCommand(opId, prestId, new DateOnly(2026, i, 1), null));
        }

        var pagina1 = await service.ListarAsync(new ListarRecursosQuery(null, null, 1, 2));
        var pagina2 = await service.ListarAsync(new ListarRecursosQuery(null, null, 2, 2));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
    }

    [Fact]
    public async Task Atualizar_CamposAtualizados_NumeroRecalculadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), "Original"));
        Assert.True(criado.IsSuccess);

        var updated = await service.AtualizarAsync(criado.Value!.Id,
            new AtualizarRecursoCommand(opId, prestId, new DateOnly(2026, 6, 15), "Atualizado"));

        Assert.True(updated.IsSuccess);
        Assert.Equal("202606", updated.Value!.Numero);
        Assert.Equal("Atualizado", updated.Value.Observacao);
    }

    [Fact]
    public async Task Excluir_SemGuias_RemovidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null));
        Assert.True(criado.IsSuccess);
        var recursoId = criado.Value!.Id;

        var result = await service.ExcluirAsync(recursoId);

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        Assert.Null(await adminCtx.Recursos.FirstOrDefaultAsync(r => r.Id == recursoId));
    }

    [Fact]
    public async Task Excluir_ComGuia_Lanca409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null));
        var recursoId = criado.Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId,
            "RE-EX-" + tenantId.ToString("N")[..4]);
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExcluirAsync(recursoId));
    }

    [Fact]
    public async Task AdicionarGuia_MudaSituacaoParaEmRecursoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null));
        var recursoId = criado.Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId,
            "RE-ADD-" + tenantId.ToString("N")[..4]);
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(SituacaoGuia.EmRecurso, guia.Situacao);
        Assert.Equal(recursoId, guia.RecursoId);
    }

    [Fact]
    public async Task AdicionarGuia_JaEmOutroRecurso_Lanca409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var rec1Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null))).Value!.Id;
        var rec2Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId,
            "RE-DUP-" + tenantId.ToString("N")[..4]);
        await service.AdicionarGuiaAsync(rec1Id, guiaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AdicionarGuiaAsync(rec2Id, guiaId));
    }

    [Fact]
    public async Task RemoverGuia_SemValorLiquidado_VoltaApresentadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId,
            "RE-REM-" + tenantId.ToString("N")[..4]);
        await service.AdicionarGuiaAsync(recursoId, guiaId);
        await service.RemoverGuiaAsync(recursoId, guiaId);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(SituacaoGuia.Apresentada, guia.Situacao);
        Assert.Null(guia.RecursoId);
    }

    [Fact]
    public async Task RemoverGuia_TodosLiquidados_VoltaLiquidadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestId, opId, procId,
            "RE-LIQ-" + tenantId.ToString("N")[..4]);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        foreach (var item in itens)
        {
            item.SetValorLiquidado(100m);
        }

        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);
        await service.RemoverGuiaAsync(recursoId, guiaId);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(SituacaoGuia.Liquidada, guia.Situacao);
        Assert.Null(guia.RecursoId);
    }

    [Fact]
    public async Task AdicionarEmLote_FiltroPorPeriodo_AdicionaTodasDoPeriodoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 3, 1), null))).Value!.Id;

        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LA-{pfx}", new DateOnly(2026, 3, 1));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LB-{pfx}", new DateOnly(2026, 3, 15));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LC-{pfx}", new DateOnly(2026, 3, 31));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LD-{pfx}", new DateOnly(2026, 2, 28));

        var rec2Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 4, 1), null))).Value!.Id;
        var guia5Id = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LE-{pfx}", new DateOnly(2026, 3, 10));
        await service.AdicionarGuiaAsync(rec2Id, guia5Id);

        var cmd = new AdicionarGuiasEmLoteCommand(
            prestId, opId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
        await using var adminCtx = db.CreateContext();
        Assert.Equal(3, await adminCtx.Guias.CountAsync(g => g.RecursoId == recursoId));
    }

    [Fact]
    public async Task AdicionarEmLote_GuiaJaVinculadaAoMesmoRecurso_IgnoraSilenciosamenteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null))).Value!.Id;

        var guia1Id = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LM-A-{pfx}", new DateOnly(2026, 2, 5));
        await service.AdicionarGuiaAsync(recursoId, guia1Id);
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LM-B-{pfx}", new DateOnly(2026, 2, 10));

        var cmd = new AdicionarGuiasEmLoteCommand(prestId, opId, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        await using var adminCtx = db.CreateContext();
        Assert.Equal(2, await adminCtx.Guias.CountAsync(g => g.RecursoId == recursoId));
    }

    [Fact]
    public async Task AdicionarEmLote_RecursoNaoEncontrado_FalhaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);

        var cmd = new AdicionarGuiasEmLoteCommand(prestId, opId, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(Guid.NewGuid(), cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task AdicionarEmLote_SomenteComGlosa_AdicionaApenasDivergentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 5, 1), null))).Value!.Id;

        var guiaAId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LG-A-{pfx}", new DateOnly(2026, 5, 5));
        var itemA = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaAId);
        itemA.SetValorApurado(100m);
        itemA.SetValorLiquidado(80m);

        var guiaBId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LG-B-{pfx}", new DateOnly(2026, 5, 6));
        var itemB = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaBId);
        itemB.SetValorApurado(100m);
        itemB.SetValorLiquidado(100m);

        var guiaCId = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LG-C-{pfx}", new DateOnly(2026, 5, 7));
        var itemC = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaCId);
        itemC.SetValorApurado(100m);

        await ctx.SaveChangesAsync();

        var cmd = new AdicionarGuiasEmLoteCommand(prestId, opId, null, null, null, null, null, true);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        await using var adminCtx = db.CreateContext();
        var guiaVinculada = await adminCtx.Guias.FirstOrDefaultAsync(g => g.RecursoId == recursoId);
        Assert.NotNull(guiaVinculada);
        Assert.Equal(guiaAId, guiaVinculada.Id);
    }

    [Fact]
    public async Task ObterPorId_RetornaGuiasComItensAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);

        ctx.Add(DeflatorPrestador.Create(tenantId, prestId, opId, PosicaoExecutor.PrimeiroAuxiliar, 70m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 3, 1), null))).Value!.Id;

        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var guiaResult = await guiaSvc.CriarAsync(new CriarGuiaCommand(
            prestId, opId, null, $"RC05-A-{pfx}", new DateOnly(2026, 3, 10), false, string.Empty,
            [
                new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao, 1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(procId, PosicaoExecutor.PrimeiroAuxiliar, 0.3m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
            ]));
        var guiaId = guiaResult.Value!.Id;
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var itens = await ctx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        itens[0].SetValorApurado(100m);
        itens[0].SetValorLiquidado(80m);
        itens[1].SetValorApurado(null);
        await ctx.SaveChangesAsync();

        var result = await service.ObterPorIdAsync(recursoId);

        Assert.True(result.IsSuccess);
        var guia = Assert.Single(result.Value!.Guias);
        Assert.Equal(2, guia.Itens.Count);

        var itemComValor = guia.Itens.First(i => i.ValorApurado == 100m);
        Assert.Equal(80m, itemComValor.ValorLiquidado);
        Assert.Contains(guia.Itens, i => i.ValorApurado == null);
    }

    [Fact]
    public async Task ObterPorId_RetornaGuiasComObservacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 4, 1), null))).Value!.Id;

        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var guiaResult = await guiaSvc.CriarAsync(new CriarGuiaCommand(
            prestId, opId, null, $"RC05-OBS-{pfx}", new DateOnly(2026, 4, 5), false,
            "Guia glosada indevidamente",
            [new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao, 1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]));
        var guiaId = guiaResult.Value!.Id;
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var result = await service.ObterPorIdAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Guia glosada indevidamente", result.Value!.Guias[0].Observacao);
    }
}

file sealed class FakeRecursoTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
