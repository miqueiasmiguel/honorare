using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using App.Storage;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class RecursoCrudTests(PostgresContainerFixture db)
{
    private static readonly IFileStorage _noopStorage = new RecursoCrudNoopFileStorage();
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
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), "Obs teste", "00250"));

        Assert.True(result.IsSuccess);
        Assert.Equal(opId, result.Value!.OperadoraId);
        Assert.Equal(prestId, result.Value.PrestadorId);
        Assert.Equal("00250", result.Value.Numero);
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

        var service = new RecursoService(ctx, user, _noopStorage);
        await service.CriarAsync(new CriarRecursoCommand(op1Id, prestId, new DateOnly(2026, 1, 1), null, "202512"));
        await service.CriarAsync(new CriarRecursoCommand(op1Id, prestId, new DateOnly(2026, 2, 1), null, "202512"));
        await service.CriarAsync(new CriarRecursoCommand(op2.Id, prestId, new DateOnly(2026, 3, 1), null, "202512"));

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

        var service = new RecursoService(ctx, user, _noopStorage);
        await service.CriarAsync(new CriarRecursoCommand(opId, prest1Id, new DateOnly(2026, 1, 1), null, "202512"));
        await service.CriarAsync(new CriarRecursoCommand(opId, prest2.Id, new DateOnly(2026, 2, 1), null, "202512"));

        var result = await service.ListarAsync(new ListarRecursosQuery(null, prest1Id, 1, 10));

        Assert.Equal(1, result.Total);
        Assert.Equal(prest1Id, result.Itens[0].PrestadorId);
    }

    [Fact]
    public async Task Deve_PersistirERelerRecursoComTipoGlosaBrancaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 5, 1), null, "20260501",
                TipoRecurso.GlosaBranca));

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(TipoRecurso.GlosaBranca, dto.Tipo);

        var relido = await service.ObterPorIdAsync(dto.Id);
        Assert.True(relido.IsSuccess);
        Assert.Equal(TipoRecurso.GlosaBranca, relido.Value!.Header.Tipo);
    }

    [Fact]
    public async Task Deve_UsarGlosaParcialComoDefaultQuandoTipoNaoEhInformadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 5, 1), null, "20260502"));

        Assert.True(result.IsSuccess);
        Assert.Equal(TipoRecurso.GlosaParcial, result.Value!.Tipo);
    }

    [Fact]
    public async Task Listar_PaginacaoFuncionaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        for (var i = 1; i <= 5; i++)
        {
            await service.CriarAsync(new CriarRecursoCommand(opId, prestId, new DateOnly(2026, i, 1), null, "202512"));
        }

        var pagina1 = await service.ListarAsync(new ListarRecursosQuery(null, null, 1, 2));
        var pagina2 = await service.ListarAsync(new ListarRecursosQuery(null, null, 2, 2));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
    }

    [Fact]
    public async Task Atualizar_CamposAtualizados_NumeroManualPersistidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), "Original", "202512"));
        Assert.True(criado.IsSuccess);

        var updated = await service.AtualizarAsync(criado.Value!.Id,
            new AtualizarRecursoCommand(opId, prestId, new DateOnly(2026, 6, 15), "Atualizado", "00099"));

        Assert.True(updated.IsSuccess);
        Assert.Equal("00099", updated.Value!.Numero);
        Assert.Equal("Atualizado", updated.Value.Observacao);
    }

    [Fact]
    public async Task Criar_NumeroVazio_FalhaValidacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null, "   "));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_NumeroNaoNumerico_FalhaValidacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null, "202601-001"));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_NumeroAcimaDe20Digitos_FalhaValidacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var result = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null, new string('1', 21)));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Excluir_SemGuias_RemovidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"));
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
        var service = new RecursoService(ctx, user, _noopStorage);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"));
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
        var service = new RecursoService(ctx, user, _noopStorage);

        var criado = await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"));
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
        var service = new RecursoService(ctx, user, _noopStorage);

        var rec1Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;
        var rec2Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null, "202512"))).Value!.Id;

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
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

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
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

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
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 3, 1), null, "202512"))).Value!.Id;

        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LA-{pfx}", new DateOnly(2026, 3, 1));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LB-{pfx}", new DateOnly(2026, 3, 15));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LC-{pfx}", new DateOnly(2026, 3, 31));
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LD-{pfx}", new DateOnly(2026, 2, 28));

        var rec2Id = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 4, 1), null, "202512"))).Value!.Id;
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
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 2, 1), null, "202512"))).Value!.Id;

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
        var service = new RecursoService(ctx, user, _noopStorage);

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
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 5, 1), null, "202512"))).Value!.Id;

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

        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 3, 1), null, "202512"))).Value!.Id;

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
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 4, 1), null, "202512"))).Value!.Id;

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

    [Fact]
    public async Task AdicionarEmLote_DevePularGuiaComCodigoNaoRecorrivelAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoConsulta = "10101012";
        var tenant = Tenant.Create("Clínica NR " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoConsulta]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. NR " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED NR " + pfx, null, null, TipoRuleSet.Unimed);
        var procNormal = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Normal NR", "1", null, false, false);
        var procConsulta = Procedimento.Create(tenantId, CodigoConsulta, "Consulta NR", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procNormal);
        ctx.Add(procConsulta);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNormal.Id, 200m));
        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procConsulta.Id, 100m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        var guiaNormalId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNormal.Id, $"NR-N-{pfx}");
        await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procConsulta.Id, $"NR-C-{pfx}");

        var cmd = new AdicionarGuiasEmLoteCommand(prestador.Id, operadora.Id, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        await using var verifyCtx = db.CreateContext();
        var guiaVinculada = await verifyCtx.Guias.FirstOrDefaultAsync(g => g.RecursoId == recursoId);
        Assert.NotNull(guiaVinculada);
        Assert.Equal(guiaNormalId, guiaVinculada.Id);
    }

    [Fact]
    public async Task AdicionarEmLote_DeveVincularTodas_QuandoListaVaziaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LV-A-{pfx}");
        await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"LV-B-{pfx}", new DateOnly(2026, 1, 11));

        var cmd = new AdicionarGuiasEmLoteCommand(prestId, opId, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task AdicionarEmLote_DeveIncluirGuiaMista_ExcluindoItensNaoRecorrivelAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoNr = "GMIX02NR1";
        var tenant = Tenant.Create("Clínica MX-A " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoNr]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. MX-A " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED MX-A " + pfx, null, null, TipoRuleSet.Unimed);
        var procNormal = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Normal MX-A", "1", null, false, false);
        var procNr = Procedimento.Create(tenantId, CodigoNr, "Proc NR MX-A", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procNormal);
        ctx.Add(procNr);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNormal.Id, 200m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNormal.Id, $"MX-A-{pfx}");
        ctx.Add(ItemGuia.Create(guiaId, procNr.Id, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 50m));
        await ctx.SaveChangesAsync();

        var cmd = new AdicionarGuiasEmLoteCommand(prestador.Id, operadora.Id, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        await using var verifyCtx = db.CreateContext();
        var itens = await verifyCtx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        Assert.Equal(2, itens.Count);
        Assert.True(itens.First(i => i.ProcedimentoId == procNormal.Id).IncluidoNoRecurso);
        Assert.False(itens.First(i => i.ProcedimentoId == procNr.Id).IncluidoNoRecurso);
    }

    [Fact]
    public async Task AdicionarEmLote_DevePularGuiaTotalmenteNaoRecorrivelAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoNr = "GMIX02NR2";
        var tenant = Tenant.Create("Clínica MX-B " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoNr]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. MX-B " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED MX-B " + pfx, null, null, TipoRuleSet.Unimed);
        var procNr = Procedimento.Create(tenantId, CodigoNr, "Proc NR MX-B", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procNr);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNr.Id, 100m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNr.Id, $"MX-B-{pfx}");

        var cmd = new AdicionarGuiasEmLoteCommand(prestador.Id, operadora.Id, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        await using var verifyCtx = db.CreateContext();
        Assert.Equal(0, await verifyCtx.Guias.CountAsync(g => g.RecursoId == recursoId));
    }

    [Fact]
    public async Task AdicionarEmLote_DevePularTotalmenteNR_EIncluirMista_NoMesmoLoteAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoNr = "GMIX02NR3";
        var tenant = Tenant.Create("Clínica MX-C " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoNr]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. MX-C " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED MX-C " + pfx, null, null, TipoRuleSet.Unimed);
        var procNormal = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Normal MX-C", "1", null, false, false);
        var procNr = Procedimento.Create(tenantId, CodigoNr, "Proc NR MX-C", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procNormal);
        ctx.Add(procNr);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNormal.Id, 200m));
        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNr.Id, 100m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        // guia totalmente NR: 1 item NR → deve ser bloqueada
        await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNr.Id, $"MX-C-NR-{pfx}");

        // guia mista: 1 item normal + 1 item NR → deve entrar, item NR excluído
        var guiaMistaId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNormal.Id, $"MX-C-MX-{pfx}");
        ctx.Add(ItemGuia.Create(guiaMistaId, procNr.Id, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 50m));

        // guia normal: 1 item normal → deve entrar, todos os itens incluídos
        var guiaNormalId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNormal.Id, $"MX-C-OK-{pfx}");
        await ctx.SaveChangesAsync();

        var cmd = new AdicionarGuiasEmLoteCommand(prestador.Id, operadora.Id, null, null, null, null, null, null);
        var result = await service.AdicionarGuiasEmLoteAsync(recursoId, cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);

        await using var verifyCtx = db.CreateContext();
        Assert.Equal(2, await verifyCtx.Guias.CountAsync(g => g.RecursoId == recursoId));
        Assert.Null((await verifyCtx.Guias.FirstAsync(g => g.PrestadorId == prestador.Id && g.NumeroGuia == $"MX-C-NR-{pfx}")).RecursoId);

        var itensMista = await verifyCtx.ItensGuia.Where(i => i.GuiaId == guiaMistaId).ToListAsync();
        Assert.True(itensMista.First(i => i.ProcedimentoId == procNormal.Id).IncluidoNoRecurso);
        Assert.False(itensMista.First(i => i.ProcedimentoId == procNr.Id).IncluidoNoRecurso);

        var itensNormal = await verifyCtx.ItensGuia.Where(i => i.GuiaId == guiaNormalId).ToListAsync();
        Assert.All(itensNormal, i => Assert.True(i.IncluidoNoRecurso));
    }

    [Fact]
    public async Task AdicionarGuia_DeveVincularGuiaMista_SemExcluirItensAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoNr = "GMIX02NR4";
        var tenant = Tenant.Create("Clínica MX-D " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoNr]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. MX-D " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED MX-D " + pfx, null, null, TipoRuleSet.Unimed);
        var procNormal = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc Normal MX-D", "1", null, false, false);
        var procNr = Procedimento.Create(tenantId, CodigoNr, "Proc NR MX-D", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procNormal);
        ctx.Add(procNr);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procNormal.Id, 200m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procNormal.Id, $"MX-D-{pfx}");
        ctx.Add(ItemGuia.Create(guiaId, procNr.Id, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 50m));
        await ctx.SaveChangesAsync();

        await service.AdicionarGuiaAsync(recursoId, guiaId);

        await using var verifyCtx = db.CreateContext();
        var itens = await verifyCtx.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync();
        Assert.Equal(2, itens.Count);
        Assert.All(itens, i => Assert.True(i.IncluidoNoRecurso));
    }

    [Fact]
    public async Task AdicionarGuia_DeveVincularGuiaNaoRecorrivel_QuandoIndividualAsync()
    {
        await using var adminCtx = db.CreateContext();
        const string CodigoConsulta = "20202020";
        var tenant = Tenant.Create("Clínica ESC " + Guid.NewGuid().ToString("N")[..4]);
        tenant.DefinirCodigosNaoRecorriveis([CodigoConsulta]);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();

        var tenantId = tenant.Id;
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var pfx = tenantId.ToString("N")[..4];

        var prestador = Prestador.Create(tenantId, "Dr. ESC " + pfx, null);
        var operadora = Operadora.Create(tenantId, "UNIMED ESC " + pfx, null, null, TipoRuleSet.Unimed);
        var procConsulta = Procedimento.Create(tenantId, CodigoConsulta, "Consulta ESC", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procConsulta);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procConsulta.Id, 100m));
        await ctx.SaveChangesAsync();

        var service = new RecursoService(ctx, user, _noopStorage);
        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(operadora.Id, prestador.Id, new DateOnly(2026, 1, 1), null, "202512"))).Value!.Id;

        var guiaId = await CriarGuiaAsync(ctx, user, prestador.Id, operadora.Id, procConsulta.Id, $"ESC-{pfx}");
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        await using var verifyCtx = db.CreateContext();
        var guia = await verifyCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId);
        Assert.NotNull(guia);
        Assert.Equal(recursoId, guia.RecursoId);
        Assert.Equal(SituacaoGuia.EmRecurso, guia.Situacao);
    }

    [Fact]
    public async Task ObterPorId_RetornaGuiasComLocalAtendimentoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 5, 1), null, "202512"))).Value!.Id;

        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var guiaResult = await guiaSvc.CriarAsync(new CriarGuiaCommand(
            prestId, opId, null, $"RC-LA-{pfx}", new DateOnly(2026, 5, 5), false, string.Empty,
            [new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao, 1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)],
            "Hospital Santa Casa"));
        await service.AdicionarGuiaAsync(recursoId, guiaResult.Value!.Id);

        var result = await service.ObterPorIdAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hospital Santa Casa", result.Value!.Guias[0].LocalAtendimento);
    }

    [Fact]
    public async Task ObterPorId_GuiasVinculadasOrdenadasPorDataAtendimentoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);
        var pfx = tenantId.ToString("N")[..4];

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 7, 1), null, "202512"))).Value!.Id;

        // criadas fora de ordem cronológica
        var g3 = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"OV-C-{pfx}", new DateOnly(2026, 7, 20));
        var g1 = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"OV-A-{pfx}", new DateOnly(2026, 7, 5));
        var g2 = await CriarGuiaAsync(ctx, user, prestId, opId, procId, $"OV-B-{pfx}", new DateOnly(2026, 7, 12));
        await service.AdicionarGuiaAsync(recursoId, g3);
        await service.AdicionarGuiaAsync(recursoId, g1);
        await service.AdicionarGuiaAsync(recursoId, g2);

        var result = await service.ObterPorIdAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 20)],
            result.Value!.Guias.Select(g => g.DataAtendimento).ToArray());
    }

    [Fact]
    public async Task ObterPorId_RetornaEhPacoteDaGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new RecursoService(ctx, user, _noopStorage);

        var recursoId = (await service.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 8, 1), null, "202512"))).Value!.Id;

        var factory = new PricingRuleSetFactory(ctx);
        var guiaService = new GuiaService(ctx, user, factory);
        var guiaId = (await guiaService.CriarAsync(new CriarGuiaCommand(
            prestId, opId, null, "RE-PKG-" + tenantId.ToString("N")[..4],
            new DateOnly(2026, 8, 10), true, string.Empty,
            [new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao,
                1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 500m)]))).Value!.Id;
        await service.AdicionarGuiaAsync(recursoId, guiaId);

        var result = await service.ObterPorIdAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Guias[0].EhPacote);
    }
}

file sealed class FakeRecursoTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}

file sealed class RecursoCrudNoopFileStorage : IFileStorage
{
    public Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<FileStorageObject?>(null);

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        Task.CompletedTask;
}
