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
public sealed class GuiaCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user, PricingRuleSetFactory factory) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        return (ctx, currentUser, new PricingRuleSetFactory(ctx));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)>
        SeedCatalogAsync(AppDbContext ctx, Guid tenantId)
    {
        var prestador = Prestador.Create(tenantId, "Dr. Crud Teste", null);
        var operadora = Operadora.Create(tenantId, "UNIMED Crud", null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, "CRUD" + tenantId.ToString("N")[..6].ToUpperInvariant(), "Paciente Crud");
        var procedimento = Procedimento.Create(tenantId, "90001" + tenantId.ToString("N")[..5].ToUpperInvariant(), "Consulta Crud", "1", null, false, false);

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

    private static CriarItemGuiaCommand ItemPadrao(Guid procedimentoId, decimal? valorApurado = null) =>
        new(procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, valorApurado);

    [Fact]
    public async Task Criar_ComDadosValidos_RetornaGuiaDetalheDtoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN001", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal(prestadorId, result.Value!.PrestadorId);
        Assert.Equal(operadoraId, result.Value.OperadoraId);
        Assert.Equal(beneficiarioId, result.Value.BeneficiarioId);
        Assert.Equal("SEN001", result.Value.Senha);
        Assert.Equal(SituacaoGuia.Apresentada, result.Value.Situacao);
        Assert.Single(result.Value.Itens);
    }

    [Fact]
    public async Task Criar_SemItens_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, _) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN002", new DateOnly(2025, 6, 1), false, "Obs",
            []);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_EhPacoteItemSemValorApurado_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN003", new DateOnly(2025, 6, 1), true, "Obs",
            [ItemPadrao(procedimentoId, null)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_PrestadorInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (_, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            Guid.NewGuid(), operadoraId, beneficiarioId,
            null, "SEN004", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Criar_OperadoraInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, _, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, Guid.NewGuid(), beneficiarioId,
            null, "SEN005", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Criar_BeneficiarioInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, _, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, Guid.NewGuid(),
            null, "SEN006", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);

        var result = await service.CriarAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_GuiaInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (_, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new AtualizarGuiaCommand(
            operadoraId, beneficiarioId, null, "SEN-UPD",
            new DateOnly(2025, 7, 1), false, "Obs atualizada",
            [ItemPadrao(procedimentoId)]);

        var result = await service.AtualizarAsync(Guid.NewGuid(), cmd);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_SubstituiTodosItensAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var criar = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-SUBST", new DateOnly(2025, 6, 1), false, "Original",
            [ItemPadrao(procedimentoId)]);
        var criado = await service.CriarAsync(criar);
        Assert.True(criado.IsSuccess);

        var atualizar = new AtualizarGuiaCommand(
            operadoraId, beneficiarioId, null, "SEN-SUBST2",
            new DateOnly(2025, 6, 15), false, "Atualizado",
            [ItemPadrao(procedimentoId), ItemPadrao(procedimentoId)]);

        var result = await service.AtualizarAsync(criado.Value!.Id, atualizar);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Itens.Count);
        Assert.Equal(2, result.Value.TotalItens);
    }

    [Fact]
    public async Task Excluir_GuiaInexistente_RetornaNotFoundAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new GuiaService(ctx, user, factory);

        var result = await service.ExcluirAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Excluir_RemoveGuiaEItensAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-DEL", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);
        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);
        var guiaId = criado.Value!.Id;

        var result = await service.ExcluirAsync(guiaId);

        Assert.True(result.IsSuccess);

        await using var adminCtx = db.CreateContext();
        Assert.Null(await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == guiaId));
        Assert.Equal(0, await adminCtx.ItensGuia.CountAsync(i => i.GuiaId == guiaId));
    }

    [Fact]
    public async Task AtualizarObservacao_SalvaTextoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var criar = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-OBS", new DateOnly(2025, 6, 1), false, "",
            [ItemPadrao(procedimentoId)]);
        var criado = await service.CriarAsync(criar);
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarObservacaoAsync(
            criado.Value!.Id, new("Procedimento não coberto pela tabela"));

        Assert.True(result.IsSuccess);

        await using var adminCtx = db.CreateContext();
        var guia = await adminCtx.Guias.FirstOrDefaultAsync(g => g.Id == criado.Value.Id);
        Assert.Equal("Procedimento não coberto pela tabela", guia!.Observacao);
    }

    [Fact]
    public async Task AtualizarObservacao_RetornaNotFoundParaGuiaInexistenteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new GuiaService(ctx, user, factory);

        var result = await service.AtualizarObservacaoAsync(Guid.NewGuid(), new("Texto"));

        Assert.False(result.IsSuccess);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task AtualizarValorApurado_SalvaValorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-VA1", new DateOnly(2025, 6, 1), false, "",
            [ItemPadrao(procedimentoId)]);
        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);
        var guiaId = criado.Value!.Id;
        var itemId = criado.Value.Itens[0].Id;

        var result = await service.AtualizarValorApuradoItemAsync(guiaId, itemId, new(150.75m));

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        var item = await adminCtx.ItensGuia.FirstOrDefaultAsync(i => i.Id == itemId);
        Assert.Equal(150.75m, item!.ValorApurado);
    }

    [Fact]
    public async Task AtualizarValorApurado_LimpaValorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-VA2", new DateOnly(2025, 6, 1), false, "",
            [ItemPadrao(procedimentoId)]);
        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);
        var guiaId = criado.Value!.Id;
        var itemId = criado.Value.Itens[0].Id;

        var result = await service.AtualizarValorApuradoItemAsync(guiaId, itemId, new(null));

        Assert.True(result.IsSuccess);
        await using var adminCtx = db.CreateContext();
        var item = await adminCtx.ItensGuia.FirstOrDefaultAsync(i => i.Id == itemId);
        Assert.Null(item!.ValorApurado);
    }

    [Fact]
    public async Task AtualizarValorApurado_RetornaNotFoundSeItemNaoPertenceAGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user, factory) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctx, tenantId);
        var service = new GuiaService(ctx, user, factory);

        var g1 = await service.CriarAsync(new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-VA3", new DateOnly(2025, 6, 1), false, "",
            [ItemPadrao(procedimentoId)]));
        Assert.True(g1.IsSuccess);

        var g2 = await service.CriarAsync(new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-VA4", new DateOnly(2025, 6, 1), false, "",
            [ItemPadrao(procedimentoId)]));
        Assert.True(g2.IsSuccess);

        var result = await service.AtualizarValorApuradoItemAsync(
            g1.Value!.Id, g2.Value!.Itens[0].Id, new(100m));

        Assert.False(result.IsSuccess);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task ObterPorId_TenantDiferente_RetornaNotFoundAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA, factoryA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync(ctxA, tenantA);
        var serviceA = new GuiaService(ctxA, userA, factoryA);

        var cmd = new CriarGuiaCommand(
            prestadorId, operadoraId, beneficiarioId,
            null, "SEN-ISO", new DateOnly(2025, 6, 1), false, "Obs",
            [ItemPadrao(procedimentoId)]);
        var criado = await serviceA.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var (ctxB, userB, factoryB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new GuiaService(ctxB, userB, factoryB);

        var result = await serviceB.ObterPorIdAsync(criado.Value!.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }
}

file sealed class FakeTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
