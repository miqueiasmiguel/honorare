using App.Catalog;
using App.Data;
using App.Faturamento.Motor;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento;

internal sealed record CriarGuiaCommand(
    Guid PrestadorId, Guid OperadoraId, Guid? BeneficiarioId,
    string NumeroGuia, DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens, string LocalAtendimento = "");

internal sealed record CriarItemGuiaCommand(
    Guid ProcedimentoId, PosicaoExecutor PosicaoExecutor,
    ViaAcesso ViaAcesso, Acomodacao Acomodacao,
    bool EhUrgencia, decimal? ValorApurado, int? TempoAnestesicoMin = null);

internal sealed record AtualizarGuiaCommand(
    Guid OperadoraId, Guid? BeneficiarioId, string NumeroGuia,
    DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens, string LocalAtendimento = "");

internal sealed record AtualizarObservacaoCommand(string Observacao);

internal sealed record AtualizarValorApuradoItemCommand(decimal? ValorApurado);

internal enum GuiaOrdenacao
{
    DataAtendimento,
    NumeroGuia,
    Prestador,
    Operadora,
    Beneficiario,
    Situacao,
}

internal sealed record ListarGuiasQuery(
    Guid? PrestadorId, Guid? OperadoraId,
    DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, string? NumeroGuia, string? Beneficiario,
    bool? SemRecurso, bool? SomenteComGlosa,
    int Pagina, int ItensPorPagina,
    GuiaOrdenacao OrdenarPor = GuiaOrdenacao.DataAtendimento, bool Descendente = true);

internal sealed record GuiaDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string NumeroGuia,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, string LocalAtendimento, int TotalItens,
    DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
    bool NaoRecorrivel, bool MistaComNaoRecorriveis);

internal sealed record ItemGuiaDto(
    Guid Id, Guid ProcedimentoId, string CodigoTuss, string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor, decimal PercentualOrdem,
    ViaAcesso ViaAcesso, Acomodacao Acomodacao, bool EhUrgencia,
    decimal? ValorApurado, decimal? ValorLiquidado, string? MotivoGlosa);

internal sealed record GuiaDetalheDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string NumeroGuia,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, string LocalAtendimento, int TotalItens, DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
    IReadOnlyList<ItemGuiaDto> Itens);

internal sealed record ListarGuiasResult(
    IReadOnlyList<GuiaDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record PassoCalculoDto(string Regra, decimal Fator, decimal ValorResultante);

internal sealed record ItemCalculoDto(
    Guid ItemGuiaId,
    string CodigoTuss,
    string DescricaoProcedimento,
    string Situacao,
    decimal? ValorApurado,
    IReadOnlyList<PassoCalculoDto> Passos);

internal sealed record GuiaCalculoDto(
    Guid GuiaId,
    bool EhPacote,
    DateTimeOffset? RealizadoEm,
    IReadOnlyList<ItemCalculoDto> Itens);

internal sealed class GuiaService(AppDbContext db, ICurrentUser currentUser, PricingRuleSetFactory factory)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly PricingRuleSetFactory _factory = factory;

    internal async Task<Result<GuiaDetalheDto>> CriarAsync(
        CriarGuiaCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Itens.Count == 0)
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("A guia deve ter ao menos um item."));
        }

        if (cmd.EhPacote && cmd.Itens.Any(i => i.ValorApurado is null))
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("Itens de guia pacote devem ter ValorApurado preenchido."));
        }

        var tenantId = _currentUser.TenantId!.Value;

        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == cmd.PrestadorId, ct);
        if (prestador is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        if (cmd.BeneficiarioId.HasValue)
        {
            var beneficiario = await _db.Beneficiarios.FirstOrDefaultAsync(b => b.Id == cmd.BeneficiarioId.Value, ct);
            if (beneficiario is null)
            {
                return Result<GuiaDetalheDto>.Fail(new NotFoundError("Beneficiário não encontrado."));
            }
        }

        if (!cmd.EhPacote)
        {
            var erroCalculo = await ValidarCalculoViavelAsync(
                cmd.PrestadorId, cmd.OperadoraId, operadora, cmd.Itens, tenantId, ct);
            if (erroCalculo is not null)
            {
                return Result<GuiaDetalheDto>.Fail(new ValidationError(erroCalculo));
            }
        }

        var guia = Guia.Create(
            tenantId, cmd.PrestadorId, cmd.OperadoraId, cmd.BeneficiarioId,
            cmd.NumeroGuia, cmd.DataAtendimento, cmd.EhPacote, cmd.Observacao, cmd.LocalAtendimento);

        _db.Guias.Add(guia);

        var itens = new List<ItemGuia>(cmd.Itens.Count);
        foreach (var itemCmd in cmd.Itens)
        {
            var item = ItemGuia.Create(
                guia.Id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
                itemCmd.ViaAcesso, itemCmd.Acomodacao,
                itemCmd.EhUrgencia, itemCmd.ValorApurado, itemCmd.TempoAnestesicoMin);
            _db.ItensGuia.Add(item);
            itens.Add(item);
        }

        await _db.SaveChangesAsync(ct);

        if (!guia.EhPacote)
        {
            await ExecutarCalculoAsync(guia, operadora, itens, ct);
        }

        return await ObterDetalheDtoInternalAsync(guia.Id, ct);
    }

    internal async Task<Result<GuiaDetalheDto>> AdicionarItemAsync(
        Guid guiaId, CriarItemGuiaCommand itemCmd, CancellationToken ct = default)
    {
        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct);
        if (guia is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == guia.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        if (guia.EhPacote)
        {
            if (itemCmd.ValorApurado is null)
            {
                return Result<GuiaDetalheDto>.Fail(
                    new ValidationError("Itens de guia pacote devem ter ValorApurado preenchido."));
            }
        }
        else
        {
            var erro = await ValidarCalculoViavelAsync(
                guia.PrestadorId, guia.OperadoraId, operadora, [itemCmd], _currentUser.TenantId!.Value, ct);
            if (erro is not null)
            {
                return Result<GuiaDetalheDto>.Fail(new ValidationError(erro));
            }
        }

        var item = ItemGuia.Create(
            guia.Id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
            itemCmd.ViaAcesso, itemCmd.Acomodacao,
            itemCmd.EhUrgencia, guia.EhPacote ? itemCmd.ValorApurado : null, itemCmd.TempoAnestesicoMin);
        _db.ItensGuia.Add(item);
        await _db.SaveChangesAsync(ct);

        if (!guia.EhPacote && operadora.TipoRuleSet != TipoRuleSet.Nulo)
        {
            await _db.Calculos.Where(c => c.GuiaId == guiaId).ExecuteDeleteAsync(ct);
            var itens = await _db.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync(ct);
            foreach (var i in itens)
            {
                i.SetValorApurado(null);
            }

            await _db.SaveChangesAsync(ct);
            await ExecutarCalculoAsync(guia, operadora, itens, ct);
        }

        return await ObterDetalheDtoInternalAsync(guiaId, ct);
    }

    internal async Task<ListarGuiasResult> ListarAsync(
        ListarGuiasQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Guias.AsQueryable();

        if (query.PrestadorId.HasValue)
        {
            baseQuery = baseQuery.Where(g => g.PrestadorId == query.PrestadorId.Value);
        }

        if (query.OperadoraId.HasValue)
        {
            baseQuery = baseQuery.Where(g => g.OperadoraId == query.OperadoraId.Value);
        }

        if (query.DataInicio.HasValue)
        {
            baseQuery = baseQuery.Where(g => g.DataAtendimento >= query.DataInicio.Value);
        }

        if (query.DataFim.HasValue)
        {
            baseQuery = baseQuery.Where(g => g.DataAtendimento <= query.DataFim.Value);
        }

        if (query.Situacao.HasValue)
        {
            baseQuery = baseQuery.Where(g => g.Situacao == query.Situacao.Value);
        }

        if (query.SemRecurso == true)
        {
            baseQuery = baseQuery.Where(g => g.RecursoId == null);
        }

        if (query.SomenteComGlosa == true)
        {
            baseQuery = baseQuery.Where(g => _db.ItensGuia.Any(i =>
                i.GuiaId == g.Id &&
                i.ValorApurado.HasValue && i.ValorLiquidado.HasValue &&
                i.ValorApurado > i.ValorLiquidado));
        }

        var q = from g in baseQuery
                join pr in _db.Prestadores on g.PrestadorId equals pr.Id
                join op in _db.Operadoras on g.OperadoraId equals op.Id
                join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
                from b in bs.DefaultIfEmpty()
                select new
                {
                    g.Id,
                    g.PrestadorId,
                    PrestadorNome = pr.Nome,
                    g.OperadoraId,
                    OperadoraNome = op.Nome,
                    g.BeneficiarioId,
                    BeneficiarioNome = (string?)b.Nome,
                    BeneficiarioCarteira = (string?)b.Carteira,
                    g.NumeroGuia,
                    g.DataAtendimento,
                    g.Situacao,
                    g.EhPacote,
                    g.Observacao,
                    g.LocalAtendimento,
                    g.CriadoEm,
                    g.AtualizadoEm,
                };

        if (!string.IsNullOrWhiteSpace(query.NumeroGuia))
        {
            q = q.Where(x => x.NumeroGuia.Contains(query.NumeroGuia));
        }

        if (!string.IsNullOrWhiteSpace(query.Beneficiario))
        {
            q = q.Where(x => x.BeneficiarioNome != null &&
                              x.BeneficiarioNome.Contains(query.Beneficiario));
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        // Ordenação dinâmica. Desempate estável por CriadoEm desc em todos os casos —
        // sem ele a paginação fica não-determinística quando a chave primária empata.
        var ordenado = query.OrdenarPor switch
        {
            GuiaOrdenacao.NumeroGuia => query.Descendente
                ? q.OrderByDescending(x => x.NumeroGuia)
                : q.OrderBy(x => x.NumeroGuia),
            GuiaOrdenacao.Prestador => query.Descendente
                ? q.OrderByDescending(x => x.PrestadorNome)
                : q.OrderBy(x => x.PrestadorNome),
            GuiaOrdenacao.Operadora => query.Descendente
                ? q.OrderByDescending(x => x.OperadoraNome)
                : q.OrderBy(x => x.OperadoraNome),
            GuiaOrdenacao.Beneficiario => query.Descendente
                ? q.OrderByDescending(x => x.BeneficiarioNome)
                : q.OrderBy(x => x.BeneficiarioNome),
            GuiaOrdenacao.Situacao => query.Descendente
                ? q.OrderByDescending(x => x.Situacao)
                : q.OrderBy(x => x.Situacao),
            _ => query.Descendente
                ? q.OrderByDescending(x => x.DataAtendimento)
                : q.OrderBy(x => x.DataAtendimento),
        };

        var pagina = await ordenado
            .ThenByDescending(x => x.CriadoEm)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var ids = pagina.Select(x => x.Id).ToList();
        var counts = await _db.ItensGuia
            .Where(i => ids.Contains(i.GuiaId))
            .GroupBy(i => i.GuiaId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId!.Value, ct);
        var codigos = tenant?.CodigosNaoRecorriveis ?? [];

        var countNrPorGuia = codigos.Count == 0
            ? new Dictionary<Guid, int>()
            : await (from i in _db.ItensGuia
                     join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
                     where ids.Contains(i.GuiaId) && codigos.Contains(p.CodigoTuss)
                     group i by i.GuiaId into g
                     select new { GuiaId = g.Key, Qtd = g.Count() })
                    .ToDictionaryAsync(x => x.GuiaId, x => x.Qtd, ct);

        var totalmenteNaoRecorrivelIds = countNrPorGuia.Keys
            .Where(id => countNrPorGuia[id] == counts.GetValueOrDefault(id, 0))
            .ToHashSet();

        var mistaIds = countNrPorGuia.Keys
            .Except(totalmenteNaoRecorrivelIds)
            .ToHashSet();

        var itens = pagina.Select(x => new GuiaDto(
            x.Id, x.PrestadorId, x.PrestadorNome,
            x.OperadoraId, x.OperadoraNome,
            x.BeneficiarioId, x.BeneficiarioNome, x.BeneficiarioCarteira,
            x.NumeroGuia, x.DataAtendimento, x.Situacao, x.EhPacote,
            x.Observacao, x.LocalAtendimento,
            counts.GetValueOrDefault(x.Id, 0),
            x.CriadoEm, x.AtualizadoEm,
            totalmenteNaoRecorrivelIds.Contains(x.Id),
            mistaIds.Contains(x.Id))).ToList();

        return new ListarGuiasResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<GuiaDetalheDto>> ObterPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        return await ObterDetalheDtoInternalAsync(id, ct);
    }

    internal async Task<Result<GuiaDetalheDto>> AtualizarAsync(
        Guid id, AtualizarGuiaCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Itens.Count == 0)
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("A guia deve ter ao menos um item."));
        }

        if (cmd.EhPacote && cmd.Itens.Any(i => i.ValorApurado is null))
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("Itens de guia pacote devem ter ValorApurado preenchido."));
        }

        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guia is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        if (!cmd.EhPacote)
        {
            var erroCalculo = await ValidarCalculoViavelAsync(
                guia.PrestadorId, cmd.OperadoraId, operadora, cmd.Itens,
                _currentUser.TenantId!.Value, ct);
            if (erroCalculo is not null)
            {
                return Result<GuiaDetalheDto>.Fail(new ValidationError(erroCalculo));
            }
        }

        // Excluir Calculo anterior (cascade apaga PassoCalculo) antes de deletar ItensGuia
        await _db.Calculos.Where(c => c.GuiaId == id).ExecuteDeleteAsync(ct);
        await _db.ItensGuia.Where(i => i.GuiaId == id).ExecuteDeleteAsync(ct);

        guia.Atualizar(
            cmd.OperadoraId, cmd.BeneficiarioId,
            cmd.NumeroGuia.Trim(), cmd.DataAtendimento, cmd.EhPacote, cmd.Observacao.Trim(),
            cmd.LocalAtendimento);

        var itens = new List<ItemGuia>(cmd.Itens.Count);
        foreach (var itemCmd in cmd.Itens)
        {
            var item = ItemGuia.Create(
                id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
                itemCmd.ViaAcesso, itemCmd.Acomodacao,
                itemCmd.EhUrgencia, itemCmd.ValorApurado, itemCmd.TempoAnestesicoMin);
            _db.ItensGuia.Add(item);
            itens.Add(item);
        }

        await _db.SaveChangesAsync(ct);

        if (!guia.EhPacote)
        {
            await ExecutarCalculoAsync(guia, operadora, itens, ct);
        }

        return await ObterDetalheDtoInternalAsync(id, ct);
    }

    internal async Task<Result<GuiaCalculoDto>> ObterCalculoAsync(
        Guid guiaId, CancellationToken ct = default)
    {
        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct);
        if (guia is null)
        {
            return Result<GuiaCalculoDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        var itensComProc = await (
            from i in _db.ItensGuia
            join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
            where i.GuiaId == guiaId
            select new
            {
                i.Id,
                i.ValorApurado,
                p.CodigoTuss,
                p.Descricao,
                i.ProcedimentoId,
                i.PosicaoExecutor,
                i.PercentualOrdem,
                i.ViaAcesso,
                i.Acomodacao,
                i.EhUrgencia,
                i.TempoAnestesicoMin,
            }
        ).ToListAsync(ct);

        if (guia.EhPacote)
        {
            var pacoteItens = itensComProc
                .Select(i => new ItemCalculoDto(i.Id, i.CodigoTuss, i.Descricao, "Pacote", i.ValorApurado, []))
                .ToList();
            return Result<GuiaCalculoDto>.Ok(new GuiaCalculoDto(guiaId, true, null, pacoteItens));
        }

        var calculo = await _db.Calculos.FirstOrDefaultAsync(c => c.GuiaId == guiaId, ct);

        Dictionary<Guid, List<PassoCalculoDto>> passosPorItem;
        if (calculo is null)
        {
            passosPorItem = [];
        }
        else
        {
            var rawPassos = await _db.PassosCalculo
                .Where(p => p.CalculoId == calculo.Id)
                .OrderBy(p => p.Sequencia)
                .Select(p => new { p.ItemGuiaId, p.Regra, p.Fator, p.ValorResultante })
                .ToListAsync(ct);

            passosPorItem = rawPassos
                .GroupBy(p => p.ItemGuiaId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new PassoCalculoDto(p.Regra, p.Fator, p.ValorResultante)).ToList());
        }

        // Re-run the rule set for non-Calculado items to surface the real situação
        var situacaoViva = new Dictionary<Guid, string>();
        var itemsSemCalculo = itensComProc.Where(i => !i.ValorApurado.HasValue).ToList();
        if (itemsSemCalculo.Count > 0)
        {
            var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == guia.OperadoraId, ct);
            if (operadora is not null)
            {
                var ruleSet = _factory.Criar(operadora.TipoRuleSet);
                var diagnosticCtx = new ApurarGuiaContext(
                    _currentUser.TenantId!.Value, guia.PrestadorId, guia.OperadoraId,
                    itemsSemCalculo.Select(i => new ApurarItemInput(
                        i.Id, i.ProcedimentoId, i.PosicaoExecutor,
                        i.ViaAcesso, i.Acomodacao,
                        i.EhUrgencia, i.TempoAnestesicoMin)).ToList());
                var resultados = await ruleSet.ApurarAsync(diagnosticCtx, ct);
                foreach (var r in resultados)
                {
                    situacaoViva[r.ItemGuiaId] = r.Situacao.ToString();
                }
            }
        }

        var itensDtos = itensComProc.Select(i =>
        {
            var passos = passosPorItem.TryGetValue(i.Id, out var p)
                ? (IReadOnlyList<PassoCalculoDto>)p
                : [];
            var situacao = i.ValorApurado.HasValue
                ? "Calculado"
                : situacaoViva.TryGetValue(i.Id, out var s) ? s : "NaoCalculado";
            return new ItemCalculoDto(i.Id, i.CodigoTuss, i.Descricao, situacao, i.ValorApurado, passos);
        }).ToList();

        return Result<GuiaCalculoDto>.Ok(
            new GuiaCalculoDto(guiaId, false, calculo?.RealizadoEm, itensDtos));
    }

    internal async Task<Result<GuiaDetalheDto>> RecalcularAsync(
        Guid guiaId, CancellationToken ct = default)
    {
        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct);
        if (guia is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        if (guia.EhPacote)
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("Guias pacote não possuem cálculo automático."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == guia.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        await _db.Calculos.Where(c => c.GuiaId == guiaId).ExecuteDeleteAsync(ct);

        var itens = await _db.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync(ct);
        foreach (var item in itens)
        {
            item.SetValorApurado(null);
        }

        await _db.SaveChangesAsync(ct);
        await ExecutarCalculoAsync(guia, operadora, itens, ct);

        return await ObterDetalheDtoInternalAsync(guiaId, ct);
    }

    internal async Task<Result<GuiaDetalheDto>> AtualizarObservacaoAsync(
        Guid id, AtualizarObservacaoCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Observacao.Length > 2000)
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("Observação não pode exceder 2000 caracteres."));
        }

        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guia is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        guia.AtualizarObservacao(cmd.Observacao);
        await _db.SaveChangesAsync(ct);

        return await ObterDetalheDtoInternalAsync(guia.Id, ct);
    }

    internal async Task<Result<ItemGuiaDto>> AtualizarValorApuradoItemAsync(
        Guid guiaId, Guid itemId, AtualizarValorApuradoItemCommand cmd,
        CancellationToken ct = default)
    {
        if (cmd.ValorApurado is { } valor && valor <= 0)
        {
            return Result<ItemGuiaDto>.Fail(new ValidationError("ValorApurado deve ser maior que zero."));
        }

        var item = await _db.ItensGuia
            .Where(i => i.Id == itemId && i.GuiaId == guiaId)
            .FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return Result<ItemGuiaDto>.Fail(new NotFoundError("Item não encontrado."));
        }

        item.SetValorApurado(cmd.ValorApurado);
        await _db.SaveChangesAsync(ct);

        return Result<ItemGuiaDto>.Ok(await MapItemToDtoAsync(itemId, ct));
    }

    internal async Task<Result<ItemGuiaDto>> AtualizarPagamentoItemAsync(
        Guid guiaId, Guid itemId,
        decimal? valorLiquidado, string? motivoGlosa,
        CancellationToken ct = default)
    {
        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct);
        if (guia is null)
        {
            return Result<ItemGuiaDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        var todosItens = await _db.ItensGuia
            .Where(i => i.GuiaId == guiaId)
            .ToListAsync(ct);

        var item = todosItens.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return Result<ItemGuiaDto>.Fail(new NotFoundError("Item não encontrado."));
        }

        item.SetValorLiquidado(valorLiquidado);
        item.SetMotivoGlosa(motivoGlosa);

        if (valorLiquidado is null)
        {
            if (guia.Situacao == SituacaoGuia.Liquidada)
            {
                guia.ReverterParaApresentada();
            }
        }
        else if (todosItens.All(i => i.ValorLiquidado is not null)
            && guia.Situacao != SituacaoGuia.EmRecurso)
        {
            guia.Liquidar();
        }

        await _db.SaveChangesAsync(ct);

        return Result<ItemGuiaDto>.Ok(await MapItemToDtoAsync(itemId, ct));
    }

    internal async Task<Result> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guia is null)
        {
            return Result.Fail(new NotFoundError("Guia não encontrada."));
        }

        // Marcar Calculos como Deleted no tracker (cascade DB apaga PassoCalculo)
        // antes de Remove(guia) para não violar a FK Restrict de Calculo->Guia
        var calculos = await _db.Calculos.Where(c => c.GuiaId == id).ToListAsync(ct);
        _db.Calculos.RemoveRange(calculos);

        _db.Guias.Remove(guia);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task ExecutarCalculoAsync(
        Guia guia, Operadora operadora, List<ItemGuia> itens, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId!.Value;
        var ruleSet = _factory.Criar(operadora.TipoRuleSet);

        var ctx = new ApurarGuiaContext(tenantId, guia.PrestadorId, guia.OperadoraId,
            itens.Select(i => new ApurarItemInput(
                i.Id, i.ProcedimentoId, i.PosicaoExecutor,
                i.ViaAcesso, i.Acomodacao, i.EhUrgencia,
                i.TempoAnestesicoMin))
            .ToList());

        var resultados = await ruleSet.ApurarAsync(ctx, ct);

        var calculo = Calculo.Create(tenantId, guia.Id);
        _db.Calculos.Add(calculo);

        var seq = 0;
        foreach (var resultado in resultados)
        {
            if (resultado.Situacao != SituacaoApuracao.Calculado)
            {
                continue;
            }

            var item = itens.First(i => i.Id == resultado.ItemGuiaId);
            item.SetValorApurado(resultado.ValorApurado);
            item.SetPercentualOrdem(resultado.PercentualOrdem);

            foreach (var passo in resultado.Passos)
            {
                _db.PassosCalculo.Add(PassoCalculo.Create(
                    calculo.Id, resultado.ItemGuiaId, ++seq,
                    passo.Regra, passo.Fator, passo.ValorResultante));
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string?> ValidarCalculoViavelAsync(
        Guid prestadorId, Guid operadoraId, Operadora operadora,
        IReadOnlyList<CriarItemGuiaCommand> itens, Guid tenantId, CancellationToken ct)
    {
        if (operadora.TipoRuleSet == TipoRuleSet.Nulo)
        {
            return null;
        }

        var ruleSet = _factory.Criar(operadora.TipoRuleSet);
        var tempItens = itens
            .Select(i => new ApurarItemInput(
                Guid.NewGuid(), i.ProcedimentoId, i.PosicaoExecutor,
                i.ViaAcesso, i.Acomodacao,
                i.EhUrgencia, i.TempoAnestesicoMin))
            .ToList();

        var ctx = new ApurarGuiaContext(tenantId, prestadorId, operadoraId, tempItens);
        var resultados = await ruleSet.ApurarAsync(ctx, ct);

        var falhas = resultados.Where(r => r.Situacao != SituacaoApuracao.Calculado).ToList();
        if (falhas.Count == 0)
        {
            return null;
        }

        var situacoes = falhas
            .GroupBy(r => r.Situacao)
            .Select(g => $"{g.Count()} item(ns) com situação '{g.Key}'")
            .ToList();
        return $"Não é possível criar a guia: {string.Join("; ", situacoes)}. " +
               "Verifique tabelas de procedimento e portes anestésicos.";
    }

    private async Task<Result<GuiaDetalheDto>> ObterDetalheDtoInternalAsync(
        Guid guiaId, CancellationToken ct)
    {
        var header = await (from g in _db.Guias
                            join pr in _db.Prestadores on g.PrestadorId equals pr.Id
                            join op in _db.Operadoras on g.OperadoraId equals op.Id
                            join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
                            from b in bs.DefaultIfEmpty()
                            where g.Id == guiaId
                            select new
                            {
                                g.Id,
                                g.PrestadorId,
                                PrestadorNome = pr.Nome,
                                g.OperadoraId,
                                OperadoraNome = op.Nome,
                                g.BeneficiarioId,
                                BeneficiarioNome = (string?)b.Nome,
                                BeneficiarioCarteira = (string?)b.Carteira,
                                g.NumeroGuia,
                                g.DataAtendimento,
                                g.Situacao,
                                g.EhPacote,
                                g.Observacao,
                                g.LocalAtendimento,
                                g.CriadoEm,
                                g.AtualizadoEm,
                            })
                           .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
        }

        var itens = await (from i in _db.ItensGuia
                           join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
                           where i.GuiaId == guiaId
                           select new ItemGuiaDto(
                               i.Id, i.ProcedimentoId, p.CodigoTuss, p.Descricao,
                               i.PosicaoExecutor, i.PercentualOrdem,
                               i.ViaAcesso, i.Acomodacao,
                               i.EhUrgencia, i.ValorApurado, i.ValorLiquidado, i.MotivoGlosa))
                          .ToListAsync(ct);

        return Result<GuiaDetalheDto>.Ok(new GuiaDetalheDto(
            header.Id, header.PrestadorId, header.PrestadorNome,
            header.OperadoraId, header.OperadoraNome,
            header.BeneficiarioId, header.BeneficiarioNome, header.BeneficiarioCarteira,
            header.NumeroGuia, header.DataAtendimento, header.Situacao, header.EhPacote,
            header.Observacao, header.LocalAtendimento, itens.Count, header.CriadoEm, header.AtualizadoEm,
            itens));
    }

    private async Task<ItemGuiaDto> MapItemToDtoAsync(Guid itemId, CancellationToken ct)
    {
        return await (from i in _db.ItensGuia
                      join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
                      where i.Id == itemId
                      select new ItemGuiaDto(
                          i.Id, i.ProcedimentoId, p.CodigoTuss, p.Descricao,
                          i.PosicaoExecutor, i.PercentualOrdem,
                          i.ViaAcesso, i.Acomodacao,
                          i.EhUrgencia, i.ValorApurado, i.ValorLiquidado, i.MotivoGlosa))
                     .FirstAsync(ct);
    }
}
