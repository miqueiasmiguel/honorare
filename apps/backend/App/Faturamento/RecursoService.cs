using App.Catalog;
using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento;

internal sealed record CriarRecursoCommand(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao);

internal sealed record AtualizarRecursoCommand(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao);

internal sealed record RecursoDto(
    Guid Id, Guid OperadoraId, string OperadoraNome,
    Guid PrestadorId, string PrestadorNome, string? PrestadorRegistroProfissional,
    string Numero, DateOnly DataEmissao, string? Observacao,
    int TotalGuias, DateTimeOffset CriadoEm);

internal sealed record ItemGuiaNoRecursoDto(
    Guid Id,
    string CodigoTuss,
    string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor,
    decimal PercentualOrdem,
    ViaAcesso ViaAcesso,
    Acomodacao Acomodacao,
    bool EhUrgencia,
    decimal? ValorApurado,
    decimal? ValorLiquidado);

internal sealed record GuiaNoRecursoDto(
    Guid Id, string NumeroGuia, DateOnly DataAtendimento,
    string? BeneficiarioNome, string? BeneficiarioCarteira,
    SituacaoGuia Situacao,
    string? Observacao,
    string LocalAtendimento,
    IReadOnlyList<ItemGuiaNoRecursoDto> Itens);

internal sealed record RecursoDetalheDto(RecursoDto Header, IReadOnlyList<GuiaNoRecursoDto> Guias);

internal sealed record ListarRecursosQuery(
    Guid? OperadoraId, Guid? PrestadorId, int Pagina, int ItensPorPagina);

internal sealed record AdicionarGuiasEmLoteCommand(
    Guid PrestadorId, Guid OperadoraId,
    DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, string? NumeroGuia, string? Beneficiario,
    bool? SomenteComGlosa);

internal sealed record RecursoPdfData(
    string TenantName,
    string OperadoraNome,
    string PrestadorNome,
    string? PrestadorRegistroProfissional,
    string Numero,
    IReadOnlyList<GuiaPdfData> Guias);

internal sealed record GuiaPdfData(
    DateOnly DataAtendimento,
    string NumeroGuia,
    string? BeneficiarioNome,
    string? BeneficiarioCarteira,
    string PosicaoExecutorLabel,
    string? Observacao,
    string LocalAtendimento,
    IReadOnlyList<ItemPdfData> Itens);

internal sealed record ItemPdfData(
    string CodigoTuss,
    string Descricao,
    string FatorEfetivo,
    decimal ValorPago,
    decimal ValorApurado);

internal sealed record ListarRecursosResult(
    IReadOnlyList<RecursoDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed class RecursoService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    internal async Task<Result<RecursoDto>> CriarAsync(
        CriarRecursoCommand cmd, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId!.Value;

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<RecursoDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == cmd.PrestadorId, ct);
        if (prestador is null)
        {
            return Result<RecursoDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        var recurso = Recurso.Create(tenantId, cmd.OperadoraId, cmd.PrestadorId, cmd.DataEmissao, cmd.Observacao);
        _db.Recursos.Add(recurso);
        await _db.SaveChangesAsync(ct);

        return Result<RecursoDto>.Ok(new RecursoDto(
            recurso.Id, recurso.OperadoraId, operadora.Nome,
            recurso.PrestadorId, prestador.Nome, prestador.RegistroProfissional,
            recurso.Numero, recurso.DataEmissao, recurso.Observacao,
            0, recurso.CriadoEm));
    }

    internal async Task<ListarRecursosResult> ListarAsync(
        ListarRecursosQuery query, CancellationToken ct = default)
    {
        var q = from r in _db.Recursos
                join op in _db.Operadoras on r.OperadoraId equals op.Id
                join pr in _db.Prestadores on r.PrestadorId equals pr.Id
                select new
                {
                    r.Id,
                    r.OperadoraId,
                    OperadoraNome = op.Nome,
                    r.PrestadorId,
                    PrestadorNome = pr.Nome,
                    PrestadorRegistroProfissional = pr.RegistroProfissional,
                    r.Numero,
                    r.DataEmissao,
                    r.Observacao,
                    r.CriadoEm,
                };

        if (query.OperadoraId.HasValue)
        {
            q = q.Where(x => x.OperadoraId == query.OperadoraId.Value);
        }

        if (query.PrestadorId.HasValue)
        {
            q = q.Where(x => x.PrestadorId == query.PrestadorId.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var pagina = await q
            .OrderByDescending(x => x.DataEmissao)
            .ThenByDescending(x => x.CriadoEm)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var ids = pagina.Select(x => x.Id).ToList();
        var guiaCounts = await _db.Guias
            .Where(g => g.RecursoId.HasValue && ids.Contains(g.RecursoId!.Value))
            .GroupBy(g => g.RecursoId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        var itens = pagina.Select(x => new RecursoDto(
            x.Id, x.OperadoraId, x.OperadoraNome,
            x.PrestadorId, x.PrestadorNome, x.PrestadorRegistroProfissional,
            x.Numero, x.DataEmissao, x.Observacao,
            guiaCounts.GetValueOrDefault(x.Id, 0),
            x.CriadoEm)).ToList();

        return new ListarRecursosResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<RecursoDetalheDto>> ObterPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var header = await (
            from r in _db.Recursos
            join op in _db.Operadoras on r.OperadoraId equals op.Id
            join pr in _db.Prestadores on r.PrestadorId equals pr.Id
            where r.Id == id
            select new
            {
                r.Id,
                r.OperadoraId,
                OperadoraNome = op.Nome,
                r.PrestadorId,
                PrestadorNome = pr.Nome,
                PrestadorRegistroProfissional = pr.RegistroProfissional,
                r.Numero,
                r.DataEmissao,
                r.Observacao,
                r.CriadoEm,
            }).FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return Result<RecursoDetalheDto>.Fail(new NotFoundError("Recurso não encontrado."));
        }

        var guiasRaw = await (
            from g in _db.Guias
            join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
            from b in bs.DefaultIfEmpty()
            where g.RecursoId == id
            orderby g.DataAtendimento
            select new
            {
                g.Id,
                g.NumeroGuia,
                g.DataAtendimento,
                g.Situacao,
                g.Observacao,
                g.LocalAtendimento,
                BeneficiarioNome = (string?)b.Nome,
                BeneficiarioCarteira = (string?)b.Carteira,
            }).ToListAsync(ct);

        var guiaIds = guiasRaw.Select(g => g.Id).ToList();
        var itens = await (
            from i in _db.ItensGuia
            join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
            where guiaIds.Contains(i.GuiaId)
            select new
            {
                i.Id,
                i.GuiaId,
                CodigoTuss = p.CodigoTuss,
                DescricaoProcedimento = p.Descricao,
                i.PosicaoExecutor,
                i.PercentualOrdem,
                i.ViaAcesso,
                i.Acomodacao,
                i.EhUrgencia,
                i.ValorApurado,
                i.ValorLiquidado,
            }).ToListAsync(ct);

        var itensPorGuia = itens.GroupBy(i => i.GuiaId)
            .ToDictionary(g => g.Key, g => g
                .Select(i => new ItemGuiaNoRecursoDto(
                    i.Id, i.CodigoTuss, i.DescricaoProcedimento,
                    i.PosicaoExecutor, i.PercentualOrdem,
                    i.ViaAcesso, i.Acomodacao, i.EhUrgencia,
                    i.ValorApurado, i.ValorLiquidado))
                .ToList());

        var headerDto = new RecursoDto(
            header.Id, header.OperadoraId, header.OperadoraNome,
            header.PrestadorId, header.PrestadorNome, header.PrestadorRegistroProfissional,
            header.Numero, header.DataEmissao, header.Observacao,
            guiasRaw.Count, header.CriadoEm);

        var guiaDtos = guiasRaw.Select(g => new GuiaNoRecursoDto(
            g.Id, g.NumeroGuia, g.DataAtendimento,
            g.BeneficiarioNome, g.BeneficiarioCarteira,
            g.Situacao, g.Observacao, g.LocalAtendimento,
            itensPorGuia.GetValueOrDefault(g.Id, []))).ToList();

        return Result<RecursoDetalheDto>.Ok(new RecursoDetalheDto(headerDto, guiaDtos));
    }

    internal async Task<Result<RecursoDto>> AtualizarAsync(
        Guid id, AtualizarRecursoCommand cmd, CancellationToken ct = default)
    {
        var recurso = await _db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recurso is null)
        {
            return Result<RecursoDto>.Fail(new NotFoundError("Recurso não encontrado."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<RecursoDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == cmd.PrestadorId, ct);
        if (prestador is null)
        {
            return Result<RecursoDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        recurso.Atualizar(cmd.OperadoraId, cmd.PrestadorId, cmd.DataEmissao, cmd.Observacao);
        await _db.SaveChangesAsync(ct);

        var totalGuias = await _db.Guias.CountAsync(g => g.RecursoId == id, ct);

        return Result<RecursoDto>.Ok(new RecursoDto(
            recurso.Id, recurso.OperadoraId, operadora.Nome,
            recurso.PrestadorId, prestador.Nome, prestador.RegistroProfissional,
            recurso.Numero, recurso.DataEmissao, recurso.Observacao,
            totalGuias, recurso.CriadoEm));
    }

    internal async Task<Result> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var recurso = await _db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recurso is null)
        {
            return Result.Fail(new NotFoundError("Recurso não encontrado."));
        }

        var temGuias = await _db.Guias.AnyAsync(g => g.RecursoId == id, ct);
        if (temGuias)
        {
            throw new InvalidOperationException("Não é possível excluir um recurso com guias vinculadas.");
        }

        _db.Recursos.Remove(recurso);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    internal async Task AdicionarGuiaAsync(
        Guid recursoId, Guid guiaId, CancellationToken ct = default)
    {
        if (!await _db.Recursos.AnyAsync(r => r.Id == recursoId, ct))
        {
            throw new InvalidOperationException("Recurso não encontrado.");
        }

        var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct)
            ?? throw new InvalidOperationException("Guia não encontrada.");

        if (guia.RecursoId != null)
        {
            throw new InvalidOperationException("A guia já está vinculada a outro recurso.");
        }

        guia.MarcarEmRecurso(recursoId);
        await _db.SaveChangesAsync(ct);
    }

    internal async Task RemoverGuiaAsync(
        Guid recursoId, Guid guiaId, CancellationToken ct = default)
    {
        var guia = await _db.Guias
            .FirstOrDefaultAsync(g => g.Id == guiaId && g.RecursoId == recursoId, ct)
            ?? throw new InvalidOperationException("Guia não encontrada neste recurso.");

        var todosLiquidados = await _db.ItensGuia
            .Where(i => i.GuiaId == guiaId)
            .AllAsync(i => i.ValorLiquidado.HasValue, ct);

        guia.RemoverDoRecurso(todosLiquidados);
        await _db.SaveChangesAsync(ct);
    }

    internal async Task<Result<int>> AdicionarGuiasEmLoteAsync(
        Guid recursoId, AdicionarGuiasEmLoteCommand cmd, CancellationToken ct = default)
    {
        if (!await _db.Recursos.AnyAsync(r => r.Id == recursoId, ct))
        {
            return Result<int>.Fail(new NotFoundError("Recurso não encontrado."));
        }

        var q = _db.Guias.Where(g =>
            g.PrestadorId == cmd.PrestadorId &&
            g.OperadoraId == cmd.OperadoraId &&
            g.RecursoId == null);

        if (cmd.DataInicio.HasValue)
        {
            q = q.Where(g => g.DataAtendimento >= cmd.DataInicio.Value);
        }

        if (cmd.DataFim.HasValue)
        {
            q = q.Where(g => g.DataAtendimento <= cmd.DataFim.Value);
        }

        if (cmd.Situacao.HasValue)
        {
            q = q.Where(g => g.Situacao == cmd.Situacao.Value);
        }

        if (!string.IsNullOrWhiteSpace(cmd.NumeroGuia))
        {
            q = q.Where(g => g.NumeroGuia.Contains(cmd.NumeroGuia));
        }

        if (cmd.SomenteComGlosa == true)
        {
            q = q.Where(g => _db.ItensGuia.Any(i =>
                i.GuiaId == g.Id &&
                i.ValorApurado.HasValue && i.ValorLiquidado.HasValue &&
                i.ValorApurado > i.ValorLiquidado));
        }

        if (!string.IsNullOrWhiteSpace(cmd.Beneficiario))
        {
            q = q.Where(g => g.BeneficiarioId.HasValue &&
                _db.Beneficiarios.Any(b =>
                    b.Id == g.BeneficiarioId.Value &&
                    b.Nome.Contains(cmd.Beneficiario)));
        }

        var guias = await q.ToListAsync(ct);
        foreach (var guia in guias)
        {
            guia.MarcarEmRecurso(recursoId);
        }

        await _db.SaveChangesAsync(ct);
        return Result<int>.Ok(guias.Count);
    }

    internal async Task<Result<RecursoPdfData>> ObterDadosPdfAsync(
        Guid id, CancellationToken ct = default)
    {
        var recurso = await _db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recurso is null)
        {
            return Result<RecursoPdfData>.Fail(new NotFoundError("Recurso não encontrado."));
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == recurso.TenantId, ct);
        var operadora = await _db.Operadoras.FirstAsync(o => o.Id == recurso.OperadoraId, ct);
        var prestador = await _db.Prestadores.FirstAsync(p => p.Id == recurso.PrestadorId, ct);

        var guiasRaw = await (
            from g in _db.Guias
            where g.RecursoId == id
            join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
            from b in bs.DefaultIfEmpty()
            orderby g.DataAtendimento
            select new
            {
                g.Id,
                g.NumeroGuia,
                g.DataAtendimento,
                g.Observacao,
                g.LocalAtendimento,
                BeneficiarioNome = (string?)b.Nome,
                BeneficiarioCarteira = (string?)b.Carteira,
            }).ToListAsync(ct);

        var guiaIds = guiasRaw.Select(g => g.Id).ToList();

        var itensRaw = await (
            from i in _db.ItensGuia
            where guiaIds.Contains(i.GuiaId)
            join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
            orderby i.PercentualOrdem descending
            select new
            {
                i.Id,
                i.GuiaId,
                p.CodigoTuss,
                p.Descricao,
                i.PosicaoExecutor,
                i.ValorLiquidado,
                i.ValorApurado,
            }).ToListAsync(ct);

        var calculosRaw = await _db.Calculos
            .Where(c => guiaIds.Contains(c.GuiaId))
            .Select(c => new { c.Id, c.GuiaId })
            .ToListAsync(ct);

        var calculoIds = calculosRaw.Select(c => c.Id).ToList();

        var passosRaw = await _db.PassosCalculo
            .Where(p => calculoIds.Contains(p.CalculoId))
            .Select(p => new { p.ItemGuiaId, p.Regra, p.Fator })
            .ToListAsync(ct);

        var itensPorGuia = itensRaw
            .GroupBy(i => i.GuiaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var passosPorItem = passosRaw
            .GroupBy(p => p.ItemGuiaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var guiaDtos = guiasRaw.Select(g =>
        {
            var guiaItens = itensPorGuia.GetValueOrDefault(g.Id, []);
            var primeiraPos = guiaItens.Count > 0
                ? guiaItens[0].PosicaoExecutor
                : PosicaoExecutor.Cirurgiao;

            var itemDtos = guiaItens.Select(i =>
            {
                var itemPassos = passosPorItem.GetValueOrDefault(i.Id, []);
                var fatoresNaoBase = itemPassos
                    .Where(p => p.Regra != "ValorBase")
                    .Select(p => p.Fator);
                return new ItemPdfData(
                    i.CodigoTuss,
                    i.Descricao,
                    CalcularFatorEfetivo(fatoresNaoBase),
                    i.ValorLiquidado ?? 0m,
                    i.ValorApurado ?? 0m);
            }).ToList();

            return new GuiaPdfData(
                g.DataAtendimento,
                g.NumeroGuia,
                g.BeneficiarioNome,
                g.BeneficiarioCarteira,
                PosicaoLabel(primeiraPos),
                string.IsNullOrEmpty(g.Observacao) ? null : g.Observacao,
                g.LocalAtendimento,
                itemDtos);
        }).ToList();

        return Result<RecursoPdfData>.Ok(new RecursoPdfData(
            tenant?.Name ?? string.Empty,
            operadora.Nome,
            prestador.Nome,
            prestador.RegistroProfissional,
            recurso.Numero,
            guiaDtos));
    }

    private static string CalcularFatorEfetivo(IEnumerable<decimal> fatores)
    {
        var lista = fatores.ToList();
        if (lista.Count == 0)
        {
            return "—";
        }

        var produto = lista.Aggregate(1m, (acc, f) => acc * f);
        return $"{(produto * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%";
    }

    private static string PosicaoLabel(PosicaoExecutor p) => p switch
    {
        PosicaoExecutor.Cirurgiao => "Cirurgião",
        PosicaoExecutor.PrimeiroAuxiliar => "1º Auxiliar",
        PosicaoExecutor.SegundoAuxiliar => "2º Auxiliar",
        PosicaoExecutor.TerceiroAuxiliar => "3º Auxiliar",
        PosicaoExecutor.Anestesista => "Anestesista",
        PosicaoExecutor.ClinicoAssistente => "Clínico Assistente",
        _ => p.ToString(),
    };
}
