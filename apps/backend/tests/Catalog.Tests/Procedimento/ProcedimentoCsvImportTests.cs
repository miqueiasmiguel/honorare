using System.Text;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Procedimento;

[Collection(nameof(PostgresCollection))]
public sealed class ProcedimentoCsvImportTests(PostgresContainerFixture db)
{
    private const string Header =
        "CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo";

    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakeCsvImportUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    private static MemoryStream ToCsvStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task ImportarCsv_TodasColunasPresentes_InsereTodosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csv = $"{Header}\n30715013;Herniorrafia inguinal;6B;4;false;false\n40314340;Eletroencefalograma;;;true;false\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csv));

        Assert.Equal(2, result.Inseridos);
        Assert.Equal(0, result.Atualizados);
        Assert.Equal(0, result.Ignorados);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_CodigoTussJaExistente_AtualizaCamposAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csvInicial = $"{Header}\n30715013;Descricao original;6B;;;false\n";
        await service.ImportarProcedimentosCsvAsync(ToCsvStream(csvInicial));

        var csvAtualiza = $"{Header}\n30715013;Descricao atualizada;7A;3;false;false\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csvAtualiza));

        Assert.Equal(0, result.Inseridos);
        Assert.Equal(1, result.Atualizados);

        var listar = await service.ListarProcedimentosAsync(
            new ListarProcedimentosQuery("30715013", null, 1, 10));
        var proc = Assert.Single(listar.Itens);
        Assert.Equal("Descricao atualizada", proc.Descricao);
        Assert.Equal("7A", proc.Porte);
        Assert.Equal(3, proc.PorteAnestesico);
    }

    [Fact]
    public async Task ImportarCsv_CodigoTussJaExistente_NaoAlteraTenantIdAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string CodigoTuss = "30715013";

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var csvA = $"{Header}\n{CodigoTuss};Desc Tenant A;;;false;false\n";
        await serviceA.ImportarProcedimentosCsvAsync(ToCsvStream(csvA));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var csvB = $"{Header}\n{CodigoTuss};Desc Tenant B;;;false;false\n";
        await serviceB.ImportarProcedimentosCsvAsync(ToCsvStream(csvB));

        var listarA = await serviceA.ListarProcedimentosAsync(
            new ListarProcedimentosQuery(CodigoTuss, null, 1, 10));
        var procA = Assert.Single(listarA.Itens);
        Assert.Equal("Desc Tenant A", procA.Descricao);

        var listarB = await serviceB.ListarProcedimentosAsync(
            new ListarProcedimentosQuery(CodigoTuss, null, 1, 10));
        var procB = Assert.Single(listarB.Itens);
        Assert.Equal("Desc Tenant B", procB.Descricao);
    }

    [Fact]
    public async Task ImportarCsv_LinhaSemCodigoTuss_IgnoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csv = $"{Header}\n;Descricao sem codigo;;;false;false\n30715013;Com codigo;;;false;false\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csv));

        Assert.Equal(1, result.Inseridos);
        Assert.Equal(1, result.Ignorados);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_PorteAnestesicoInvalido_RegistraErroMasContinuaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csv = $"{Header}\n30715013;Proc valido;;;false;false\n40314340;Proc invalido;;9;false;false\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csv));

        Assert.Equal(1, result.Inseridos);
        Assert.Single(result.Erros);
        Assert.Equal(3, result.Erros[0].Linha);
    }

    [Fact]
    public async Task ImportarCsv_ColunasOpcionaisAusentes_UsaDefaultsFalseNullAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csv = $"{Header}\n30715013;Apenas obrigatorios;;;;\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csv));

        Assert.Equal(1, result.Inseridos);
        Assert.Empty(result.Erros);

        var listar = await service.ListarProcedimentosAsync(
            new ListarProcedimentosQuery("30715013", null, 1, 10));
        var proc = Assert.Single(listar.Itens);
        Assert.Null(proc.Porte);
        Assert.Null(proc.PorteAnestesico);
        Assert.False(proc.EhSadt);
        Assert.False(proc.TemPorteProprioVideo);
    }

    [Fact]
    public async Task ImportarCsv_SeparadorPontoEVirgula_ParseiaCorretamenteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var csv = "CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo\n30715013;Herniorrafia inguinal;6B;4;false;false\n";
        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(csv));

        Assert.Equal(1, result.Inseridos);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_TenantDistinto_NaoInterfereCadastroExistenteAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var csvA = $"{Header}\n10000001;Proc exclusivo A;;;false;false\n";
        await serviceA.ImportarProcedimentosCsvAsync(ToCsvStream(csvA));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var csvB = $"{Header}\n10000001;Proc exclusivo B;;;false;false\n";
        var result = await serviceB.ImportarProcedimentosCsvAsync(ToCsvStream(csvB));

        Assert.Equal(1, result.Inseridos);

        var listarA = await serviceA.ListarProcedimentosAsync(
            new ListarProcedimentosQuery(null, null, 1, 100));
        Assert.Contains(listarA.Itens, p => p.CodigoTuss == "10000001" && p.Descricao == "Proc exclusivo A");

        var listarB = await serviceB.ListarProcedimentosAsync(
            new ListarProcedimentosQuery(null, null, 1, 100));
        Assert.Contains(listarB.Itens, p => p.CodigoTuss == "10000001" && p.Descricao == "Proc exclusivo B");
    }

    [Fact]
    public async Task ImportarCsv_Maisde10000Linhas_RetornaErroGenericoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var sb = new StringBuilder(Header).AppendLine();
        for (var i = 0; i <= 10000; i++)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"{i:D8};Desc {i};;;false;false");
        }

        var result = await service.ImportarProcedimentosCsvAsync(ToCsvStream(sb.ToString()));

        Assert.NotEmpty(result.Erros);
        Assert.Equal(0, result.Inseridos);
    }
}

file sealed class FakeCsvImportUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
