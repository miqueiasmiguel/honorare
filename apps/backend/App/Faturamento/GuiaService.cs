using App.Catalog;
using App.Data;
using App.Faturamento.Motor;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento;

internal sealed record CriarGuiaCommand(
    Guid PrestadorId, Guid OperadoraId, Guid? BeneficiarioId,
    string Senha, DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens);

internal sealed record CriarItemGuiaCommand(
    Guid ProcedimentoId, PosicaoExecutor PosicaoExecutor,
    OrdemProcedimento OrdemProcedimento, ViaAcesso ViaAcesso, Acomodacao Acomodacao,
    bool EhUrgencia, decimal? ValorApurado);

internal sealed record AtualizarGuiaCommand(
    Guid OperadoraId, Guid? BeneficiarioId, string Senha,
    DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens);

internal sealed record ListarGuiasQuery(
    Guid? PrestadorId, DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, int Pagina, int ItensPorPagina);

internal sealed record GuiaDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string Senha,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, int TotalItens, DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm);

internal sealed record ItemGuiaDto(
    Guid Id, Guid ProcedimentoId, string CodigoTuss, string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor, OrdemProcedimento OrdemProcedimento,
    ViaAcesso ViaAcesso, Acomodacao Acomodacao, bool EhUrgencia,
    decimal? ValorApurado, decimal? ValorLiquidado);

internal sealed record GuiaDetalheDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string Senha,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, int TotalItens, DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
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

        var guia = Guia.Create(
            tenantId, cmd.PrestadorId, cmd.OperadoraId, cmd.BeneficiarioId,
            cmd.Senha, cmd.DataAtendimento, cmd.EhPacote, cmd.Observacao);

        _db.Guias.Add(guia);

        var itens = new List<ItemGuia>(cmd.Itens.Count);
        foreach (var itemCmd in cmd.Itens)
        {
            var item = ItemGuia.Create(
                guia.Id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
                itemCmd.OrdemProcedimento, itemCmd.ViaAcesso, itemCmd.Acomodacao,
                itemCmd.EhUrgencia, itemCmd.ValorApurado);
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

    internal async Task<ListarGuiasResult> ListarAsync(
        ListarGuiasQuery query, CancellationToken ct = default)
    {
        var q = from g in _db.Guias
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
                    g.Senha,
                    g.DataAtendimento,
                    g.Situacao,
                    g.EhPacote,
                    g.Observacao,
                    g.CriadoEm,
                    g.AtualizadoEm,
                };

        if (query.PrestadorId.HasValue)
        {
            q = q.Where(x => x.PrestadorId == query.PrestadorId.Value);
        }

        if (query.DataInicio.HasValue)
        {
            q = q.Where(x => x.DataAtendimento >= query.DataInicio.Value);
        }

        if (query.DataFim.HasValue)
        {
            q = q.Where(x => x.DataAtendimento <= query.DataFim.Value);
        }

        if (query.Situacao.HasValue)
        {
            q = q.Where(x => x.Situacao == query.Situacao.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var pagina = await q
            .OrderByDescending(x => x.DataAtendimento)
            .ThenByDescending(x => x.CriadoEm)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var ids = pagina.Select(x => x.Id).ToList();
        var counts = await _db.ItensGuia
            .Where(i => ids.Contains(i.GuiaId))
            .GroupBy(i => i.GuiaId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        var itens = pagina.Select(x => new GuiaDto(
            x.Id, x.PrestadorId, x.PrestadorNome,
            x.OperadoraId, x.OperadoraNome,
            x.BeneficiarioId, x.BeneficiarioNome, x.BeneficiarioCarteira,
            x.Senha, x.DataAtendimento, x.Situacao, x.EhPacote,
            x.Observacao,
            counts.GetValueOrDefault(x.Id, 0),
            x.CriadoEm, x.AtualizadoEm)).ToList();

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

        // Excluir Calculo anterior (cascade apaga PassoCalculo) antes de deletar ItensGuia
        await _db.Calculos.Where(c => c.GuiaId == id).ExecuteDeleteAsync(ct);
        await _db.ItensGuia.Where(i => i.GuiaId == id).ExecuteDeleteAsync(ct);

        guia.Atualizar(
            cmd.OperadoraId, cmd.BeneficiarioId,
            cmd.Senha.Trim(), cmd.DataAtendimento, cmd.EhPacote, cmd.Observacao.Trim());

        var itens = new List<ItemGuia>(cmd.Itens.Count);
        foreach (var itemCmd in cmd.Itens)
        {
            var item = ItemGuia.Create(
                id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
                itemCmd.OrdemProcedimento, itemCmd.ViaAcesso, itemCmd.Acomodacao,
                itemCmd.EhUrgencia, itemCmd.ValorApurado);
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
            select new { i.Id, i.ValorApurado, p.CodigoTuss, p.Descricao }
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

        var itensDtos = itensComProc.Select(i =>
        {
            var passos = passosPorItem.TryGetValue(i.Id, out var p)
                ? (IReadOnlyList<PassoCalculoDto>)p
                : [];
            var situacao = i.ValorApurado.HasValue ? "Calculado" : "SemTabela";
            return new ItemCalculoDto(i.Id, i.CodigoTuss, i.Descricao, situacao, i.ValorApurado, passos);
        }).ToList();

        return Result<GuiaCalculoDto>.Ok(
            new GuiaCalculoDto(guiaId, false, calculo?.RealizadoEm, itensDtos));
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
                i.OrdemProcedimento, i.ViaAcesso, i.Acomodacao, i.EhUrgencia))
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

            foreach (var passo in resultado.Passos)
            {
                _db.PassosCalculo.Add(PassoCalculo.Create(
                    calculo.Id, resultado.ItemGuiaId, ++seq,
                    passo.Regra, passo.Fator, passo.ValorResultante));
            }
        }

        await _db.SaveChangesAsync(ct);
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
                                g.Senha,
                                g.DataAtendimento,
                                g.Situacao,
                                g.EhPacote,
                                g.Observacao,
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
                               i.PosicaoExecutor, i.OrdemProcedimento,
                               i.ViaAcesso, i.Acomodacao,
                               i.EhUrgencia, i.ValorApurado, i.ValorLiquidado))
                          .ToListAsync(ct);

        return Result<GuiaDetalheDto>.Ok(new GuiaDetalheDto(
            header.Id, header.PrestadorId, header.PrestadorNome,
            header.OperadoraId, header.OperadoraNome,
            header.BeneficiarioId, header.BeneficiarioNome, header.BeneficiarioCarteira,
            header.Senha, header.DataAtendimento, header.Situacao, header.EhPacote,
            header.Observacao, itens.Count, header.CriadoEm, header.AtualizadoEm,
            itens));
    }
}
