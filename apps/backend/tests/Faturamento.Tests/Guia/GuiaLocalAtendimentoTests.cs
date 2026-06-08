using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.Service;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaLocalAtendimentoTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user, PricingRuleSetFactory factory) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeLocalTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        return (ctx, currentUser, new PricingRuleSetFactory(ctx));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Local Teste", null);
        var operadora = Operadora.Create(tenantId, "UNIMED Local", null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, "LOCAL" + tenantId.ToString("N")[..5].ToUpperInvariant(), "Paciente Local");
        var procedimento = Procedimento.Create(tenantId, "91001" + tenantId.ToString("N")[..5].ToUpperInvariant(), "Consulta Local", "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, beneficiario.Id, procedimento.Id);
    }

    private static CriarItemGuiaCommand ItemPadrao(Guid procedimentoId) =>
        new(procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);

    [Fact]
    public async Task Criar_ComLocalAtendimento_PersisteEExpoeNoDetalheAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            "GUIA-LA01", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)],
            "Hospital São Lucas");

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hospital São Lucas", result.Value!.LocalAtendimento);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == result.Value.Id);
        Assert.Equal("Hospital São Lucas", guia!.LocalAtendimento);
    }

    [Fact]
    public async Task Atualizar_AlteraLocalAtendimentoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var criado = await service.CriarAsync(new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            "GUIA-LA02", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)],
            "Local Original"));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarAsync(criado.Value!.Id, new AtualizarGuiaCommand(
            operadoraId, beneficiarioId, "GUIA-LA02",
            new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)],
            "Clínica Nova"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Clínica Nova", result.Value!.LocalAtendimento);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == criado.Value.Id);
        Assert.Equal("Clínica Nova", guia!.LocalAtendimento);
    }
}

file sealed class FakeLocalTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
