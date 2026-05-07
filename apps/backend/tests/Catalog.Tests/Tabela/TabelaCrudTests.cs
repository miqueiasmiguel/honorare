using System.Text;
using App;
using App.Catalog;
using App.Data;
using App.Identity;
using Catalog.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Tabela;

[Collection(nameof(PostgresCollection))]
public sealed class TabelaCrudTests(PostgresContainerFixture db)
{
    private const string HeaderCsv = "CodigoTuss;Valor";

    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var user = new FakeTabelaUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, user), user);
    }

    private static async Task<OperadoraDto> CriarOperadoraAsync(CatalogService service, string nome = "UNIMED")
    {
        var result = await service.CriarAsync(new CriarOperadoraCommand(nome, null, null, TipoRuleSet.Unimed));
        return result.Value!;
    }

    private static async Task<ProcedimentoDto> CriarProcedimentoAsync(
        CatalogService service, string codigoTuss = "12345678")
    {
        var result = await service.CriarProcedimentoAsync(
            new SalvarProcedimentoCommand(codigoTuss, "Proc " + codigoTuss, null, null, false, false, true));
        return result.Value!;
    }

    private static MemoryStream ToCsvStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    // ── CRUD básico ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_FiltraPorOperadoraId_RetornaSomenteDaOperadoraAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op1 = await CriarOperadoraAsync(service, "UNIMED A");
        var op2 = await CriarOperadoraAsync(service, "UNIMED B");
        var proc1 = await CriarProcedimentoAsync(service, "10000001");
        var proc2 = await CriarProcedimentoAsync(service, "10000002");

        await service.CriarTabelaAsync(new SalvarTabelaCommand(op1.Id, proc1.Id, 100m));
        await service.CriarTabelaAsync(new SalvarTabelaCommand(op2.Id, proc2.Id, 200m));

        var result = await service.ListarTabelasAsync(
            new ListarTabelasQuery(op1.Id, null, 1, 20));

        Assert.Single(result.Itens);
        Assert.Equal(op1.Id, result.Itens[0].OperadoraId);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_RetornaTabelaDtoAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        var proc = await CriarProcedimentoAsync(service);

        var result = await service.CriarTabelaAsync(
            new SalvarTabelaCommand(op.Id, proc.Id, 150.50m));

        Assert.True(result.IsSuccess);
        Assert.Equal(op.Id, result.Value!.OperadoraId);
        Assert.Equal(proc.Id, result.Value.ProcedimentoId);
        Assert.Equal(150.50m, result.Value.Valor);
    }

    [Fact]
    public async Task Criar_ValorZeroOuNegativo_RetornaValidationErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        var proc = await CriarProcedimentoAsync(service);

        var resultZero = await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, 0m));
        var resultNeg = await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, -10m));

        Assert.True(resultZero.IsFailure);
        Assert.IsType<ValidationError>(resultZero.Error);
        Assert.True(resultNeg.IsFailure);
        Assert.IsType<ValidationError>(resultNeg.Error);
    }

    [Fact]
    public async Task Criar_DuplicadoMesmaOperadoraProcedimento_RetornaConflictErrorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        var proc = await CriarProcedimentoAsync(service);

        await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, 100m));
        var result = await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, 200m));

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task Atualizar_EntradaExistente_AtualizaValorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        var proc = await CriarProcedimentoAsync(service);
        var criado = await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, 100m));
        Assert.True(criado.IsSuccess);

        var result = await service.AtualizarTabelaAsync(
            criado.Value!.Id,
            new SalvarTabelaCommand(op.Id, proc.Id, 350m));

        Assert.True(result.IsSuccess);
        Assert.Equal(350m, result.Value!.Valor);
    }

    [Fact]
    public async Task Excluir_EntradaExistente_RemoveAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        var proc = await CriarProcedimentoAsync(service);
        var criado = await service.CriarTabelaAsync(new SalvarTabelaCommand(op.Id, proc.Id, 100m));
        Assert.True(criado.IsSuccess);

        var result = await service.ExcluirTabelaAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        var obter = await service.ObterTabelaPorIdAsync(criado.Value.Id);
        Assert.True(obter.IsFailure);
    }

    [Fact]
    public async Task Listar_NaoRetornaTabelasDeOutroTenantAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (ctxA, userA) = BuildTenant(tenantA);
        await using var _a = ctxA;
        var serviceA = new CatalogService(ctxA, userA);
        var opA = await CriarOperadoraAsync(serviceA, "UNIMED A");
        var procA = await CriarProcedimentoAsync(serviceA, "20000001");
        var tabelaA = await serviceA.CriarTabelaAsync(new SalvarTabelaCommand(opA.Id, procA.Id, 100m));

        var (ctxB, userB) = BuildTenant(tenantB);
        await using var _b = ctxB;
        var serviceB = new CatalogService(ctxB, userB);
        var opB = await CriarOperadoraAsync(serviceB, "UNIMED B");
        var procB = await CriarProcedimentoAsync(serviceB, "20000001");
        var tabelaB = await serviceB.CriarTabelaAsync(new SalvarTabelaCommand(opB.Id, procB.Id, 200m));

        var result = await serviceA.ListarTabelasAsync(new ListarTabelasQuery(opA.Id, null, 1, 100));

        Assert.Contains(result.Itens, i => i.Id == tabelaA.Value!.Id);
        Assert.DoesNotContain(result.Itens, i => i.Id == tabelaB.Value!.Id);
    }

    // ── CSV import ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportarCsv_ArquivoValido_InsereNovasAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30000001");
        await CriarProcedimentoAsync(service, "30000002");

        var csv = $"{HeaderCsv}\n30000001;100.00\n30000002;200.00\n";
        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(csv), op.Id);

        Assert.Equal(2, result.Inseridos);
        Assert.Equal(0, result.Atualizados);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_CodigoExistente_AtualizaValorAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30001001");

        var csvInicial = $"{HeaderCsv}\n30001001;100.00\n";
        await service.ImportarTabelaCsvAsync(ToCsvStream(csvInicial), op.Id);

        var csvAtualiza = $"{HeaderCsv}\n30001001;999.50\n";
        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(csvAtualiza), op.Id);

        Assert.Equal(0, result.Inseridos);
        Assert.Equal(1, result.Atualizados);
        Assert.Empty(result.Erros);

        var lista = await service.ListarTabelasAsync(new ListarTabelasQuery(op.Id, "30001001", 1, 10));
        var item = Assert.Single(lista.Itens);
        Assert.Equal(999.50m, item.Valor);
    }

    [Fact]
    public async Task ImportarCsv_LinhaComErro_NaoAbortaBatchAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30002001");

        // linha 2 tem Valor inválido; linha 3 é válida
        var csv = $"{HeaderCsv}\n30002001;abc\n30002001;150.00\n";
        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(csv), op.Id);

        Assert.Equal(1, result.Inseridos);
        Assert.Single(result.Erros);
    }

    [Fact]
    public async Task ImportarCsv_CodigoTussInexistente_RegistraErroDeLinhaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30003001");

        // 30003001 existe, 99999999 não existe
        var csv = $"{HeaderCsv}\n30003001;100.00\n99999999;200.00\n";
        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(csv), op.Id);

        Assert.Equal(1, result.Inseridos);
        Assert.Single(result.Erros);
        Assert.Equal(3, result.Erros[0].Linha);
    }

    [Fact]
    public async Task ImportarCsv_ValorInvalido_RegistraErroDeLinhaAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);
        await CriarProcedimentoAsync(service, "30004001");
        await CriarProcedimentoAsync(service, "30004002");

        // linha 2: valor não numérico; linha 3: valor zero
        var csv = $"{HeaderCsv}\n30004001;nao-e-numero\n30004002;0\n";
        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(csv), op.Id);

        Assert.Equal(0, result.Inseridos);
        Assert.Equal(2, result.Erros.Count);
    }

    [Fact]
    public async Task ImportarCsv_AcimaLimite_RetornaErroGeralAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var service = new CatalogService(ctx, user);

        var op = await CriarOperadoraAsync(service);

        var sb = new StringBuilder(HeaderCsv).AppendLine();
        for (var i = 0; i <= 10000; i++)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"{i:D8};{i + 1}.00");
        }

        var result = await service.ImportarTabelaCsvAsync(ToCsvStream(sb.ToString()), op.Id);

        Assert.NotEmpty(result.Erros);
        Assert.Equal(0, result.Inseridos);
    }
}

file sealed class FakeTabelaUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsAuthenticated => true;
}
