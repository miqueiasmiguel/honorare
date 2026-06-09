using App;
using App.Data;
using App.Identity;
using App.Storage;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public sealed class TenantSettingsServiceTests(PostgresContainerFixture db)
{
    private AppDbContext BuildContext(ICurrentUser user) =>
        new(db.BuildOptions<AppDbContext>(), user);

    private static async Task<Tenant> SeedTenantAsync(AppDbContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync();

        var tenant = Tenant.Create("Clínica Teste");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        return tenant;
    }

    // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
    private static byte[] PngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    // ── Testes existentes ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_DeveRetornarNomeEHasLogoFalseAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.GetSettingsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(tenant.Id, result.Value!.Id);
        Assert.Equal("Clínica Teste", result.Value.Name);
        Assert.False(result.Value.HasLogo);
    }

    [Fact]
    public async Task RenameAsync_DeveAtualizarNomeDoTenantAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.RenameAsync("Novo Nome");

        Assert.True(result.IsSuccess);
        Assert.Equal("Novo Nome", result.Value!.Name);

        await using var verifyCtx = BuildContext(currentUser);
        var updated = await verifyCtx.Tenants.FindAsync([tenant.Id]);
        Assert.Equal("Novo Nome", updated!.Name);
    }

    [Fact]
    public async Task RenameAsync_DeveFalharQuandoNomeEhVazioAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.RenameAsync("   ");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task RenameAsync_DeveFalharQuandoTenantNaoExisteAsync()
    {
        var currentUser = new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid());
        await using var ctx = BuildContext(currentUser);
        await ctx.Database.EnsureCreatedAsync();
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.RenameAsync("Qualquer Nome");

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    // ── Novos testes: upload/download/remoção da logo ─────────────────────

    [Fact]
    public async Task UploadLogoAsync_DeveAceitarPngValidoGravarNoStorageESetarLogoKeyAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var storage = new FakeFileStorage();
        var service = new TenantSettingsService(ctx, currentUser, storage);

        var result = await service.UploadLogoAsync(PngBytes());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasLogo);
        var expectedKey = $"tenants/{tenant.Id}/logo.png";
        Assert.True(storage.Contains(expectedKey));

        await using var verifyCtx = BuildContext(currentUser);
        var updated = await verifyCtx.Tenants.FindAsync([tenant.Id]);
        Assert.Equal(expectedKey, updated!.LogoKey);
    }

    [Fact]
    public async Task UploadLogoAsync_DeveRejeitarBytesQueNaoSaoPngOuJpegAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.UploadLogoAsync([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
        Assert.Contains("Formato inválido", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadLogoAsync_DeveRejeitarArquivoAcimaDe2MbAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var bigFile = new byte[2 * 1024 * 1024 + 1];

        var result = await service.UploadLogoAsync(bigFile);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
        Assert.Contains("2 MB", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLogoAsync_DeveRetornarBytesGravadosAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var storage = new FakeFileStorage();
        var service = new TenantSettingsService(ctx, currentUser, storage);

        await service.UploadLogoAsync(PngBytes());

        await using var ctx2 = BuildContext(currentUser);
        var service2 = new TenantSettingsService(ctx2, currentUser, storage);
        var result = await service2.GetLogoAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value!.ContentType);
        Assert.Equal(PngBytes(), result.Value.Content);
    }

    [Fact]
    public async Task GetLogoAsync_DeveFalharComNotFoundQuandoTenantNaoTemLogoAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, new FakeFileStorage());

        var result = await service.GetLogoAsync();

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task DeleteLogoAsync_DeveLimparLogoKeyERemoverDoStorageAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var uploadCtx = BuildContext(currentUser);
        var storage = new FakeFileStorage();
        var uploadService = new TenantSettingsService(uploadCtx, currentUser, storage);
        await uploadService.UploadLogoAsync(PngBytes());

        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser, storage);
        var result = await service.DeleteLogoAsync();

        Assert.True(result.IsSuccess);

        await using var verifyCtx = BuildContext(currentUser);
        var updated = await verifyCtx.Tenants.FindAsync([tenant.Id]);
        Assert.Null(updated!.LogoKey);
        Assert.False(storage.Contains($"tenants/{tenant.Id}/logo.png"));
    }
}

file sealed class FakeTenantUser(Guid userId, Guid tenantId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserId => userId;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
}

file sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, FileStorageObject> _store = [];

    public Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        _store[key] = new FileStorageObject(content, contentType);
        return Task.CompletedTask;
    }

    public Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default)
    {
        _store.TryGetValue(key, out var obj);
        return Task.FromResult(obj);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public bool Contains(string key) => _store.ContainsKey(key);
}
