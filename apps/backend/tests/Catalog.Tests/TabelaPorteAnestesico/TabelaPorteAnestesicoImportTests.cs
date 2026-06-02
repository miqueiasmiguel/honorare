using System.Text;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.TabelaPorteAnestesico;

[Collection(nameof(PostgresCollection))]
public sealed class TabelaPorteAnestesicoImportTests(PostgresContainerFixture db)
{
    private static readonly string[] _headerLines = Enumerable.Repeat("Cabecalho", 8).ToArray();
    private const string DataHeader = "Código,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte";

    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakePorteUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    private static async Task<OperadoraDto> CriarOperadoraAsync(CatalogService service)
    {
        var result = await service.CriarAsync(
            new CriarOperadoraCommand("UNIMED JPA", null, null, TipoRuleSet.Unimed));
        return result.Value!;
    }

    private static async Task CriarProcedimentoAsync(CatalogService service, string codigoTuss)
    {
        await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(codigoTuss, "Proc " + codigoTuss, null, null, false, false, true));
    }

    private static MemoryStream BuildCsvStream(params string[] dataLines)
    {
        var sb = new StringBuilder();
        foreach (var h in _headerLines)
        {
            sb.AppendLine(h);
        }

        sb.AppendLine(DataHeader);
        foreach (var line in dataLines)
        {
            sb.AppendLine(line);
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Fact]
    public async Task ImportarCsv_2Portes_5Procs_RetornaContadoresCorretosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30101050");
        await CriarProcedimentoAsync(service, "30101069");

        // 5 data lines: 3 with porte E (proc 30101050), 2 with porte D (proc 30101069)
        var stream = BuildCsvStream(
            @"30101050,APENDICE PRE-AURICULAR,""224,64"","""",""292,5"",""468,00"",E",
            @"30101050,APENDICE PRE-AURICULAR,""224,64"","""",""292,5"",""468,00"",E",
            @"30101069,OUTRO PROC,""100,00"","""",""150,00"",""250,00"",D",
            @"30101069,OUTRO PROC,""100,00"","""",""150,00"",""250,00"",D",
            @"30101050,APENDICE PRE-AURICULAR,""224,64"","""",""292,5"",""468,00"",E");

        var result = await service.ImportarTabelaUnimedAnestesistaAsync(stream, op.Id);

        Assert.Equal(2, result.PortesAtualizados);
        Assert.Equal(2, result.ProcedimentosAtualizados);
        Assert.Empty(result.ProcedimentosNaoEncontrados);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_TussInexistente_ListaCodigoNaoEncontradoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);

        var stream = BuildCsvStream(
            @"99999999,PROC INEXISTENTE,""100,00"","""",""150,00"",""250,00"",J");

        var result = await service.ImportarTabelaUnimedAnestesistaAsync(stream, op.Id);

        Assert.Equal(1, result.PortesAtualizados);
        Assert.Equal(0, result.ProcedimentosAtualizados);
        Assert.Equal(["99999999"], result.ProcedimentosNaoEncontrados);
    }

    [Fact]
    public async Task ImportarCsv_PorteDuplicado_Upsert_NaoDuplicaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);

        var stream1 = BuildCsvStream(
            @"30101050,PROC A,""224,64"","""",""292,5"",""468,00"",J");
        await service.ImportarTabelaUnimedAnestesistaAsync(stream1, op.Id);

        var stream2 = BuildCsvStream(
            @"30101050,PROC A,""224,64"","""",""300,00"",""500,00"",J");
        await service.ImportarTabelaUnimedAnestesistaAsync(stream2, op.Id);

        await using var ctxVerify = db.CreateContext();
        var count = await ctxVerify.TabelasPorteAnestesico
            .IgnoreQueryFilters()
            .CountAsync(t => t.OperadoraId == op.Id && t.PorteLetra == "J");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportarCsv_LinhaMalformada_RegistraErroEContinuaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);

        // line 10 (data-line 1) is malformed, line 11 is valid
        var stream = BuildCsvStream(
            "MALFORMADA_SEM_COLUNAS_SUFICIENTES",
            @"30101050,PROC A,""224,64"","""",""292,5"",""468,00"",E");

        var result = await service.ImportarTabelaUnimedAnestesistaAsync(stream, op.Id);

        Assert.Single(result.Erros);
        Assert.Equal(10, result.Erros[0].Linha);
        Assert.Equal(1, result.PortesAtualizados);
    }
}

file sealed class FakePorteUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
