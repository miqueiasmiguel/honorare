using App;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Beneficiario;

[Collection(nameof(PostgresCollection))]
public sealed class BeneficiarioCrudTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    [Fact]
    public async Task Criar_CarteiraENomeValidos_RetornaBeneficiarioDtoAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("0001234567", "João Silva"));

        Assert.True(result.IsSuccess);
        Assert.Equal("0001234567", result.Value!.Carteira);
        Assert.Equal("João Silva", result.Value.Nome);
    }

    [Fact]
    public async Task Criar_CarteiraNormalizada_PersistidaUppercaseAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand(" 001abc ", "Maria"));

        Assert.True(result.IsSuccess);
        Assert.Equal("001ABC", result.Value!.Carteira);
    }

    [Fact]
    public async Task Criar_NomeTrimado_PersistidoSemEspacosAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("XYZ999", "  Ana Lima  "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana Lima", result.Value!.Nome);
    }

    [Fact]
    public async Task Criar_CarteiraVazia_RetornaValidationErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("   ", "João"));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_NomeVazio_RetornaValidationErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("ABC123", "   "));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_CarteiraDuplicadaMesmoTenant_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("CART001", "Paciente Um"));
        var result = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("CART001", "Paciente Dois"));

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task Criar_CarteiraDuplicadaTenantDiferente_PermitidoAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);

        await serviceA.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("CART-DUPLA", "Paciente A"));
        var result = await serviceB.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("CART-DUPLA", "Paciente B"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Obter_IdExistente_RetornaBeneficiarioDtoAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("OBT001", "Paciente Obter"));
        Assert.True(criado.IsSuccess);

        var result = await service.ObterBeneficiarioPorIdAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(criado.Value.Id, result.Value!.Id);
        Assert.Equal("OBT001", result.Value.Carteira);
    }

    [Fact]
    public async Task Obter_IdInexistente_RetornaNotFoundErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ObterBeneficiarioPorIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Obter_IdDeOutroTenant_RetornaNotFoundErrorAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var criado = await serviceA.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("CROSS-TENANT", "Paciente A"));
        Assert.True(criado.IsSuccess);

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);

        var result = await serviceB.ObterBeneficiarioPorIdAsync(criado.Value!.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_NomeValido_RetornaDtoAtualizadoAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("UPD001", "Nome Antigo"));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarBeneficiarioAsync(
            criado.Value!.Id, new AtualizarBeneficiarioCommand("Nome Novo"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", result.Value!.Nome);
        Assert.Equal("UPD001", result.Value.Carteira);
    }

    [Fact]
    public async Task Atualizar_NomeVazio_RetornaValidationErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("UPD002", "Nome Original"));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarBeneficiarioAsync(
            criado.Value!.Id, new AtualizarBeneficiarioCommand("   "));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_IdInexistente_RetornaNotFoundErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.AtualizarBeneficiarioAsync(
            Guid.NewGuid(), new AtualizarBeneficiarioCommand("Novo Nome"));

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Listar_SemFiltros_RetornaTodosDoTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("LST001", "Paciente Um"));
        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("LST002", "Paciente Dois"));

        var result = await service.ListarBeneficiariosAsync(
            new ListarBeneficiariosQuery(null, null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Listar_FiltroCarteira_RetornaApenasMatchesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("FILT-ABC-001", "Paciente Um"));
        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("FILT-ABC-002", "Paciente Dois"));
        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("XYZ-999", "Outro"));

        var result = await service.ListarBeneficiariosAsync(
            new ListarBeneficiariosQuery("filt-abc", null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i => Assert.Contains("FILT-ABC", i.Carteira, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Listar_FiltroNome_RetornaApenasMatchesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("NM001", "Carlos Santos"));
        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("NM002", "Carlos Oliveira"));
        await service.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("NM003", "Ana Lima"));

        var result = await service.ListarBeneficiariosAsync(
            new ListarBeneficiariosQuery(null, "Carlos", 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i => Assert.Contains("Carlos", i.Nome, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Listar_IsolacaoDeTenant_NaoRetornaDadosDeTenantDiferenteAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        await serviceA.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("ISO-A", "Paciente Tenant A"));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        await serviceB.CriarBeneficiarioAsync(new CriarBeneficiarioCommand("ISO-B", "Paciente Tenant B"));

        var result = await serviceA.ListarBeneficiariosAsync(
            new ListarBeneficiariosQuery(null, null, 1, 100));

        Assert.Contains(result.Itens, i => i.Carteira == "ISO-A");
        Assert.DoesNotContain(result.Itens, i => i.Carteira == "ISO-B");
    }

    [Fact]
    public async Task Excluir_IdExistente_RemoveBeneficiarioAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarBeneficiarioAsync(
            new CriarBeneficiarioCommand("DEL001", "Paciente Excluir"));
        Assert.True(criado.IsSuccess);

        var result = await service.ExcluirBeneficiarioAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var obter = await service.ObterBeneficiarioPorIdAsync(criado.Value.Id);
        Assert.True(obter.IsFailure);
    }

    [Fact]
    public async Task Excluir_IdInexistente_RetornaNotFoundErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ExcluirBeneficiarioAsync(Guid.NewGuid());

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
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
