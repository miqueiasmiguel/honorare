using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Procedimento;

[Collection(nameof(PostgresCollection))]
public sealed class ProcedimentoCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakeProcedimentoUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    [Fact]
    public async Task CriarProcedimento_DadosMinimosValidos_RetornaProcedimentoCriadoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30715013", "Herniorrafia inguinal", null, null, false, false, true));

        Assert.True(result.IsSuccess);
        Assert.Equal("30715013", result.Value!.CodigoTuss);
        Assert.Equal("Herniorrafia inguinal", result.Value.Descricao);
        Assert.True(result.Value.Ativo);
    }

    [Fact]
    public async Task CriarProcedimento_CodigoTussFaltando_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("   ", "Descricao", null, null, false, false, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarProcedimento_CodigoTussComMaisDe10Chars_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("12345678901", "Descricao", null, null, false, false, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarProcedimento_CodigoTussDuplicadoNoMesmoTenant_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);
        const string CodigoTussDuplicado = "30715013";

        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(CodigoTussDuplicado, "Desc A", null, null, false, false, true));
        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(CodigoTussDuplicado, "Desc B", null, null, false, false, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task CriarProcedimento_CodigoTussDuplicadoEmTenantDistinto_PermitidoAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string CodigoTussCompartilhado = "40314340";

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        await serviceA.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(CodigoTussCompartilhado, "Desc A", null, null, false, false, true));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var result = await serviceB.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(CodigoTussCompartilhado, "Desc B", null, null, false, false, true));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CriarProcedimento_PorteAnestesicoDigito_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30715013", "Herniorrafia inguinal", null, "9", false, false, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarProcedimento_PorteAnestesicoLetraO_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30715013", "Herniorrafia inguinal", null, "O", false, false, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarProcedimento_PorteAnestesicoA_PermitidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30715014", "Herniorrafia inguinal", null, "A", false, false, true));

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Value!.PorteAnestesico);
    }

    [Fact]
    public async Task ObterProcedimentoPorId_DeOutroTenant_RetornaNotFoundErrorAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var created = await serviceA.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("50000001", "Proc A", null, null, false, false, true));
        Assert.True(created.IsSuccess);
        var id = created.Value!.Id;

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var result = await serviceB.ObterProcedimentoPorIdAsync(id);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task ListarProcedimentos_BuscaPorCodigoTuss_RetornaCorrespondentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30715013", "Herniorrafia inguinal", null, null, false, false, true));
        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("40314340", "Eletroencefalograma", null, null, true, false, true));
        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("30712345", "Colecistectomia", null, null, false, false, true));

        var result = await service.ListarProcedimentosAsync(
            new ListarProcedimentosQuery("3071", null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i => Assert.StartsWith("3071", i.CodigoTuss, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListarProcedimentos_BuscaPorDescricao_RetornaCorrespondentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("50000010", "Herniorrafia inguinal bilateral", null, null, false, false, true));
        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("50000011", "Eletroencefalograma basal", null, null, false, false, true));
        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("50000012", "Herniorrafia umbilical", null, null, false, false, true));

        var result = await service.ListarProcedimentosAsync(
            new ListarProcedimentosQuery("HERNI", null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i =>
            Assert.Contains("herniorrafia", i.Descricao, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListarProcedimentos_NaoRetornaProcedimentosDeOutroTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var procA = await serviceA.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("60000001", "Proc Tenant A", null, null, false, false, true));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var procB = await serviceB.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("60000002", "Proc Tenant B", null, null, false, false, true));

        var result = await serviceA.ListarProcedimentosAsync(
            new ListarProcedimentosQuery(null, null, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == procA.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == procB.Value!.Id);
    }

    [Fact]
    public async Task AtualizarProcedimento_AlteraFlags_PersisteCorretamenteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var created = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand("70000001", "Proc flags", null, null, false, false, true));
        Assert.True(created.IsSuccess);

        var result = await service.AtualizarProcedimentoAsync(
            created.Value!.Id,
            new SalvarProcedimentoCommand("70000001", "Proc flags atualizado", "6B", "D", true, true, true));

        Assert.True(result.IsSuccess);
        Assert.Equal("Proc flags atualizado", result.Value!.Descricao);
        Assert.Equal("6B", result.Value.Porte);
        Assert.Equal("D", result.Value.PorteAnestesico);
        Assert.True(result.Value.EhSadt);
        Assert.True(result.Value.TemPorteProprioVideo);
    }

    [Fact]
    public async Task ExcluirProcedimento_NaoEncontrado_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ExcluirProcedimentoAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task ExcluirProcedimento_ComGuiaAssociada_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var codTuss = tenantId.ToString("N")[..8];
        var procedimento = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(codTuss, "Proc Bloqueado", null, null, false, false, true));
        Assert.True(procedimento.IsSuccess);
        var procedimentoId = procedimento.Value!.Id;

        var prestador = App.Catalog.Prestador.Create(tenantId, "Dr. Seed Proc", null);
        var operadora = App.Catalog.Operadora.Create(tenantId, "UNIMED Seed Proc", null, null, TipoRuleSet.Unimed);
        var beneficiario = App.Catalog.Beneficiario.Create(tenantId, tenantId.ToString("N")[..8].ToUpperInvariant(), "Paciente Seed Proc");
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        await ctx.SaveChangesAsync();

        var guia = Guia.Create(tenantId, prestador.Id, operadora.Id, beneficiario.Id, "SEN001", new DateOnly(2025, 1, 1), false, "");
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao, 1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.Add(item);
        await ctx.SaveChangesAsync();

        var result = await service.ExcluirProcedimentoAsync(procedimentoId);

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }
}

file sealed class FakeProcedimentoUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
