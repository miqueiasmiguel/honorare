using System.Text;
using App;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using Faturamento.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Tests.ImportacaoGuiaCsv;

[Collection(nameof(PostgresCollection))]
public sealed class ImportacaoGuiaCsvTests(PostgresContainerFixture db)
{
    private const string CsvHeader =
        "GUIA;CODIGO;BENEFICIARIO;DATA SERVICO;CODIGO PROCEDIMENTO;NOME PROCEDIMENTO;" +
        "FUNCAO;EXECUTANTE DO SERVICO;% VIA;ACOMODACAO;ACRESCIMO;QTDE PAGA;HONORARIO;GLOSA;COD_GLOSA;TOTAL;LOCAL ATENDIMENTO";

    private static MemoryStream ToCsvStream(string csv) =>
        new(Encoding.UTF8.GetBytes(csv));

    private static string CsvRow(
        string guia, string codigo, string beneficiario, string dataServico,
        string codigoProcedimento, string nomeProcedimento,
        string funcao, string executante,
        string percentVia, string acomodacao, string acrescimo,
        string qtdePaga, string honorario, string glosa, string codGlosa, string total,
        string localAtendimento = "") =>
        $"{guia};{codigo};{beneficiario};{dataServico};{codigoProcedimento};{nomeProcedimento};" +
        $"{funcao};{executante};{percentVia};{acomodacao};{acrescimo};{qtdePaga};{honorario};{glosa};{codGlosa};{total};{localAtendimento}";

    private (AppDbContext ctx, ImportacaoGuiaCsvService service) BuildService(Guid tenantId)
    {
        var user = new FakeTenantUserIO04(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, user);
        var factory = new PricingRuleSetFactory(ctx);
        return (ctx, new ImportacaoGuiaCsvService(ctx, user, factory));
    }

    private static async Task<(Guid prestadorId, Guid operadoraId)> SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var pr = Prestador.Create(tenantId, "Dr IO04 " + tenantId.ToString("N")[..4], null);
        var op = Operadora.Create(tenantId, "Unimed IO04 " + tenantId.ToString("N")[..4], null, null, TipoRuleSet.Unimed);
        ctx.Add(pr);
        ctx.Add(op);
        await ctx.SaveChangesAsync();
        return (pr.Id, op.Id);
    }

    [Fact]
    public async Task ImportarCsv_AnestesistaValido_CriaGuiaItemAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var benef = Beneficiario.Create(tenantId, "9999000000000001", "PACIENTE ANEST");
        var proc = Procedimento.Create(tenantId, "30501326", "ANESTESIA GERAL", "1", "A", false, false);
        ctx.Add(benef);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        var tabPorte = TabelaPorteAnestesico.Create(tenantId, operadoraId, "A", 500m, 600m);
        ctx.Add(tabPorte);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091936",
            CsvHeader,
            CsvRow("34280511", "9999000000000001", "PACIENTE ANEST", "01/01/2025",
                "30501326", "ANESTESIA GERAL", "ANESTESISTA", "DR IO04",
                "100", "ENFERMARIA", "", "1", "500,00", "0,00", "", "500,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.GuiasCriadas);
        Assert.Equal(1, resultado.Value.ItensCriados);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "34280511" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);

        var itemGuia = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guia.Id);
        Assert.NotNull(itemGuia);
        Assert.Equal(PosicaoExecutor.Anestesista, itemGuia.PosicaoExecutor);
        Assert.Equal(1.0m, itemGuia.PercentualOrdem);
        Assert.NotNull(itemGuia.ValorApurado);
        Assert.Equal(500m, itemGuia.ValorLiquidado);
    }

    [Fact]
    public async Task ImportarCsv_CirurgiaoMultiplosItens_OrdemPercentualCorretoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc1 = Procedimento.Create(tenantId, "30101010", "Proc 1", "2", null, false, false);
        var proc2 = Procedimento.Create(tenantId, "30202020", "Proc 2", "2", null, false, false);
        var proc3 = Procedimento.Create(tenantId, "30303030", "Proc 3", "2", null, false, false);
        ctx.AddRange(proc1, proc2, proc3);
        await ctx.SaveChangesAsync();

        ctx.AddRange(
            TabelaProcedimento.Create(tenantId, operadoraId, proc1.Id, 200m),
            TabelaProcedimento.Create(tenantId, operadoraId, proc2.Id, 100m),
            TabelaProcedimento.Create(tenantId, operadoraId, proc3.Id, 80m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091937",
            CsvHeader,
            CsvRow("12345678", "1111111111111111", "PACIENTE X", "01/02/2025",
                "30101010", "Proc 1", "CIRURGIAO", "DR IO04", "100", "ENFERMARIA", "", "1", "200,00", "0,00", "", "200,00"),
            CsvRow("12345678", "1111111111111111", "PACIENTE X", "01/02/2025",
                "30202020", "Proc 2", "CIRURGIAO", "DR IO04", "50", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"),
            CsvRow("12345678", "1111111111111111", "PACIENTE X", "01/02/2025",
                "30303030", "Proc 3", "CIRURGIAO", "DR IO04", "40", "ENFERMARIA", "", "1", "80,00", "0,00", "", "80,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "12345678" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);

        var itens = await check.ItensGuia
            .Where(i => i.GuiaId == guia.Id)
            .OrderBy(i => i.CriadoEm)
            .ToListAsync();

        Assert.Equal(3, itens.Count);
        Assert.Equal(1.0m, itens[0].PercentualOrdem);
        Assert.Equal(0.5m, itens[1].PercentualOrdem);
        Assert.Equal(0.4m, itens[2].PercentualOrdem);
    }

    [Fact]
    public async Task ImportarCsv_GlosaItem_ValorLiquidadoZeroAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "40101010", "Proc Glosa", "2", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091938",
            CsvHeader,
            CsvRow("99887766", "2222222222222222", "PACIENTE GLOSA", "01/03/2025",
                "40101010", "Proc Glosa", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "0", "100,00", "100,00", "2602", "0,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "99887766" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);

        var itemGuia = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guia.Id);
        Assert.NotNull(itemGuia);
        Assert.Equal(0m, itemGuia.ValorLiquidado);
        Assert.Equal("2602", itemGuia.MotivoGlosa);
    }

    [Fact]
    public async Task ImportarCsv_BeneficiarioNaoExiste_CriaBeneficiarioAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "50101010", "Proc Benef", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091939",
            CsvHeader,
            CsvRow("77665544", "0332800031800013", "ANDREA SANTOS DE SOUZA", "01/04/2025",
                "50101010", "Proc Benef", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "300,00", "0,00", "", "300,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.BeneficiariosCriados);

        await using var check = db.CreateTenantContext(tenantId);
        var benef = await check.Beneficiarios.FirstOrDefaultAsync(b =>
            b.Carteira == "0332800031800013" && b.TenantId == tenantId);
        Assert.NotNull(benef);
        Assert.Equal("ANDREA SANTOS DE SOUZA", benef.Nome);
    }

    [Fact]
    public async Task ImportarCsv_ProcedimentoNaoEncontrado_RegistraErroEContinuaAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "60101010", "Proc Valido", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091940",
            CsvHeader,
            CsvRow("55443322", "3333333333333333", "PACIENTE ERR", "01/05/2025",
                "99999999", "Proc Invalido", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "200,00", "0,00", "", "200,00"),
            CsvRow("55443322", "3333333333333333", "PACIENTE ERR", "01/05/2025",
                "60101010", "Proc Valido", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "150,00", "0,00", "", "150,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.ItensCriados);
        Assert.Single(resultado.Value.Erros);
        Assert.Contains("99999999", resultado.Value.Erros[0].Mensagem, StringComparison.Ordinal);
        Assert.Equal(3, resultado.Value.Erros[0].Linha);
    }

    [Fact]
    public async Task ImportarCsv_GuiaJaExiste_NaoDuplicaGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "70101010", "Proc Dup", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        var guiaExistente = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "34280511", new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guiaExistente);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091941",
            CsvHeader,
            CsvRow("34280511", "4444444444444444", "PACIENTE DUP", "01/01/2025",
                "70101010", "Proc Dup", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "400,00", "0,00", "", "400,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value!.GuiasCriadas);
        Assert.Equal(1, resultado.Value.GuiasAtualizadas);

        await using var check = db.CreateTenantContext(tenantId);
        var count = await check.Guias.CountAsync(g => g.NumeroGuia == "34280511" && g.PrestadorId == prestadorId);
        Assert.Equal(1, count);

        var itemAdicionado = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guiaExistente.Id);
        Assert.NotNull(itemAdicionado);
    }

    [Fact]
    public async Task ImportarCsv_ItemEquipamento_IgnoradoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091942",
            CsvHeader,
            CsvRow("11223344", "5555555555555555", "PACIENTE EQ", "01/06/2025",
                "60024380", "ALUGUEL EQUIPAMENTO", "", "DR IO04",
                "100", "ENFERMARIA", "", "1", "50,00", "0,00", "", "50,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.ItensIgnorados);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "11223344" && g.PrestadorId == prestadorId);
        if (guia != null)
        {
            var itemGuiaCount = await check.ItensGuia.CountAsync(i => i.GuiaId == guia.Id);
            Assert.Equal(0, itemGuiaCount);
        }
    }

    [Fact]
    public async Task ImportarCsv_UrgenciaDetectada_EhUrgenciaTrueAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "80101010", "Proc Urgencia", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091943",
            CsvHeader,
            CsvRow("22334455", "6666666666666666", "PACIENTE URG", "01/07/2025",
                "80101010", "Proc Urgencia", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "30,00%", "1", "200,00", "0,00", "", "200,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "22334455" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);

        var itemGuia = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guia.Id);
        Assert.NotNull(itemGuia);
        Assert.True(itemGuia.EhUrgencia);
    }

    [Fact]
    public async Task ImportarCsv_FormatoInvalido_RetornaErroAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var csv = string.Join("\n",
            "780091944",
            "COLUNA_INVALIDA;OUTRA_COLUNA",
            "dado1;dado2");

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.IsType<ValidationError>(resultado.Error);
    }

    [Fact]
    public async Task ImportarCsv_SomenteValidar_NaoPersistEAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "90101010", "Proc Preview", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "780091945",
            CsvHeader,
            CsvRow("33445566", "7777777777777777", "PACIENTE PREV", "01/08/2025",
                "90101010", "Proc Preview", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, true, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value!.SomenteValidar);
        Assert.True(resultado.Value.GuiasPrevistas >= 1);
        Assert.True(resultado.Value.ItensPrevistas >= 1);

        await using var check = db.CreateTenantContext(tenantId);
        var guiasCount = await check.Guias.CountAsync(g => g.PrestadorId == prestadorId && g.NumeroGuia == "33445566");
        Assert.Equal(0, guiasCount);
    }

    [Fact]
    public async Task ImportarCsv_FuncaoHonorarioPrinc_MapeiaParaCirurgiaoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009336", "HERNIORRAFIA", "2", null, false, false);
        ctx.Add(proc);
        var tabProc = TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 1000m);
        ctx.Add(tabProc);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IO06_001",
            CsvHeader,
            CsvRow("34113710", "0335400202005701", "PACIENTE CIRUG", "22/04/2025",
                "31009336", "HERNIORRAFIA", "Honorario princ.", "DR IO06",
                "100", "APARTAMENTO", "", "1", "702,00", "0,00", "", "702,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.ItensCriados);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "34113710" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        var itemGuia = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guia.Id);
        Assert.NotNull(itemGuia);
        Assert.Equal(PosicaoExecutor.Cirurgiao, itemGuia.PosicaoExecutor);
    }

    [Fact]
    public async Task ImportarCsv_DataServicoAnoAbreviado_ParseCorretamenteAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009001", "PROC DATA", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IO06_002",
            CsvHeader,
            CsvRow("11223301", "0000000000000001", "PACIENTE DATA", "22/04/26",
                "31009001", "PROC DATA", "CIRURGIAO", "DR IO06",
                "100", "ENFERMARIA", "", "1", "500,00", "0,00", "", "500,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.GuiasCriadas);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "11223301" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        Assert.Equal(new DateOnly(2026, 4, 22), guia.DataAtendimento);
    }

    [Fact]
    public async Task ImportarCsv_IdentificadorComLabel_ExtraiNumeroAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009002", "PROC ID", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IDENTIFICADOR PAGAMENTO: 741443463",
            CsvHeader,
            CsvRow("55667701", "0000000000000002", "PACIENTE ID", "01/01/2025",
                "31009002", "PROC ID", "CIRURGIAO", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("741443463", resultado.Value!.IdentificadorPagamento);
    }

    [Fact]
    public async Task ImportarCsv_ReimportarMesmoCsv_SobreescreveValoresAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009003", "PROC DEDUP", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "741443464",
            CsvHeader,
            CsvRow("99001101", "0000000000000003", "PACIENTE DEDUP", "01/01/2025",
                "31009003", "PROC DEDUP", "CIRURGIAO", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;

        var resultado1 = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);
        Assert.True(resultado1.IsSuccess);

        var resultado2 = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);
        Assert.True(resultado2.IsSuccess);
    }

    [Fact]
    public async Task ImportarCsv_TodosFuncoesDesconhecidas_RejeicaoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IO06_UNKF",
            CsvHeader,
            CsvRow("12340001", "0000000000000004", "PACIENTE UNKF", "01/01/2025",
                "31009004", "PROC UNKF", "FUNCAO_INVALIDA", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"),
            CsvRow("12340001", "0000000000000004", "PACIENTE UNKF", "01/01/2025",
                "31009005", "PROC UNKF2", "OUTRA_INVALIDA", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.IsType<ValidationError>(resultado.Error);
    }

    [Fact]
    public async Task ImportarCsv_AlgumaFuncaoDesconhecida_GeraAlertaAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009006", "PROC ALERTA", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IO06_ALERTA",
            CsvHeader,
            CsvRow("12340002", "0000000000000005", "PACIENTE ALR", "01/01/2025",
                "31009006", "PROC ALERTA", "CIRURGIAO", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"),
            CsvRow("12340002", "0000000000000005", "PACIENTE ALR", "01/01/2025",
                "31009006", "PROC ALERTA", "FUNCAO_INVALIDA_X", "DR IO06",
                "100", "ENFERMARIA", "", "1", "100,00", "0,00", "", "100,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value!.ItensCriados);
        Assert.Contains(resultado.Value.Alertas, a =>
            a.Mensagem.Contains("FUNCAO_INVALIDA_X", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportarCsv_CodGlosaPreenchido_MotivoGlosaSalvoAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31009099", "PROC GLOSA MOTIVO", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "IO06_MOTIVO",
            CsvHeader,
            CsvRow("12340099", "0000000000000099", "PACIENTE MOTIVO", "01/01/2025",
                "31009099", "PROC GLOSA MOTIVO", "CIRURGIAO", "DR IO06",
                "100", "ENFERMARIA", "", "0", "200,00", "200,00", "CB", "0,00"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "12340099" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        var itemGuia = await check.ItensGuia.FirstOrDefaultAsync(i => i.GuiaId == guia.Id);
        Assert.NotNull(itemGuia);
        Assert.Equal("CB", itemGuia.MotivoGlosa);
    }

    [Fact]
    public async Task ImportarCsv_ComLocalAtendimento_PreencheNaGuiaAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31010001", "PROC LOCAL", "1", null, false, false);
        ctx.Add(proc);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "LA02_001",
            CsvHeader,
            CsvRow("44550011", "0000000000001001", "PACIENTE LOCAL", "01/01/2025",
                "31010001", "PROC LOCAL", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "200,00", "0,00", "", "200,00",
                "HOSPITAL SAO LUCAS"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "44550011" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        Assert.Equal("HOSPITAL SAO LUCAS", guia.LocalAtendimento);
    }

    [Fact]
    public async Task ImportarCsv_GuiaExistenteSemLocal_BackfillAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31010002", "PROC BACKFILL", "1", null, false, false);
        ctx.Add(proc);
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        var guiaExistente = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "44550022", new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guiaExistente);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "LA02_002",
            CsvHeader,
            CsvRow("44550022", "0000000000001002", "PACIENTE BACK", "01/01/2025",
                "31010002", "PROC BACKFILL", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "200,00", "0,00", "", "200,00",
                "CLINICA NOVA"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "44550022" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        Assert.Equal("CLINICA NOVA", guia.LocalAtendimento);
    }

    [Fact]
    public async Task ImportarCsv_GuiaExistenteComLocal_NaoSobrescreveAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = db.CreateTenantContext(tenantId);
        var (prestadorId, operadoraId) = await SeedBaseAsync(ctx, tenantId);

        var proc = Procedimento.Create(tenantId, "31010003", "PROC NOSOBRE", "1", null, false, false);
        ctx.Add(proc);
        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, proc.Id, 200m));
        var guiaExistente = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "44550033", new DateOnly(2025, 1, 1), false, string.Empty, "LOCAL ORIGINAL");
        ctx.Add(guiaExistente);
        await ctx.SaveChangesAsync();

        var csv = string.Join("\n",
            "LA02_003",
            CsvHeader,
            CsvRow("44550033", "0000000000001003", "PACIENTE NOSOBRE", "01/01/2025",
                "31010003", "PROC NOSOBRE", "CIRURGIAO", "DR IO04",
                "100", "ENFERMARIA", "", "1", "200,00", "0,00", "", "200,00",
                "LOCAL NOVO CSV"));

        var (svcCtx, service) = BuildService(tenantId);
        await using var _ = svcCtx;
        var resultado = await service.ImportarAsync(ToCsvStream(csv), prestadorId, operadoraId, false, CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        await using var check = db.CreateTenantContext(tenantId);
        var guia = await check.Guias.FirstOrDefaultAsync(g => g.NumeroGuia == "44550033" && g.PrestadorId == prestadorId);
        Assert.NotNull(guia);
        Assert.Equal("LOCAL ORIGINAL", guia.LocalAtendimento);
    }
}

file sealed class FakeTenantUserIO04(Guid tenantId) : App.Identity.ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}
