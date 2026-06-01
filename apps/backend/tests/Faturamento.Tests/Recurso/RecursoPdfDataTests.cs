using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Faturamento.Pdf;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class RecursoPdfDataTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakePdfTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    private async Task<Guid> SeedTenantAsync(string name)
    {
        await using var adminCtx = db.CreateContext();
        var tenant = Tenant.Create(name);
        adminCtx.Tenants.Add(tenant);
        await adminCtx.SaveChangesAsync();
        return tenant.Id;
    }

    private static async Task<(Guid opId, Guid prestId, Guid procId)> SeedCatalogAsync(
        AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. PDF " + tenantId.ToString("N")[..4], null);
        var operadora = Operadora.Create(tenantId, "UNIMED PDF " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, tenantId.ToString("N")[..8], "Proc PDF", "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await ctx.SaveChangesAsync();

        return (operadora.Id, prestador.Id, procedimento.Id);
    }

    private static async Task<Guid> CriarRecursoAsync(
        AppDbContext ctx, ICurrentUser user, Guid opId, Guid prestId)
    {
        var svc = new RecursoService(ctx, user);
        var result = await svc.CriarAsync(
            new CriarRecursoCommand(opId, prestId, new DateOnly(2026, 3, 1), null));
        return result.Value!.Id;
    }

    private static async Task CriarGuiaEVincularAsync(
        AppDbContext ctx, ICurrentUser user,
        Guid prestId, Guid opId, Guid procId, string senha, Guid recursoId)
    {
        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestId, opId, null, null, senha, new DateOnly(2026, 3, 5), false, string.Empty,
            [new CriarItemGuiaCommand(
                procId, PosicaoExecutor.Cirurgiao, 1.0m,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);
        var guiaResult = await guiaSvc.CriarAsync(cmd);
        var recursoSvc = new RecursoService(ctx, user);
        await recursoSvc.AdicionarGuiaAsync(recursoId, guiaResult.Value!.Id);
    }

    [Fact]
    public async Task ObterDadosPdf_RetornaTenantNameAsync()
    {
        const string TenantName = "Clínica RE03 Test";
        var tenantId = await SeedTenantAsync(TenantName);
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx, tenantId);
        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);
        var svc = new RecursoService(ctx, user);

        var result = await svc.ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantName, result.Value!.TenantName);
    }

    [Fact]
    public async Task ObterDadosPdf_GuiaComItens_RetornaDadosCorretosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);

        var proc2CodigoTuss = "P2" + tenantId.ToString("N")[..6];
        var proc2 = Procedimento.Create(tenantId, proc2CodigoTuss, "Proc PDF 2", "1", null, false, false);
        ctx.Add(proc2);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, opId, proc2.Id, 150m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestId, opId, PosicaoExecutor.PrimeiroAuxiliar, 100m));
        await ctx.SaveChangesAsync();

        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);

        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestId, opId, null, null, "PDF-2IT-" + tenantId.ToString("N")[..4],
            new DateOnly(2026, 3, 5), false, string.Empty,
            [
                new CriarItemGuiaCommand(procId, PosicaoExecutor.Cirurgiao, 1.0m,
                    ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
                new CriarItemGuiaCommand(proc2.Id, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
                    ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null),
            ]);
        var guiaResult = await guiaSvc.CriarAsync(cmd);
        Assert.True(guiaResult.IsSuccess);

        var recursoSvc = new RecursoService(ctx, user);
        await recursoSvc.AdicionarGuiaAsync(recursoId, guiaResult.Value!.Id);

        var result = await recursoSvc.ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Guias);
        Assert.Equal(2, result.Value.Guias[0].Itens.Count);
        var codigosRetornados = result.Value.Guias[0].Itens.Select(i => i.CodigoTuss).ToList();
        Assert.Contains(tenantId.ToString("N")[..8], codigosRetornados);
        Assert.Contains(proc2CodigoTuss, codigosRetornados);
    }

    [Fact]
    public async Task ObterDadosPdf_ValorPago_NullVirazZeroAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);
        await CriarGuiaEVincularAsync(ctx, user, prestId, opId, procId,
            "PDF-NUL-" + tenantId.ToString("N")[..4], recursoId);

        var result = await new RecursoService(ctx, user).ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Guias[0].Itens, i => Assert.Equal(0m, i.ValorPago));
    }

    [Fact]
    public async Task ObterDadosPdf_FatorEfetivo_SemPassos_RetornaDashAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);

        var factory = new PricingRuleSetFactory(ctx);
        var guiaSvc = new GuiaService(ctx, user, factory);
        var cmd = new CriarGuiaCommand(
            prestId, opId, null, null, "PDF-PKG-" + tenantId.ToString("N")[..4],
            new DateOnly(2026, 3, 5), true, string.Empty,
            [new CriarItemGuiaCommand(
                procId, PosicaoExecutor.Cirurgiao, 1.0m,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 50m)]);
        var guiaResult = await guiaSvc.CriarAsync(cmd);
        Assert.True(guiaResult.IsSuccess);

        var recursoSvc = new RecursoService(ctx, user);
        await recursoSvc.AdicionarGuiaAsync(recursoId, guiaResult.Value!.Id);

        var result = await recursoSvc.ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Guias[0].Itens, i => Assert.Equal("—", i.FatorEfetivo));
    }

    [Fact]
    public async Task ObterDadosPdf_FatorEfetivo_ComPassos_RetornaProdutoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);

        var guia = Guia.Create(tenantId, prestId, opId, null,
            null, "PDF-FAT-" + tenantId.ToString("N")[..4], new DateOnly(2026, 3, 5), false, string.Empty);
        ctx.Guias.Add(guia);
        var item = ItemGuia.Create(guia.Id, procId, PosicaoExecutor.Cirurgiao,
            1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 35m);
        ctx.ItensGuia.Add(item);
        var calculo = Calculo.Create(tenantId, guia.Id);
        ctx.Calculos.Add(calculo);
        ctx.PassosCalculo.Add(PassoCalculo.Create(calculo.Id, item.Id, 1, "ValorBase", 100m, 100m));
        ctx.PassosCalculo.Add(PassoCalculo.Create(calculo.Id, item.Id, 2, "Deflator", 0.7m, 70m));
        ctx.PassosCalculo.Add(PassoCalculo.Create(calculo.Id, item.Id, 3, "Urgência", 0.5m, 35m));
        await ctx.SaveChangesAsync();

        var recursoSvc = new RecursoService(ctx, user);
        await recursoSvc.AdicionarGuiaAsync(recursoId, guia.Id);

        var result = await recursoSvc.ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsSuccess);
        Assert.Equal("35%", result.Value!.Guias[0].Itens[0].FatorEfetivo);
    }

    [Fact]
    public async Task GerarPdf_NaoLancaExcecaoAsync()
    {
        var tenantId = await SeedTenantAsync("Tenant PDF Render " + Guid.NewGuid().ToString("N")[..4]);
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (opId, prestId, procId) = await SeedCatalogAsync(ctx, tenantId);
        var recursoId = await CriarRecursoAsync(ctx, user, opId, prestId);
        await CriarGuiaEVincularAsync(ctx, user, prestId, opId, procId,
            "PDF-RND-" + tenantId.ToString("N")[..4], recursoId);

        var dados = await new RecursoService(ctx, user).ObterDadosPdfAsync(recursoId);
        Assert.True(dados.IsSuccess);

        var doc = new RecursoPdfDocument(dados.Value!);
        var bytes = doc.GeneratePdf();

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task ObterDadosPdf_RecursoDeOutroTenant_NotFoundAsync()
    {
        var tenantId1 = Guid.NewGuid();
        var (ctx1, user1) = BuildTenant(tenantId1);
        await using var _ = ctx1;
        var (opId, prestId, _) = await SeedCatalogAsync(ctx1, tenantId1);
        var recursoId = await CriarRecursoAsync(ctx1, user1, opId, prestId);

        var tenantId2 = Guid.NewGuid();
        var (ctx2, user2) = BuildTenant(tenantId2);
        await using var __ = ctx2;

        var result = await new RecursoService(ctx2, user2).ObterDadosPdfAsync(recursoId);

        Assert.True(result.IsFailure);
    }
}

file sealed class FakePdfTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
