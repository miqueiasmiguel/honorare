using App;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Operadora;

[Collection(nameof(PostgresCollection))]
public sealed class OperadoraCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakeTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    [Fact]
    public async Task CriarOperadora_NomeValido_RetornaOperadoraCriadaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarAsync(new CriarOperadoraCommand("UNIMED JP", null, null, TipoRuleSet.Unimed));

        Assert.True(result.IsSuccess);
        Assert.Equal("UNIMED JP", result.Value!.Nome);
        Assert.Equal(TipoRuleSet.Unimed, result.Value.TipoRuleSet);
        Assert.True(result.Value.Ativa);
    }

    [Fact]
    public async Task CriarOperadora_NomeFaltando_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarAsync(new CriarOperadoraCommand("   ", null, null, TipoRuleSet.Unimed));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarOperadora_CnpjComFormatoInvalido_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarAsync(new CriarOperadoraCommand("UNIMED", null, "123", TipoRuleSet.Unimed));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CriarOperadora_CnpjDuplicadoNoMesmoTenant_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);
        const string CnpjDuplicado = "12345678000195";

        await service.CriarAsync(new CriarOperadoraCommand("UNIMED A", null, CnpjDuplicado, TipoRuleSet.Unimed));
        var result = await service.CriarAsync(new CriarOperadoraCommand("UNIMED B", null, CnpjDuplicado, TipoRuleSet.Unimed));

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task CriarOperadora_CnpjDuplicadoEmTenantDistinto_PermitidoAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string CnpjCompartilhado = "22222222000100";

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        await serviceA.CriarAsync(new CriarOperadoraCommand("UNIMED A", null, CnpjCompartilhado, TipoRuleSet.Unimed));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var result = await serviceB.CriarAsync(new CriarOperadoraCommand("UNIMED B", null, CnpjCompartilhado, TipoRuleSet.Unimed));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CriarOperadora_RegistroAnsComFormatoInvalido_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarAsync(new CriarOperadoraCommand("UNIMED", "12", null, TipoRuleSet.Unimed));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task ObterOperadoraPorId_NaoEncontrada_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ObterPorIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task ObterOperadoraPorId_OperadoraDeOutroTenant_RetornaNotFoundErrorAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var created = await serviceA.CriarAsync(new CriarOperadoraCommand("UNIMED A", null, null, TipoRuleSet.Unimed));
        Assert.True(created.IsSuccess);
        var id = created.Value!.Id;

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var result = await serviceB.ObterPorIdAsync(id);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task AtualizarOperadora_CnpjDuplicadoNoProprioCadastro_PermitidoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);
        const string CnpjProprio = "33333333000155";

        var created = await service.CriarAsync(new CriarOperadoraCommand("UNIMED", null, CnpjProprio, TipoRuleSet.Unimed));
        Assert.True(created.IsSuccess);

        var result = await service.AtualizarAsync(
            created.Value!.Id,
            new AtualizarOperadoraCommand("UNIMED Atualizada", null, CnpjProprio, TipoRuleSet.Unimed, true));

        Assert.True(result.IsSuccess);
        Assert.Equal("UNIMED Atualizada", result.Value!.Nome);
    }

    [Fact]
    public async Task ListarOperadoras_FiltroNome_RetornaApenasCorrespondentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarAsync(new CriarOperadoraCommand("UNIMED João Pessoa", null, null, TipoRuleSet.Unimed));
        await service.CriarAsync(new CriarOperadoraCommand("UNIMED Recife", null, null, TipoRuleSet.Unimed));
        await service.CriarAsync(new CriarOperadoraCommand("Amil", null, null, TipoRuleSet.Nulo));

        var result = await service.ListarAsync(new ListarOperadorasQuery("UNIMED", null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i => Assert.Contains("UNIMED", i.Nome, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListarOperadoras_FiltroAtiva_RetornaApenasAtivasAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var ativa = await service.CriarAsync(new CriarOperadoraCommand("Ativa", null, null, TipoRuleSet.Unimed));
        var inativa = await service.CriarAsync(new CriarOperadoraCommand("Inativa", null, null, TipoRuleSet.Unimed));
        await service.AtualizarAsync(
            inativa.Value!.Id,
            new AtualizarOperadoraCommand("Inativa", null, null, TipoRuleSet.Unimed, false));

        var result = await service.ListarAsync(new ListarOperadorasQuery(null, true, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == ativa.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == inativa.Value!.Id);
    }

    [Fact]
    public async Task ListarOperadoras_NaoRetornaOperadorasDeOutroTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var opA = await serviceA.CriarAsync(new CriarOperadoraCommand("UNIMED A", null, null, TipoRuleSet.Unimed));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var opB = await serviceB.CriarAsync(new CriarOperadoraCommand("UNIMED B", null, null, TipoRuleSet.Unimed));

        var result = await serviceA.ListarAsync(new ListarOperadorasQuery(null, null, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == opA.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == opB.Value!.Id);
    }

    [Fact]
    public async Task ExcluirOperadora_Existente_RemoveDoBancoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var created = await service.CriarAsync(new CriarOperadoraCommand("Para Excluir", null, null, TipoRuleSet.Unimed));
        Assert.True(created.IsSuccess);

        var result = await service.ExcluirAsync(created.Value!.Id);

        Assert.True(result.IsSuccess);
        var obter = await service.ObterPorIdAsync(created.Value.Id);
        Assert.True(obter.IsFailure);
    }

    [Fact]
    public async Task ExcluirOperadora_NaoEncontrada_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ExcluirAsync(Guid.NewGuid());

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
