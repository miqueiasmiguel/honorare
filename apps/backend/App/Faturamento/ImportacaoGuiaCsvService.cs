using System.Globalization;
using App.Catalog;
using App.Data;
using App.Faturamento.Motor;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento;

internal sealed record ImportacaoResultado(
    string IdentificadorPagamento,
    bool SomenteValidar,
    int GuiasCriadas,
    int GuiasAtualizadas,
    int ItensCriados,
    int ItensAtualizados,
    int ItensIgnorados,
    int BeneficiariosCriados,
    int GuiasPrevistas,
    int ItensPrevistas,
    IReadOnlyList<ErroImportacao> Erros,
    IReadOnlyList<AlertaImportacao> Alertas);

internal sealed record ErroImportacao(int Linha, string Mensagem);

internal sealed record AlertaImportacao(int Linha, string Mensagem);

internal sealed class ImportacaoGuiaCsvService(
    AppDbContext db, ICurrentUser currentUser, PricingRuleSetFactory factory)
{
    private static readonly string[] _requiredHeaders =
        ["GUIA", "CODIGO PROCEDIMENTO", "HONORARIO", "% VIA", "ACOMODACAO"];

    internal async Task<Result<ImportacaoResultado>> ImportarAsync(
        Stream csvStream, Guid prestadorId, Guid operadoraId,
        bool somenteValidar, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId!.Value;

        var prestador = await db.Prestadores.FirstOrDefaultAsync(p => p.Id == prestadorId, ct);
        if (prestador is null)
        {
            return Result<ImportacaoResultado>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        var operadora = await db.Operadoras.FirstOrDefaultAsync(o => o.Id == operadoraId, ct);
        if (operadora is null)
        {
            return Result<ImportacaoResultado>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        using var reader = new StreamReader(csvStream);
        var parseResult = ParsearCsv(reader, out var identificadorPagamento);
        if (parseResult.IsFailure)
        {
            return Result<ImportacaoResultado>.Fail(parseResult.Error!);
        }

        var linhas = parseResult.Value!;

        var linhasComFuncao = linhas.Where(l => !string.IsNullOrWhiteSpace(l.Funcao)).ToList();
        if (linhasComFuncao.Count > 0 && linhasComFuncao.All(l => MapearFuncao(l.Funcao) is null))
        {
            var funcoesUnicas = linhasComFuncao
                .Select(l => l.Funcao.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var lista = string.Join(", ", funcoesUnicas.Select(f => $"'{f}'"));
            return Result<ImportacaoResultado>.Fail(
                new ValidationError(
                    $"Nenhum item pôde ser importado — funções não reconhecidas: {lista}."));
        }

        if (somenteValidar)
        {
            var guiasPrevistas = linhas
                .Where(l => !string.IsNullOrWhiteSpace(l.Funcao))
                .Select(l => (l.Guia, l.DataServico))
                .Distinct()
                .Count();
            var itensPrevistas = linhas.Count(l => !string.IsNullOrWhiteSpace(l.Funcao));

            return Result<ImportacaoResultado>.Ok(new ImportacaoResultado(
                identificadorPagamento, true,
                0, 0, 0, 0, 0, 0,
                guiasPrevistas, itensPrevistas,
                [], []));
        }

        var erros = new List<ErroImportacao>();
        var alertas = new List<AlertaImportacao>();
        var funcoesIgnoradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guiasCriadas = 0;
        var guiasAtualizadas = 0;
        var itensCriados = 0;
        var itensAtualizados = 0;
        var itensIgnorados = 0;
        var beneficiariosCriados = 0;

        var grupos = linhas.GroupBy(l => (l.Guia, l.DataServico));
        var todasGuias = new List<(Guia Guia, Operadora Op, List<ItemGuia> Itens)>();

        foreach (var grupo in grupos)
        {
            var primeiraLinha = grupo.First();

            var carteira = primeiraLinha.Codigo.Trim().ToUpperInvariant();
            var beneficiario = await db.Beneficiarios
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Carteira == carteira, ct);

            if (beneficiario is null)
            {
                beneficiario = Beneficiario.Create(tenantId, primeiraLinha.Codigo, primeiraLinha.Beneficiario);
                db.Beneficiarios.Add(beneficiario);
                await db.SaveChangesAsync(ct);
                beneficiariosCriados++;
            }

            var numeroGuia = grupo.Key.Guia;
            var guia = await db.Guias
                .FirstOrDefaultAsync(g =>
                    g.TenantId == tenantId &&
                    g.PrestadorId == prestadorId &&
                    g.NumeroGuia == numeroGuia, ct);

            var guiaCriada = guia is null;
            if (guia is null)
            {
                guia = Guia.Create(
                    tenantId, prestadorId, operadoraId,
                    beneficiario.Id, numeroGuia, grupo.Key.DataServico,
                    false, string.Empty, primeiraLinha.LocalAtendimento);
                db.Guias.Add(guia);
                await db.SaveChangesAsync(ct);
                guiasCriadas++;
            }
            else
            {
                if (string.IsNullOrEmpty(guia.LocalAtendimento) &&
                    !string.IsNullOrWhiteSpace(primeiraLinha.LocalAtendimento))
                {
                    guia.AtualizarLocalAtendimento(primeiraLinha.LocalAtendimento);
                    await db.SaveChangesAsync(ct);
                }

                guiasAtualizadas++;
            }

            var itensGrupo = new List<ItemGuia>();

            foreach (var linha in grupo)
            {
                if (string.IsNullOrWhiteSpace(linha.Funcao))
                {
                    itensIgnorados++;
                    continue;
                }

                var posicao = MapearFuncao(linha.Funcao);
                if (posicao is null)
                {
                    itensIgnorados++;
                    if (!string.IsNullOrWhiteSpace(linha.Funcao))
                    {
                        funcoesIgnoradas.Add(linha.Funcao.Trim());
                    }

                    continue;
                }

                var proc = await db.Procedimentos
                    .FirstOrDefaultAsync(p =>
                        p.TenantId == tenantId && p.CodigoTuss == linha.CodigoProcedimento, ct);

                if (proc is null)
                {
                    erros.Add(new ErroImportacao(linha.Linha,
                        $"Procedimento TUSS '{linha.CodigoProcedimento}' não encontrado no catálogo."));
                    continue;
                }

                var percentualOrdem = MapearPercentualOrdem(linha.PercentVia);
                var acomodacao = MapearAcomodacao(linha.Acomodacao);
                var ehUrgencia = MapearUrgencia(linha.Acrescimo);

                var itemExistente = await db.ItensGuia
                    .FirstOrDefaultAsync(i =>
                        i.GuiaId == guia.Id &&
                        i.ProcedimentoId == proc.Id &&
                        i.PosicaoExecutor == posicao.Value, ct);

                if (itemExistente is not null)
                {
                    itemExistente.Atualizar(percentualOrdem, acomodacao, ehUrgencia);
                    await db.SaveChangesAsync(ct);
                    itensAtualizados++;
                    itensGrupo.Add(itemExistente);
                }
                else
                {
                    var novoItem = ItemGuia.Create(
                        guia.Id, proc.Id, posicao.Value,
                        percentualOrdem, ViaAcesso.NaoAplicavel,
                        acomodacao, ehUrgencia, null);
                    db.ItensGuia.Add(novoItem);
                    await db.SaveChangesAsync(ct);
                    itensCriados++;
                    itensGrupo.Add(novoItem);
                }

                var itemGuia = itensGrupo[^1];
                itemGuia.SetValorLiquidado(linha.Total);
                itemGuia.SetMotivoGlosa(linha.CodGlosa);
                await db.SaveChangesAsync(ct);
            }

            if (guiaCriada || itensGrupo.Count > 0)
            {
                todasGuias.Add((guia, operadora, itensGrupo));
            }
        }

        foreach (var funcao in funcoesIgnoradas)
        {
            alertas.Add(new AlertaImportacao(0, $"Função '{funcao}' não reconhecida — itens ignorados."));
        }

        foreach (var (guia, op, itens) in todasGuias)
        {
            if (itens.Count == 0)
            {
                continue;
            }

            await ExecutarCalculoAsync(guia, op, itens, ct);

            var todosItens = await db.ItensGuia
                .Where(i => i.GuiaId == guia.Id)
                .ToListAsync(ct);

            if (todosItens.All(i => i.ValorLiquidado.HasValue))
            {
                guia.Liquidar();
                await db.SaveChangesAsync(ct);
            }
        }

        return Result<ImportacaoResultado>.Ok(new ImportacaoResultado(
            identificadorPagamento, false,
            guiasCriadas, guiasAtualizadas,
            itensCriados, itensAtualizados, itensIgnorados,
            beneficiariosCriados,
            0, 0,
            erros, alertas));
    }

    private static Result<IReadOnlyList<LinhaCSV>> ParsearCsv(StreamReader reader, out string identificadorPagamento)
    {
        var primeiraLinha = reader.ReadLine()?.Trim() ?? string.Empty;
        var colonIdx = primeiraLinha.LastIndexOf(':');
        identificadorPagamento = colonIdx >= 0
            ? primeiraLinha[(colonIdx + 1)..].Trim()
            : primeiraLinha;

        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return Result<IReadOnlyList<LinhaCSV>>.Fail(
                new ValidationError("CSV inválido: header ausente."));
        }

        var headers = headerLine.Split(';').Select(h => h.Trim().ToUpperInvariant()).ToArray();

        foreach (var required in _requiredHeaders)
        {
            if (!headers.Contains(required.ToUpperInvariant()))
            {
                return Result<IReadOnlyList<LinhaCSV>>.Fail(
                    new ValidationError($"CSV inválido: coluna obrigatória '{required}' não encontrada no header."));
            }
        }

        var idx = BuildIndexMap(headers);

        var linhas = new List<LinhaCSV>();
        var numeroLinha = 2;

        string? dataLine;
        while ((dataLine = reader.ReadLine()) is not null)
        {
            numeroLinha++;
            if (string.IsNullOrWhiteSpace(dataLine))
            {
                continue;
            }

            var cols = dataLine.Split(';');
            if (cols.Length < headers.Length)
            {
                Array.Resize(ref cols, headers.Length);
            }

            static string Col(string[] c, Dictionary<string, int> map, string key) =>
                map.TryGetValue(key, out var i) && i < c.Length ? c[i].Trim() : string.Empty;

            var honorario = ParseDecimal(Col(cols, idx, "HONORARIO"));
            var total = ParseDecimal(Col(cols, idx, "TOTAL"));
            var percentVia = ParseDecimal(Col(cols, idx, "% VIA"));

            if (!honorario.HasValue || !total.HasValue || !percentVia.HasValue)
            {
                continue;
            }

            var dataServicoStr = Col(cols, idx, "DATA SERVICO");
            if (!DateOnly.TryParseExact(dataServicoStr, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataServico) &&
                !DateOnly.TryParseExact(dataServicoStr, "dd/MM/yy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dataServico))
            {
                dataServico = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            linhas.Add(new LinhaCSV(
                Linha: numeroLinha,
                Guia: Col(cols, idx, "GUIA"),
                Codigo: Col(cols, idx, "CODIGO"),
                Beneficiario: Col(cols, idx, "BENEFICIARIO"),
                DataServico: dataServico,
                CodigoProcedimento: Col(cols, idx, "CODIGO PROCEDIMENTO"),
                NomeProcedimento: Col(cols, idx, "NOME PROCEDIMENTO"),
                Funcao: Col(cols, idx, "FUNCAO"),
                ExecutanteServico: Col(cols, idx, "EXECUTANTE DO SERVICO"),
                PercentVia: percentVia.Value,
                Acomodacao: Col(cols, idx, "ACOMODACAO"),
                Acrescimo: Col(cols, idx, "ACRESCIMO"),
                QtdePaga: int.TryParse(Col(cols, idx, "QTDE PAGA"), out var q) ? q : 0,
                Honorario: honorario.Value,
                Glosa: ParseDecimal(Col(cols, idx, "GLOSA")) ?? 0m,
                CodGlosa: Col(cols, idx, "COD_GLOSA"),
                Total: total.Value,
                LocalAtendimento: Col(cols, idx, "LOCAL ATENDIMENTO")));
        }

        return Result<IReadOnlyList<LinhaCSV>>.Ok(linhas);
    }

    private static Dictionary<string, int> BuildIndexMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            map[headers[i]] = i;
        }

        return map;
    }

    private static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static PosicaoExecutor? MapearFuncao(string funcao) =>
        funcao.Trim().ToUpperInvariant() switch
        {
            "CIRURGIAO" or "CIRURGIÃO" => PosicaoExecutor.Cirurgiao,
            "HONORARIO PRINC." or "HONORÁRIO PRINC." or
                "HONORARIO PRINCIPAL" or "HONORÁRIO PRINCIPAL" => PosicaoExecutor.Cirurgiao,
            "PRIMEIRO AUXILIAR" or "1 AUXILIAR" or "1º AUXILIAR" => PosicaoExecutor.PrimeiroAuxiliar,
            "SEGUNDO AUXILIAR" or "2 AUXILIAR" or "2º AUXILIAR" => PosicaoExecutor.SegundoAuxiliar,
            "TERCEIRO AUXILIAR" or "3 AUXILIAR" or "3º AUXILIAR" => PosicaoExecutor.TerceiroAuxiliar,
            "ANESTESISTA" or "ANESTESIA" => PosicaoExecutor.Anestesista,
            "CLINICO ASSISTENTE" or "CLÍNICO ASSISTENTE" => PosicaoExecutor.ClinicoAssistente,
            _ => null,
        };

    private static Acomodacao MapearAcomodacao(string acomodacao) =>
        acomodacao.Trim().ToUpperInvariant() switch
        {
            "APARTAMENTO" => Acomodacao.Apartamento,
            "AMBULATORIAL" => Acomodacao.Ambulatorial,
            _ => Acomodacao.Enfermaria,
        };

    private static bool MapearUrgencia(string acrescimo)
    {
        if (string.IsNullOrWhiteSpace(acrescimo))
        {
            return false;
        }

        var normalized = acrescimo.Trim().TrimEnd('%')
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var v) && v > 0m;
    }

    private static decimal MapearPercentualOrdem(decimal percentVia) =>
        percentVia / 100m;

    private async Task ExecutarCalculoAsync(
        Guia guia, Operadora operadora, List<ItemGuia> itens, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId!.Value;
        var ruleSet = factory.Criar(operadora.TipoRuleSet);

        var ctx = new ApurarGuiaContext(
            tenantId, guia.PrestadorId, guia.OperadoraId,
            itens.Select(i => new ApurarItemInput(
                i.Id, i.ProcedimentoId, i.PosicaoExecutor,
                i.ViaAcesso, i.Acomodacao,
                i.EhUrgencia, i.TempoAnestesicoMin))
            .ToList());

        var resultados = await ruleSet.ApurarAsync(ctx, ct);

        var calculo = Calculo.Create(tenantId, guia.Id);
        db.Calculos.Add(calculo);

        var seq = 0;
        foreach (var resultado in resultados)
        {
            if (resultado.Situacao != SituacaoApuracao.Calculado)
            {
                continue;
            }

            var item = itens.FirstOrDefault(i => i.Id == resultado.ItemGuiaId);
            if (item is null)
            {
                continue;
            }

            item.SetValorApurado(resultado.ValorApurado);

            foreach (var passo in resultado.Passos)
            {
                db.PassosCalculo.Add(PassoCalculo.Create(
                    calculo.Id, resultado.ItemGuiaId, ++seq,
                    passo.Regra, passo.Fator, passo.ValorResultante));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record LinhaCSV(
        int Linha,
        string Guia,
        string Codigo,
        string Beneficiario,
        DateOnly DataServico,
        string CodigoProcedimento,
        string NomeProcedimento,
        string Funcao,
        string ExecutanteServico,
        decimal PercentVia,
        string Acomodacao,
        string Acrescimo,
        int QtdePaga,
        decimal Honorario,
        decimal Glosa,
        string CodGlosa,
        decimal Total,
        string LocalAtendimento);
}
