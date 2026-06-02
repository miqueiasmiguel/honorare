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

        await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dr. Carlos Silva", null, null));
        await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dr. Carlos Souza", null, null));
        await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dra. Ana Lima", null, null));

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

        var ativo = await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dr. Ativo", null, null));
        var inativo = await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dr. Inativo", null, null));
        await service.AtualizarPrestadorAsync(
            inativo.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Inativo", null, false));

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
            new CriarPrestadorCommand("Dr. João Alves", "CRM/PB 12345", null));

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

        var result = await service.CriarPrestadorAsync(new CriarPrestadorCommand("   ", null, null));

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
        var result = await service.CriarPrestadorAsync(new CriarPrestadorCommand(nomeLongo, null, null));

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
            new CriarPrestadorCommand("Dr. Original", "CRM/PB 111", null));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Atualizado", "CRM/PB 999", false));

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
            new AtualizarPrestadorCommand("Dr. Inexistente", null, true));

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
            new CriarPrestadorCommand("Dr. Para Excluir", null, null));
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
            new CriarPrestadorCommand("Dr. Tenant A", null, null));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var prestadorB = await serviceB.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Tenant B", null, null));

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

        var criado = await service.CriarPrestadorAsync(new CriarPrestadorCommand("Dr. Bloqueado", null, null));
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

    // ── Novos testes TASK-UP-01 ───────────────────────────────────────────────

    [Fact]
    public async Task CriarPrestador_SemEmail_NaoCriaUsuarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Sem Email", null, null));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.TemUsuario);
        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == result.Value.Id);
        Assert.Null(userDb);
    }

    [Fact]
    public async Task CriarPrestador_ComEmail_CriaUsuarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"medico-{Guid.NewGuid():N}@test.com";
        var result = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Com Email", null, email));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.TemUsuario);
        Assert.Equal(email, result.Value.EmailAcesso);

        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == result.Value.Id);
        Assert.NotNull(userDb);
        Assert.Equal(result.Value.Id, userDb.MedicoId);
    }

    [Fact]
    public async Task CriarPrestador_EmailDuplicado_Retorna409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"dup-{Guid.NewGuid():N}@test.com";
        var primeiro = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Primeiro", null, email));
        Assert.True(primeiro.IsSuccess);

        var segundo = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Segundo", null, email));

        Assert.True(segundo.IsFailure);
        Assert.IsType<ConflictError>(segundo.Error);
    }

    [Fact]
    public async Task CriarPrestador_EmailInvalido_Retorna400Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var result = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Email Invalido", null, "nao-e-email"));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task AtualizarPrestador_NaoAlteraEmailAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"imutavel-{Guid.NewGuid():N}@test.com";
        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Original", null, email));
        Assert.True(criado.IsSuccess);

        var atualizado = await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Atualizado", null, true));

        Assert.True(atualizado.IsSuccess);
        Assert.Equal(email, atualizado.Value!.EmailAcesso);
    }

    [Fact]
    public async Task AtualizarPrestador_DesativarPrestador_DesativaUsuarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"desativar-{Guid.NewGuid():N}@test.com";
        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Ativo", null, email));
        Assert.True(criado.IsSuccess);

        await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Ativo", null, false));

        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == criado.Value.Id);
        Assert.NotNull(userDb);
        Assert.False(userDb.IsActive);
    }

    [Fact]
    public async Task AtualizarPrestador_ReativarPrestador_ReativaUsuarioAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"reativar-{Guid.NewGuid():N}@test.com";
        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Inativo", null, email));
        Assert.True(criado.IsSuccess);

        await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Inativo", null, false));

        await service.AtualizarPrestadorAsync(
            criado.Value!.Id,
            new AtualizarPrestadorCommand("Dr. Inativo", null, true));

        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == criado.Value.Id);
        Assert.NotNull(userDb);
        Assert.True(userDb.IsActive);
    }

    [Fact]
    public async Task ExcluirPrestador_SemUsuario_RemoveAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Sem Usuario", null, null));
        Assert.True(criado.IsSuccess);

        var result = await service.ExcluirPrestadorAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var obter = await service.ObterPrestadorPorIdAsync(criado.Value.Id);
        Assert.True(obter.IsFailure);
    }

    [Fact]
    public async Task ExcluirPrestador_ComUsuario_SemGoogleId_RemoveAmbosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"excluir-{Guid.NewGuid():N}@test.com";
        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Com User", null, email));
        Assert.True(criado.IsSuccess);
        var prestadorId = criado.Value!.Id;

        var result = await service.ExcluirPrestadorAsync(prestadorId);

        Assert.True(result.IsSuccess);
        var obterPrestador = await service.ObterPrestadorPorIdAsync(prestadorId);
        Assert.True(obterPrestador.IsFailure);
        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == prestadorId);
        Assert.Null(userDb);
    }

    [Fact]
    public async Task ExcluirPrestador_ComUsuario_ComGoogleId_Retorna409Async()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"googleid-{Guid.NewGuid():N}@test.com";
        var criado = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Google", null, email));
        Assert.True(criado.IsSuccess);

        var userDb = await ctx.Users.FirstOrDefaultAsync(u => u.MedicoId == criado.Value!.Id);
        Assert.NotNull(userDb);
        userDb.AssociateGoogleId("google-id-123");
        await ctx.SaveChangesAsync();

        var result = await service.ExcluirPrestadorAsync(criado.Value!.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task ListarPrestadores_TemUsuario_CorretoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var email = $"listar-{Guid.NewGuid():N}@test.com";
        var comEmail = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Com Acesso", null, email));
        var semEmail = await service.CriarPrestadorAsync(
            new CriarPrestadorCommand("Dr. Sem Acesso", null, null));

        var result = await service.ListarPrestadoresAsync(new ListarPrestadoresQuery(null, null, 1, 100));

        var itemComEmail = result.Itens.First(i => i.Id == comEmail.Value!.Id);
        var itemSemEmail = result.Itens.First(i => i.Id == semEmail.Value!.Id);

        Assert.True(itemComEmail.TemUsuario);
        Assert.False(itemSemEmail.TemUsuario);
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
