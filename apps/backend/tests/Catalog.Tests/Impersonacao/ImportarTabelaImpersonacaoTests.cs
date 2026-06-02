using System.Text;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Impersonacao;

[Collection(nameof(PostgresCollection))]
public sealed class ImportarTabelaImpersonacaoTests(PostgresContainerFixture db)
{
    private static string CsvWith(string codigoTuss, decimal valor) =>
        $"CodigoTuss;Valor\n{codigoTuss};{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private (AppDbContext ctx, ICurrentUser user) BuildImpersonating(Guid tenantId)
    {
        var user = new FakeImpersonatingUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    // ── ImportarTabela_SobImpersonacao_CarimbaTenantAlvo ───────────────────────

    [Fact]
    public async Task ImportarTabela_SobImpersonacao_CarimbaTenantAlvoAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var codigoTuss = tenantA.ToString("N")[..8];

        // Seed: criar procedimento e operadora para tenantA via contexto admin global
        var insertCtx = db.CreateContext();
        await using var _ = insertCtx;

        var proc = global::App.Catalog.Procedimento.Create(tenantA, codigoTuss, $"Proc {codigoTuss}", null, null, false, false);
        insertCtx.Procedimentos.Add(proc);

        var op = global::App.Catalog.Operadora.Create(tenantA, "Operadora Imp", null, null, TipoRuleSet.Nulo);
        insertCtx.Operadoras.Add(op);
        await insertCtx.SaveChangesAsync();

        // Importar sob impersonação de tenantA
        var (impCtx, impUser) = BuildImpersonating(tenantA);
        await using var __ = impCtx;
        var service = new CatalogService(impCtx, impUser);

        var csv = CsvWith(codigoTuss, 100m);
        var result = await service.ImportarTabelaCsvAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), op.Id);

        Assert.Equal(1, result.Inseridos);
        Assert.Empty(result.Erros);

        // Linhas persistidas devem ter TenantId == tenantA
        await using var readCtx = db.CreateContext();
        var linhas = await readCtx.TabelasProcedimento
            .Where(t => t.OperadoraId == op.Id)
            .ToListAsync();

        Assert.All(linhas, l => Assert.Equal(tenantA, l.TenantId));

        // Contexto de tenantB não deve ver as linhas
        await using var tenantBCtx = db.CreateTenantContext(tenantB);
        var visivelB = await tenantBCtx.TabelasProcedimento
            .AnyAsync(t => t.OperadoraId == op.Id);

        Assert.False(visivelB);
    }

    private sealed class FakeImpersonatingUser(Guid tenantId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => tenantId;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => true;
        public bool IsImpersonating => true;
        public bool IsAuthenticated => true;
    }
}
