using App;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Deflator;

[Collection(nameof(PostgresCollection))]
public sealed class DeflatorServiceTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakeDeflatorUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    private static async Task<PrestadorDto> CriarPrestadorAsync(CatalogService service, string nome = "Dr. Teste")
    {
        var result = await service.CriarPrestadorAsync(new SalvarPrestadorCommand(nome, null, true));
        return result.Value!;
    }

    private static async Task<OperadoraDto> CriarOperadoraAsync(CatalogService service, string nome = "UNIMED")
    {
        var result = await service.CriarAsync(new CriarOperadoraCommand(nome, null, null, TipoRuleSet.Unimed));
        return result.Value!;
    }

    [Fact]
    public async Task Listar_RetornaDeflatoresDoPrestadorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Anestesista, 70m));

        var result = await service.ListarDeflatoresAsync(prestador.Id);

        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(prestador.Id, d.PrestadorId));
    }

    [Fact]
    public async Task Listar_NaoRetornaDeflatoresDeOutroPrestadorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestadorA = await CriarPrestadorAsync(service, "Dr. A");
        var prestadorB = await CriarPrestadorAsync(service, "Dr. B");
        var operadora = await CriarOperadoraAsync(service);

        await service.CriarDeflatorAsync(prestadorA.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await service.CriarDeflatorAsync(prestadorB.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 80m));

        var result = await service.ListarDeflatoresAsync(prestadorA.Id);

        Assert.Single(result);
        Assert.Equal(prestadorA.Id, result[0].PrestadorId);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_RetornaDeflatorDtoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        var result = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));

        Assert.True(result.IsSuccess);
        Assert.Equal(prestador.Id, result.Value!.PrestadorId);
        Assert.Equal(operadora.Id, result.Value.OperadoraId);
        Assert.Equal(PosicaoExecutor.Cirurgiao, result.Value.Posicao);
        Assert.Equal(100m, result.Value.Percentual);
    }

    [Fact]
    public async Task Criar_PercentualZero_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        var result = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 0m));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_PercentualAcima200_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        var result = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 200.01m));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task Criar_DuplicadoMesmaPosicaoOperadora_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));

        var result = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 90m));

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task Criar_PrestadorDeOutroTenant_RetornaNotFoundErrorAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var prestadorA = await CriarPrestadorAsync(serviceA, "Dr. Tenant A");

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var operadoraB = await CriarOperadoraAsync(serviceB);

        var result = await serviceB.CriarDeflatorAsync(prestadorA.Id,
            new SalvarDeflatorCommand(operadoraB.Id, PosicaoExecutor.Cirurgiao, 100m));

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_DeflatorExistente_AtualizaPercentualAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        var criado = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarDeflatorAsync(prestador.Id, criado.Value!.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 85m));

        Assert.True(result.IsSuccess);
        Assert.Equal(85m, result.Value!.Percentual);
    }

    [Fact]
    public async Task Excluir_DeflatorExistente_RemoveAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);
        var operadora = await CriarOperadoraAsync(service);

        var criado = await service.CriarDeflatorAsync(prestador.Id,
            new SalvarDeflatorCommand(operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        Assert.True(criado.IsSuccess);

        var result = await service.ExcluirDeflatorAsync(prestador.Id, criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var lista = await service.ListarDeflatoresAsync(prestador.Id);
        Assert.Empty(lista);
    }

    [Fact]
    public async Task Excluir_DeflatorInexistente_RetornaNotFoundErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var prestador = await CriarPrestadorAsync(service);

        var result = await service.ExcluirDeflatorAsync(prestador.Id, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }
}

file sealed class FakeDeflatorUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
