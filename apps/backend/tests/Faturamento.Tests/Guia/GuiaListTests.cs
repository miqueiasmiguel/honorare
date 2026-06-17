using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaListTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user, PricingRuleSetFactory factory) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeListTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        return (ctx, currentUser, new PricingRuleSetFactory(ctx));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var prestador = Prestador.Create(tenantId, "Dr. List " + uid, null);
        var operadora = Operadora.Create(tenantId, "UNIMED List " + uid, null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, uid, "Paciente List");
        var procedimento = Procedimento.Create(tenantId, uid, "Proc List", "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, beneficiario.Id, procedimento.Id);
    }

    private static CriarItemGuiaCommand ItemPadrao(Guid procedimentoId) =>
        new(procedimentoId, PosicaoExecutor.Cirurgiao,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);

    private static async Task<Guid> CriarGuiaAsync(
        GuiaService service,
        Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId,
        string numeroGuia, DateOnly data)
    {
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            numeroGuia, data, false, string.Empty,
            [ItemPadrao(procedimentoId)]);
        var result = await service.CriarAsync(cmd);
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Listar_FiltraPorPrestadorIdAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorA, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var prestadorB = Prestador.Create(tenantId, "Dr. B List", null);
        ctx.Add(prestadorB);
        await ctx.SaveChangesAsync();
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorA, operadoraId, beneficiarioId, procedimentoId, "SFA001", new DateOnly(2025, 1, 10));
        await CriarGuiaAsync(service, prestadorA, operadoraId, beneficiarioId, procedimentoId, "SFA002", new DateOnly(2025, 1, 11));
        await CriarGuiaAsync(service, prestadorB.Id, operadoraId, beneficiarioId, procedimentoId, "SFB001", new DateOnly(2025, 1, 12));

        var result = await service.ListarAsync(new ListarGuiasQuery(prestadorA, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(2, result.Total);
        Assert.All(result.Itens, g => Assert.Equal(prestadorA, g.PrestadorId));
    }

    [Fact]
    public async Task Listar_FiltraPorPeriodoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFP001", new DateOnly(2025, 1, 15));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFP002", new DateOnly(2025, 3, 15));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, new DateOnly(2025, 2, 1), new DateOnly(2025, 4, 1), null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal(new DateOnly(2025, 3, 15), result.Itens[0].DataAtendimento);
    }

    [Fact]
    public async Task Listar_FiltraPorSituacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFS001", new DateOnly(2025, 6, 1));

        var comApresentada = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, SituacaoGuia.Apresentada, null, null, null, null, 1, 20));
        var comLiquidada = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, SituacaoGuia.Liquidada, null, null, null, null, 1, 20));

        Assert.Equal(1, comApresentada.Total);
        Assert.Equal(0, comLiquidada.Total);
    }

    [Fact]
    public async Task Listar_PaginacaoCorretaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        for (var i = 0; i < 5; i++)
        {
            await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId,
                $"SFPAG{i:D2}", new DateOnly(2025, 1, i + 1));
        }

        var pagina1 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 2));
        var pagina2 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 2, 2));
        var pagina3 = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 3, 2));

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.Single(pagina3.Itens);
    }

    [Fact]
    public async Task Listar_SoRetornaGuiasDoPropriTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA, factoryA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var (prestadorA, operadoraA, beneficiarioA, procedimentoA) = await SeedCatalogAsync(ctxA, tenantA);
        var serviceA = new GuiaService(ctxA, userA, factoryA);
        await CriarGuiaAsync(serviceA, prestadorA, operadoraA, beneficiarioA, procedimentoA, "SFISO-A", new DateOnly(2025, 6, 1));

        var (ctxB, userB, factoryB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var (prestadorB, operadoraB, beneficiarioB, procedimentoB) = await SeedCatalogAsync(ctxB, tenantB);
        var serviceB = new GuiaService(ctxB, userB, factoryB);
        await CriarGuiaAsync(serviceB, prestadorB, operadoraB, beneficiarioB, procedimentoB, "SFISO-B", new DateOnly(2025, 6, 2));

        var resultA = await serviceA.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 100));

        Assert.Contains(resultA.Itens, g => g.NumeroGuia == "SFISO-A");
        Assert.DoesNotContain(resultA.Itens, g => g.NumeroGuia == "SFISO-B");
    }

    [Fact]
    public async Task Listar_FiltraPorOperadoraIdAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraA, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var opB = Operadora.Create(tenantId, "UNIMED OpB " + uid, null, null, TipoRuleSet.Unimed);
        ctx.Add(opB);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, opB.Id, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraA, beneficiarioId, procedimentoId, "SFOPA001", new DateOnly(2025, 6, 1));
        await CriarGuiaAsync(service, prestadorId, opB.Id, beneficiarioId, procedimentoId, "SFOPB001", new DateOnly(2025, 6, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, operadoraA, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal(operadoraA, result.Itens[0].OperadoraId);
    }

    [Fact]
    public async Task Listar_FiltraPorNumeroGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "ABC123", new DateOnly(2025, 6, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "XYZ789", new DateOnly(2025, 6, 2));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "ABCxxx", new DateOnly(2025, 6, 3));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, "ABC", null, null, null, 1, 20));

        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Listar_FiltraPorBeneficiarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, _, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var maria = Beneficiario.Create(tenantId, uid + "M", "Maria Silva");
        var joao = Beneficiario.Create(tenantId, uid + "J", "João Costa");
        ctx.Add(maria);
        ctx.Add(joao);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, maria.Id, procedimentoId, "SFBEM001", new DateOnly(2025, 6, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, joao.Id, procedimentoId, "SFBEM002", new DateOnly(2025, 6, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, "Maria", null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal("Maria Silva", result.Itens[0].BeneficiarioNome);
    }

    [Fact]
    public async Task Listar_FiltraSemRecursoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var g1Id = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFREC001", new DateOnly(2025, 6, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFREC002", new DateOnly(2025, 6, 2));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFREC003", new DateOnly(2025, 6, 3));

        var recurso = Recurso.Create(tenantId, operadoraId, prestadorId, new DateOnly(2025, 6, 1), null, "202505");
        ctx.Add(recurso);
        var g1 = await ctx.Guias.FirstAsync(g => g.Id == g1Id);
        g1.MarcarEmRecurso(recurso.Id);
        await ctx.SaveChangesAsync();

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, true, null, 1, 20));

        Assert.Equal(2, result.Total);
        Assert.DoesNotContain(result.Itens, g => g.Id == g1Id);
    }

    [Fact]
    public async Task Listar_FiltraSomenteComGlosaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var guiaAId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFGLO001", new DateOnly(2025, 6, 1));
        var guiaBId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFGLO002", new DateOnly(2025, 6, 2));
        var guiaCId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFGLO003", new DateOnly(2025, 6, 3));

        var itemA = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaAId);
        itemA.SetValorApurado(100m);
        itemA.SetValorLiquidado(80m);

        var itemB = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaBId);
        itemB.SetValorApurado(100m);
        itemB.SetValorLiquidado(100m);

        var itemC = await ctx.ItensGuia.FirstAsync(i => i.GuiaId == guiaCId);
        itemC.SetValorApurado(100m);

        await ctx.SaveChangesAsync();

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, true, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal(guiaAId, result.Itens[0].Id);
    }

    [Fact]
    public async Task Listar_CombinarOperadoraESemRecursoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraA, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var opB = Operadora.Create(tenantId, "UNIMED OpB2 " + uid, null, null, TipoRuleSet.Unimed);
        ctx.Add(opB);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, opB.Id, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var g1Id = await CriarGuiaAsync(service, prestadorId, operadoraA, beneficiarioId, procedimentoId, "SFCOMB01", new DateOnly(2025, 6, 1));
        var g2Id = await CriarGuiaAsync(service, prestadorId, operadoraA, beneficiarioId, procedimentoId, "SFCOMB02", new DateOnly(2025, 6, 2));
        await CriarGuiaAsync(service, prestadorId, opB.Id, beneficiarioId, procedimentoId, "SFCOMB03", new DateOnly(2025, 6, 3));
        await CriarGuiaAsync(service, prestadorId, opB.Id, beneficiarioId, procedimentoId, "SFCOMB04", new DateOnly(2025, 6, 4));

        var recurso = Recurso.Create(tenantId, operadoraA, prestadorId, new DateOnly(2025, 6, 1), null, "202505");
        ctx.Add(recurso);
        var g2 = await ctx.Guias.FirstAsync(g => g.Id == g2Id);
        g2.MarcarEmRecurso(recurso.Id);
        await ctx.SaveChangesAsync();

        var result = await service.ListarAsync(new ListarGuiasQuery(null, operadoraA, null, null, null, null, null, true, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal(g1Id, result.Itens[0].Id);
    }

    [Fact]
    public async Task Listar_OrdenacaoPadraoEhDataDescAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFORD01", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFORD02", new DateOnly(2025, 1, 2));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFORD03", new DateOnly(2025, 1, 3));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(
            [new DateOnly(2025, 1, 3), new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 1)],
            result.Itens.Select(g => g.DataAtendimento).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorDataAtendimentoAscAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFDA01", new DateOnly(2025, 1, 3));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFDA02", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFDA03", new DateOnly(2025, 1, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.DataAtendimento, false));

        Assert.Equal(
            [new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 3)],
            result.Itens.Select(g => g.DataAtendimento).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorNumeroGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "NUM03", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "NUM01", new DateOnly(2025, 1, 2));
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "NUM02", new DateOnly(2025, 1, 3));

        var asc = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, "NUM", null, null, null, 1, 20,
            GuiaOrdenacao.NumeroGuia, false));
        var desc = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, "NUM", null, null, null, 1, 20,
            GuiaOrdenacao.NumeroGuia, true));

        Assert.Equal(["NUM01", "NUM02", "NUM03"], asc.Itens.Select(g => g.NumeroGuia).ToArray());
        Assert.Equal(["NUM03", "NUM02", "NUM01"], desc.Itens.Select(g => g.NumeroGuia).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorBeneficiarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, _, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();
        var ana = Beneficiario.Create(tenantId, uid + "A", "Ana Ordena");
        var bruno = Beneficiario.Create(tenantId, uid + "B", "Bruno Ordena");
        ctx.Add(ana);
        ctx.Add(bruno);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, bruno.Id, procedimentoId, "SFBO01", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, prestadorId, operadoraId, ana.Id, procedimentoId, "SFBO02", new DateOnly(2025, 1, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.Beneficiario, false));

        Assert.Equal(["Ana Ordena", "Bruno Ordena"], result.Itens.Select(g => g.BeneficiarioNome!).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorPrestadorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (_, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var aaa = Prestador.Create(tenantId, "AAA Prestador Ordena", null);
        var zzz = Prestador.Create(tenantId, "ZZZ Prestador Ordena", null);
        ctx.Add(aaa);
        ctx.Add(zzz);
        await ctx.SaveChangesAsync();
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, zzz.Id, operadoraId, beneficiarioId, procedimentoId, "SFPO01", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, aaa.Id, operadoraId, beneficiarioId, procedimentoId, "SFPO02", new DateOnly(2025, 1, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.Prestador, false));

        Assert.Equal(
            ["AAA Prestador Ordena", "ZZZ Prestador Ordena"],
            result.Itens.Select(g => g.PrestadorNome).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorOperadoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, _, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        var aaa = Operadora.Create(tenantId, "AAA Operadora Ordena", null, null, TipoRuleSet.Unimed);
        var zzz = Operadora.Create(tenantId, "ZZZ Operadora Ordena", null, null, TipoRuleSet.Unimed);
        ctx.Add(aaa);
        ctx.Add(zzz);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, aaa.Id, procedimentoId, 200m));
        ctx.Add(TabelaProcedimento.Create(tenantId, zzz.Id, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, zzz.Id, beneficiarioId, procedimentoId, "SFOO01", new DateOnly(2025, 1, 1));
        await CriarGuiaAsync(service, prestadorId, aaa.Id, beneficiarioId, procedimentoId, "SFOO02", new DateOnly(2025, 1, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.Operadora, false));

        Assert.Equal(
            ["AAA Operadora Ordena", "ZZZ Operadora Ordena"],
            result.Itens.Select(g => g.OperadoraNome).ToArray());
    }

    [Fact]
    public async Task Listar_OrdenaPorSituacaoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var apresentadaId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFSO01", new DateOnly(2025, 1, 1));
        var emRecursoId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFSO02", new DateOnly(2025, 1, 2));

        var recurso = Recurso.Create(tenantId, operadoraId, prestadorId, new DateOnly(2025, 1, 1), null, "202412");
        ctx.Add(recurso);
        var emRecurso = await ctx.Guias.FirstAsync(g => g.Id == emRecursoId);
        emRecurso.MarcarEmRecurso(recurso.Id);
        await ctx.SaveChangesAsync();

        var asc = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.Situacao, false));

        // Apresentada (0) antes de EmRecurso (2)
        Assert.Equal([apresentadaId, emRecursoId], asc.Itens.Select(g => g.Id).ToArray());
    }

    [Fact]
    public async Task Listar_DesempataPorCriadoEmDescEmDatasIguaisAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var data = new DateOnly(2025, 5, 20);
        var primeiraId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFTIE01", data);
        var segundaId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFTIE02", data);
        var terceiraId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFTIE03", data);

        var result = await service.ListarAsync(new ListarGuiasQuery(
            null, null, null, null, null, null, null, null, null, 1, 20,
            GuiaOrdenacao.DataAtendimento, true));

        // datas iguais → mais recente (CriadoEm desc) primeiro
        Assert.Equal([terceiraId, segundaId, primeiraId], result.Itens.Select(g => g.Id).ToArray());
    }

    [Fact]
    public async Task ListarAsync_DeveMarcarNaoRecorrivel_QuandoGuiaTemItemComCodigoNaListaAsync()
    {
        var tenant = Tenant.Create("Tenant NR Mark");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();

        tenant.DefinirCodigosNaoRecorriveis([uid]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFNR001", new DateOnly(2025, 6, 1));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.True(result.Itens[0].NaoRecorrivel);
    }

    [Fact]
    public async Task ListarAsync_NaoDeveMarcar_QuandoCodigoNaoEstaNaListaAsync()
    {
        var tenant = Tenant.Create("Tenant NR No Mark");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        // código diferente do usado pelos itens
        tenant.DefinirCodigosNaoRecorriveis(["99999999"]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFNR002", new DateOnly(2025, 6, 2));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.False(result.Itens[0].NaoRecorrivel);
    }

    [Fact]
    public async Task ListarAsync_NaoDeveExcluirGuia_MesmoSendoNaoRecorrivel_Async()
    {
        var tenant = Tenant.Create("Tenant NR No Exclude");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();

        tenant.DefinirCodigosNaoRecorriveis([uid]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var guiaNrId = await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFNR003", new DateOnly(2025, 6, 3));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Contains(result.Itens, g => g.Id == guiaNrId);
        Assert.True(result.Itens.First(g => g.Id == guiaNrId).NaoRecorrivel);
    }

    [Fact]
    public async Task ListarAsync_DeveMarcarNaoRecorrivel_QuandoTodosOsItensEstaoNaListaAsync()
    {
        var tenant = Tenant.Create("Tenant NR Todos");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();

        tenant.DefinirCodigosNaoRecorriveis([uid]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFNRT01", new DateOnly(2025, 6, 1));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.True(result.Itens[0].NaoRecorrivel);
        Assert.False(result.Itens[0].MistaComNaoRecorriveis);
    }

    [Fact]
    public async Task ListarAsync_DeveMarcarMista_QuandoApenasAlgumItemEstaNaListaAsync()
    {
        var tenant = Tenant.Create("Tenant NR Mista");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();

        var codigoRecorrivel = uid[..7] + "X";
        var procRecorrivel = Procedimento.Create(tenantId, codigoRecorrivel, "Proc Recorrivel", "1", null, false, false);
        ctx.Add(procRecorrivel);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procRecorrivel.Id, 200m));
        await ctx.SaveChangesAsync();

        tenant.DefinirCodigosNaoRecorriveis([uid]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            "SFMISTA01", new DateOnly(2025, 6, 1), false, string.Empty,
            [ItemPadrao(procedimentoId), ItemPadrao(procRecorrivel.Id)]);
        var criarResult = await service.CriarAsync(cmd);
        Assert.True(criarResult.IsSuccess);

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.False(result.Itens[0].NaoRecorrivel);
        Assert.True(result.Itens[0].MistaComNaoRecorriveis);
    }

    [Fact]
    public async Task ListarAsync_NaoDeveMarcarNada_QuandoNenhumItemEstaNaListaAsync()
    {
        var tenant = Tenant.Create("Tenant NR Nada");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);

        tenant.DefinirCodigosNaoRecorriveis(["99999999"]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        await CriarGuiaAsync(service, prestadorId, operadoraId, beneficiarioId, procedimentoId, "SFNADA01", new DateOnly(2025, 6, 1));

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.False(result.Itens[0].NaoRecorrivel);
        Assert.False(result.Itens[0].MistaComNaoRecorriveis);
    }

    [Fact]
    public async Task ListarAsync_NaoDeveExcluirGuiaMista_MesmoSendoMistaAsync()
    {
        var tenant = Tenant.Create("Tenant NR Mista Keep");
        var tenantId = tenant.Id;
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var uid = tenantId.ToString("N")[..8].ToUpperInvariant();

        var codigoRecorrivel = uid[..7] + "Y";
        var procRecorrivel = Procedimento.Create(tenantId, codigoRecorrivel, "Proc Recorrivel Keep", "1", null, false, false);
        ctx.Add(procRecorrivel);
        await ctx.SaveChangesAsync();
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procRecorrivel.Id, 200m));
        await ctx.SaveChangesAsync();

        tenant.DefinirCodigosNaoRecorriveis([uid]);
        await ctx.SaveChangesAsync();

        var service = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            "SFMISTAEX", new DateOnly(2025, 6, 1), false, string.Empty,
            [ItemPadrao(procedimentoId), ItemPadrao(procRecorrivel.Id)]);
        var criarResult = await service.CriarAsync(cmd);
        Assert.True(criarResult.IsSuccess);
        var guiaId = criarResult.Value!.Id;

        var result = await service.ListarAsync(new ListarGuiasQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Contains(result.Itens, g => g.Id == guiaId);
        Assert.True(result.Itens.First(g => g.Id == guiaId).MistaComNaoRecorriveis);
    }
}

file sealed class FakeListTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
