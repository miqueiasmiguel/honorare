using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Prestador;

[Collection(nameof(PostgresCollection))]
public sealed class PrestadorCrudTests(PostgresContainerFixture db)
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
    public async Task Listar_RetornaVazioQuandoSemPrestadoresAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ListarPrestadoresAsync(new ListarPrestadoresQuery(null, null, 1, 20));

        Assert.Empty(result.Itens);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Listar_FiltraPorNome_RetornaSomenteCorrespondentesAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dr. Carlos Silva", null, true));
        await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dr. Carlos Souza", null, true));
        await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dra. Ana Lima", null, true));

        var result = await service.ListarPrestadoresAsync(new ListarPrestadoresQuery("Carlos", null, 1, 20));

        Assert.Equal(2, result.Itens.Count);
        Assert.All(result.Itens, i => Assert.Contains("Carlos", i.Nome, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Listar_FiltraPorAtivo_RetornaSomenteAtivosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var ativo = await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dr. Ativo", null, true));
        var inativo = await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dr. Inativo", null, true));
        await service.AtualizarPrestadorAsync(
            inativo.Value!.Id,
            new SalvarPrestadorCommand("Dr. Inativo", null, false));

        var result = await service.ListarPrestadoresAsync(new ListarPrestadoresQuery(null, true, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == ativo.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == inativo.Value!.Id);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_RetornaPrestadorDtoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarPrestadorAsync(
            new SalvarPrestadorCommand("Dr. João Alves", "CRM/PB 12345", true));

        Assert.True(result.IsSuccess);
        Assert.Equal("Dr. João Alves", result.Value!.Nome);
        Assert.Equal("CRM/PB 12345", result.Value.RegistroProfissional);
        Assert.True(result.Value.Ativo);
    }

    [Fact]
    public async Task Criar_SemNome_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarPrestadorAsync(new SalvarPrestadorCommand("   ", null, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_NomeMuitoLongo_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var nomeLongo = new string('A', 151);
        var result = await service.CriarPrestadorAsync(new SalvarPrestadorCommand(nomeLongo, null, true));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_PrestadorExistente_AtualizaCamposAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarPrestadorAsync(
            new SalvarPrestadorCommand("Dr. Original", "CRM/PB 111", true));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new SalvarPrestadorCommand("Dr. Atualizado", "CRM/PB 999", false));

        Assert.True(result.IsSuccess);
        Assert.Equal("Dr. Atualizado", result.Value!.Nome);
        Assert.Equal("CRM/PB 999", result.Value.RegistroProfissional);
        Assert.False(result.Value.Ativo);
    }

    [Fact]
    public async Task Atualizar_PrestadorInexistente_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.AtualizarPrestadorAsync(
            Guid.NewGuid(),
            new SalvarPrestadorCommand("Dr. Inexistente", null, true));

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Excluir_PrestadorExistente_RemoveAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarPrestadorAsync(
            new SalvarPrestadorCommand("Dr. Para Excluir", null, true));
        Assert.True(criado.IsSuccess);

        var result = await service.ExcluirPrestadorAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var obter = await service.ObterPrestadorPorIdAsync(criado.Value.Id);
        Assert.True(obter.IsFailure);
    }

    [Fact]
    public async Task Excluir_PrestadorInexistente_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.ExcluirPrestadorAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Listar_NaoRetornaPrestadoresDeOutroTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var prestadorA = await serviceA.CriarPrestadorAsync(
            new SalvarPrestadorCommand("Dr. Tenant A", null, true));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var prestadorB = await serviceB.CriarPrestadorAsync(
            new SalvarPrestadorCommand("Dr. Tenant B", null, true));

        var result = await serviceA.ListarPrestadoresAsync(new ListarPrestadoresQuery(null, null, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == prestadorA.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == prestadorB.Value!.Id);
    }

    [Fact]
    public async Task ExcluirPrestador_ComGuiaAssociada_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarPrestadorAsync(new SalvarPrestadorCommand("Dr. Bloqueado", null, true));
        Assert.True(criado.IsSuccess);
        var prestadorId = criado.Value!.Id;

        var operadora = App.Catalog.Operadora.Create(tenantId, "UNIMED Seed Prest", null, null, TipoRuleSet.Unimed);
        var beneficiario = App.Catalog.Beneficiario.Create(tenantId, tenantId.ToString("N")[..8].ToUpperInvariant(), "Paciente Seed Prest");
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        await ctx.SaveChangesAsync();

        var guia = Guia.Create(tenantId, prestadorId, operadora.Id, beneficiario.Id, "SEN001", new DateOnly(2025, 1, 1), false, "");
        ctx.Add(guia);
        await ctx.SaveChangesAsync();

        var result = await service.ExcluirPrestadorAsync(prestadorId);

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
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
