using App;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Beneficiario;

[Collection(nameof(PostgresCollection))]
public sealed class BeneficiarioLookupTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeLookupTenantUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    [Fact]
    public async Task LookupOrCreate_CarteiraExistente_RetornaBeneficiarioExistenteSemCriarAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var primeira = await service.LookupOrCreateAsync("001ABC", "Paciente Um");
        Assert.True(primeira.IsSuccess);
        Assert.True(primeira.Value!.Criado);

        var segunda = await service.LookupOrCreateAsync("001ABC", "Paciente Um");
        Assert.True(segunda.IsSuccess);
        Assert.False(segunda.Value!.Criado);
        Assert.Equal(primeira.Value.Beneficiario.Id, segunda.Value.Beneficiario.Id);

        var count = await ctx.Beneficiarios.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task LookupOrCreate_CarteiraNova_CriaBeneficiarioRetornaCriadoAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.LookupOrCreateAsync("CART-NOVA-001", "Novo Paciente");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Criado);
        Assert.NotEqual(Guid.Empty, result.Value.Beneficiario.Id);

        var existe = await ctx.Beneficiarios.AnyAsync(b => b.Id == result.Value.Beneficiario.Id);
        Assert.True(existe);
    }

    [Fact]
    public async Task LookupOrCreate_CarteiraComEspacos_NormalizaEEncontraExistenteAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.LookupOrCreateAsync("001ABC", "Paciente Espaços");

        var result = await service.LookupOrCreateAsync("  001ABC  ", "Outro Nome");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Criado);
        Assert.Equal("001ABC", result.Value.Beneficiario.Carteira);
    }

    [Fact]
    public async Task LookupOrCreate_CarteiraMinuscula_NormalizaEEncontraExistenteAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.LookupOrCreateAsync("001ABC", "Paciente Maiusculo");

        var result = await service.LookupOrCreateAsync("001abc", "Outro Nome");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Criado);
        Assert.Equal("001ABC", result.Value.Beneficiario.Carteira);
    }

    [Fact]
    public async Task LookupOrCreate_CarteiraVazia_RetornaValidationErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.LookupOrCreateAsync("   ", "Paciente");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task LookupOrCreate_NomeVazioComCarteiraNova_RetornaValidationErrorAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.LookupOrCreateAsync("CART-SEM-NOME", "   ");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task LookupOrCreate_NomeVazio_CarteiraExistente_IgnoraNomeERetornaExistenteAsync()
    {
        var (ctx, user) = BuildTenant(Guid.NewGuid());
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.LookupOrCreateAsync("CART-IGNORA-NOME", "Nome Original");

        // nome vazio mas carteira já existe → deve retornar existente (nome ignorado)
        var result = await service.LookupOrCreateAsync("CART-IGNORA-NOME", "   ");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Criado);
        Assert.Equal("Nome Original", result.Value.Beneficiario.Nome);
    }

    [Fact]
    public async Task LookupOrCreate_IsolacaoDeTenant_NaoCruzaTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        await serviceA.LookupOrCreateAsync("CART-ISO-AB", "Paciente Tenant A");

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);

        var result = await serviceB.LookupOrCreateAsync("CART-ISO-AB", "Paciente Tenant B");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Criado);
    }
}

file sealed class FakeLookupTenantUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
