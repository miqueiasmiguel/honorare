using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests.QueryFilter;

// Entity mínima usada apenas nos testes para exercitar o filtro global.
// Não existe no AppDbContext de produção — registrada apenas no TestableDbContext abaixo.
internal sealed class TenantItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Test double: TenantAdmin com TenantId fixo.
internal sealed class TenantAdminCurrentUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}

// Test double: SaasAdmin — bypass total do filtro.
internal sealed class SaasAdminCurrentUser : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => null;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => true;
    public bool IsAuthenticated => true;
}

// Subclasse de AppDbContext que adiciona TenantItem ao modelo.
// TenantItem é registrado ANTES de base.OnModelCreating para que o loop de filtro o inclua.
internal sealed class TestableDbContext : AppDbContext
{
    public DbSet<TenantItem> TenantItems => Set<TenantItem>();

    public TestableDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantItem>(b =>
        {
            b.ToTable("TenantItems");
            b.HasKey(t => t.Id);
        });
        base.OnModelCreating(modelBuilder);
    }
}

[Collection(nameof(IdentityPostgresCollection))]
public sealed class TenantQueryFilterTests(PostgresContainerFixture db)
{
    private async Task<TestableDbContext> CreateTestableContextAsync(ICurrentUser? currentUser = null)
    {
        currentUser ??= new SaasAdminCurrentUser();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new TestableDbContext(options, currentUser);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    [Fact]
    public async Task TenantAdmin_CannotSee_OtherTenantData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var ctx = await CreateTestableContextAsync())
        {
            ctx.TenantItems.AddRange(
                new TenantItem { Id = Guid.NewGuid(), TenantId = tenantA, Name = "item-A" },
                new TenantItem { Id = Guid.NewGuid(), TenantId = tenantB, Name = "item-B" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await CreateTestableContextAsync(new TenantAdminCurrentUser(tenantA)))
        {
            var items = await ctx.TenantItems.ToListAsync();
            Assert.Single(items);
            Assert.Equal("item-A", items[0].Name);
        }
    }

    [Fact]
    public async Task TenantAdmin_CanSee_OwnTenantData()
    {
        var tenantId = Guid.NewGuid();

        await using (var ctx = await CreateTestableContextAsync())
        {
            ctx.TenantItems.Add(new TenantItem { Id = Guid.NewGuid(), TenantId = tenantId, Name = "meu-item" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await CreateTestableContextAsync(new TenantAdminCurrentUser(tenantId)))
        {
            var items = await ctx.TenantItems
                .Where(t => t.TenantId == tenantId)
                .ToListAsync();
            Assert.Single(items);
            Assert.Equal("meu-item", items[0].Name);
        }
    }

    [Fact]
    public async Task SaasAdmin_CanSee_AllTenantData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var ctx = await CreateTestableContextAsync();
        ctx.TenantItems.AddRange(
            new TenantItem { Id = Guid.NewGuid(), TenantId = tenantA, Name = "saas-item-A" },
            new TenantItem { Id = Guid.NewGuid(), TenantId = tenantB, Name = "saas-item-B" });
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();

        var allItems = await ctx.TenantItems
            .Where(t => t.TenantId == tenantA || t.TenantId == tenantB)
            .ToListAsync();
        Assert.Equal(2, allItems.Count);
    }
}
