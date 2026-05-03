using System.Text;
using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Catalog;

internal sealed record ListarProcedimentosQuery(string? Busca, bool? Ativo, int Pagina, int ItensPorPagina);

internal sealed record ListarProcedimentosResult(
    IReadOnlyList<ProcedimentoDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record ProcedimentoDto(
    Guid Id, string CodigoTuss, string Descricao, string? Porte, int? PorteAnestesico,
    bool EhSadt, bool TemPorteProprioVideo, bool Ativo, DateTimeOffset CriadoEm);

internal sealed record SalvarProcedimentoCommand(
    string CodigoTuss, string Descricao, string? Porte, int? PorteAnestesico,
    bool EhSadt, bool TemPorteProprioVideo, bool Ativo);

internal sealed record ImportarCsvResult(
    int Inseridos, int Atualizados, int Ignorados, IReadOnlyList<ErroCsvLinha> Erros);

internal sealed record ErroCsvLinha(int Linha, string Mensagem);

internal sealed record ListarOperadorasQuery(string? Nome, bool? Ativa, int Pagina, int ItensPorPagina);

internal sealed record ListarOperadorasResult(
    IReadOnlyList<OperadoraDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record OperadoraDto(
    Guid Id, string Nome, string? RegistroAns, string? Cnpj,
    TipoRuleSet TipoRuleSet, bool Ativa, DateTimeOffset CriadaEm);

internal sealed record CriarOperadoraCommand(
    string Nome, string? RegistroAns, string? Cnpj, TipoRuleSet TipoRuleSet);

internal sealed record AtualizarOperadoraCommand(
    string Nome, string? RegistroAns, string? Cnpj, TipoRuleSet TipoRuleSet, bool Ativa);

internal sealed class CatalogService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    internal async Task<ListarOperadorasResult> ListarAsync(
        ListarOperadorasQuery query, CancellationToken ct = default)
    {
        var q = _db.Operadoras.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Nome))
        {
            var pattern = $"%{query.Nome}%";
            q = q.Where(o => EF.Functions.ILike(o.Nome, pattern));
        }

        if (query.Ativa.HasValue)
        {
            q = q.Where(o => o.Ativa == query.Ativa.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var itens = await q
            .OrderBy(o => o.Nome)
            .Skip(skip)
            .Take(itensPorPagina)
            .Select(o => new OperadoraDto(
                o.Id, o.Nome, o.RegistroAns, o.Cnpj, o.TipoRuleSet, o.Ativa, o.CriadaEm))
            .ToListAsync(ct);

        return new ListarOperadorasResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<OperadoraDto>> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var op = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null)
        {
            return Result<OperadoraDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        return Result<OperadoraDto>.Ok(ToDto(op));
    }

    internal async Task<Result<OperadoraDto>> CriarAsync(
        CriarOperadoraCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Nome))
        {
            return Result<OperadoraDto>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (cmd.RegistroAns is not null && cmd.RegistroAns.Length != 6)
        {
            return Result<OperadoraDto>.Fail(
                new ValidationError("Registro ANS deve ter exatamente 6 dígitos."));
        }

        if (cmd.Cnpj is not null && cmd.Cnpj.Length != 14)
        {
            return Result<OperadoraDto>.Fail(
                new ValidationError("CNPJ deve ter exatamente 14 dígitos."));
        }

        if (cmd.Cnpj is not null && await _db.Operadoras.AnyAsync(o => o.Cnpj == cmd.Cnpj, ct))
        {
            return Result<OperadoraDto>.Fail(new ConflictError("CNPJ já cadastrado neste tenant."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var op = Operadora.Create(tenantId, cmd.Nome.Trim(), cmd.RegistroAns, cmd.Cnpj, cmd.TipoRuleSet);
        _db.Operadoras.Add(op);
        await _db.SaveChangesAsync(ct);
        return Result<OperadoraDto>.Ok(ToDto(op));
    }

    internal async Task<Result<OperadoraDto>> AtualizarAsync(
        Guid id, AtualizarOperadoraCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Nome))
        {
            return Result<OperadoraDto>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (cmd.RegistroAns is not null && cmd.RegistroAns.Length != 6)
        {
            return Result<OperadoraDto>.Fail(
                new ValidationError("Registro ANS deve ter exatamente 6 dígitos."));
        }

        if (cmd.Cnpj is not null && cmd.Cnpj.Length != 14)
        {
            return Result<OperadoraDto>.Fail(
                new ValidationError("CNPJ deve ter exatamente 14 dígitos."));
        }

        var op = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null)
        {
            return Result<OperadoraDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        if (cmd.Cnpj is not null
            && await _db.Operadoras.AnyAsync(o => o.Cnpj == cmd.Cnpj && o.Id != id, ct))
        {
            return Result<OperadoraDto>.Fail(new ConflictError("CNPJ já cadastrado neste tenant."));
        }

        op.Atualizar(cmd.Nome.Trim(), cmd.RegistroAns, cmd.Cnpj, cmd.TipoRuleSet, cmd.Ativa);
        await _db.SaveChangesAsync(ct);
        return Result<OperadoraDto>.Ok(ToDto(op));
    }

    internal async Task<Result> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var op = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null)
        {
            return Result.Fail(new NotFoundError("Operadora não encontrada."));
        }

        _db.Operadoras.Remove(op);
        await _db.SaveChangesAsync(ct);
        // TODO F3.1: bloquear se houver Guias associadas
        return Result.Ok();
    }

    private static OperadoraDto ToDto(Operadora op) =>
        new(op.Id, op.Nome, op.RegistroAns, op.Cnpj, op.TipoRuleSet, op.Ativa, op.CriadaEm);

    // ── Procedimento ─────────────────────────────────────────────────────────

    internal async Task<ListarProcedimentosResult> ListarProcedimentosAsync(
        ListarProcedimentosQuery query, CancellationToken ct = default)
    {
        var q = _db.Procedimentos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Busca))
        {
            var pattern = $"%{query.Busca}%";
            q = q.Where(p =>
                EF.Functions.ILike(p.CodigoTuss, pattern) ||
                EF.Functions.ILike(p.Descricao, pattern));
        }

        if (query.Ativo.HasValue)
        {
            q = q.Where(p => p.Ativo == query.Ativo.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var itens = await q
            .OrderBy(p => p.CodigoTuss)
            .Skip(skip)
            .Take(itensPorPagina)
            .Select(p => new ProcedimentoDto(
                p.Id, p.CodigoTuss, p.Descricao, p.Porte, p.PorteAnestesico,
                p.EhSadt, p.TemPorteProprioVideo, p.Ativo, p.CriadoEm))
            .ToListAsync(ct);

        return new ListarProcedimentosResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<ProcedimentoDto>> ObterProcedimentoPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var proc = await _db.Procedimentos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proc is null)
        {
            return Result<ProcedimentoDto>.Fail(new NotFoundError("Procedimento não encontrado."));
        }

        return Result<ProcedimentoDto>.Ok(ToDto(proc));
    }

    internal async Task<Result<ProcedimentoDto>> CriarProcedimentoAsync(
        SalvarProcedimentoCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComando(cmd);
        if (erro is not null)
        {
            return Result<ProcedimentoDto>.Fail(erro);
        }

        if (await _db.Procedimentos.AnyAsync(p => p.CodigoTuss == cmd.CodigoTuss, ct))
        {
            return Result<ProcedimentoDto>.Fail(new ConflictError("Código TUSS já cadastrado neste tenant."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var proc = Procedimento.Create(
            tenantId, cmd.CodigoTuss.Trim(), cmd.Descricao.Trim(),
            cmd.Porte, cmd.PorteAnestesico, cmd.EhSadt, cmd.TemPorteProprioVideo);
        _db.Procedimentos.Add(proc);
        await _db.SaveChangesAsync(ct);
        return Result<ProcedimentoDto>.Ok(ToDto(proc));
    }

    internal async Task<Result<ProcedimentoDto>> AtualizarProcedimentoAsync(
        Guid id, SalvarProcedimentoCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComando(cmd);
        if (erro is not null)
        {
            return Result<ProcedimentoDto>.Fail(erro);
        }

        var proc = await _db.Procedimentos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proc is null)
        {
            return Result<ProcedimentoDto>.Fail(new NotFoundError("Procedimento não encontrado."));
        }

        if (await _db.Procedimentos.AnyAsync(p => p.CodigoTuss == cmd.CodigoTuss && p.Id != id, ct))
        {
            return Result<ProcedimentoDto>.Fail(new ConflictError("Código TUSS já cadastrado neste tenant."));
        }

        proc.Atualizar(
            cmd.CodigoTuss.Trim(), cmd.Descricao.Trim(), cmd.Porte,
            cmd.PorteAnestesico, cmd.EhSadt, cmd.TemPorteProprioVideo, cmd.Ativo);
        await _db.SaveChangesAsync(ct);
        return Result<ProcedimentoDto>.Ok(ToDto(proc));
    }

    internal async Task<Result> ExcluirProcedimentoAsync(Guid id, CancellationToken ct = default)
    {
        var proc = await _db.Procedimentos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proc is null)
        {
            return Result.Fail(new NotFoundError("Procedimento não encontrado."));
        }

        _db.Procedimentos.Remove(proc);
        await _db.SaveChangesAsync(ct);
        // TODO F3.1: bloquear se procedimento estiver em uso em Guias
        return Result.Ok();
    }

    internal async Task<ImportarCsvResult> ImportarProcedimentosCsvAsync(
        Stream csvStream, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId!.Value;
        using var reader = new StreamReader(csvStream, Encoding.UTF8);

        var linhas = new List<string>();
        string? lida;
        while ((lida = await reader.ReadLineAsync(ct)) is not null)
        {
            linhas.Add(lida);
        }

        if (linhas.Count == 0)
        {
            return new ImportarCsvResult(0, 0, 0, []);
        }

        // Primeira linha = cabeçalho
        if (linhas.Count - 1 > 10000)
        {
            return new ImportarCsvResult(0, 0, 0,
                [new ErroCsvLinha(0, "O arquivo excede o limite de 10.000 linhas de dados.")]);
        }

        var header = linhas[0].Split(';');
        var colIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
        {
            colIdx[header[i].Trim()] = i;
        }

        var erros = new List<ErroCsvLinha>();
        var ignorados = 0;

        var validRows = new Dictionary<string, (string CodigoTuss, string Descricao, string? Porte, int? PorteAnestesico, bool EhSadt, bool TemPorteProprioVideo)>(
            StringComparer.Ordinal);

        for (var i = 1; i < linhas.Count; i++)
        {
            var lineNum = i + 1;
            var cols = linhas[i].Split(';');

            var codigoTuss = GetColValue(cols, colIdx, "CodigoTuss")?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(codigoTuss))
            {
                ignorados++;
                continue;
            }

            if (codigoTuss.Length > 10)
            {
                erros.Add(new ErroCsvLinha(lineNum,
                    $"CodigoTuss '{codigoTuss}' tem mais de 10 caracteres."));
                continue;
            }

            var descricao = GetColValue(cols, colIdx, "Descricao")?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(descricao))
            {
                erros.Add(new ErroCsvLinha(lineNum, "Descricao é obrigatória."));
                continue;
            }

            var porteStr = GetColValue(cols, colIdx, "Porte")?.Trim();
            string? porte = string.IsNullOrEmpty(porteStr) ? null : porteStr;

            int? porteAnestesico = null;
            var porteAnestesicoStr = GetColValue(cols, colIdx, "PorteAnestesico")?.Trim();
            if (!string.IsNullOrEmpty(porteAnestesicoStr))
            {
                if (!int.TryParse(porteAnestesicoStr, out var pa))
                {
                    erros.Add(new ErroCsvLinha(lineNum,
                        $"PorteAnestesico '{porteAnestesicoStr}' não é um número válido."));
                    continue;
                }

                if (pa < 0 || pa > 8)
                {
                    erros.Add(new ErroCsvLinha(lineNum,
                        $"PorteAnestesico deve ser entre 0 e 8."));
                    continue;
                }

                porteAnestesico = pa;
            }

            var ehSadtStr = GetColValue(cols, colIdx, "EhSadt")?.Trim();
            var ehSadt = ehSadtStr?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            var videoStr = GetColValue(cols, colIdx, "TemPorteProprioVideo")?.Trim();
            var temPorteProprioVideo = videoStr?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            validRows[codigoTuss] = (codigoTuss, descricao, porte, porteAnestesico, ehSadt, temPorteProprioVideo);
        }

        var codes = validRows.Keys.ToList();
        var existingCodes = await _db.Procedimentos
            .Where(p => codes.Contains(p.CodigoTuss))
            .Select(p => p.CodigoTuss)
            .ToHashSetAsync(ct);

        var atualizados = 0;
        foreach (var row in validRows.Values.Where(r => existingCodes.Contains(r.CodigoTuss)))
        {
            var codigo = row.CodigoTuss;
            var descricaoAtual = row.Descricao;
            var porteAtual = row.Porte;
            var porteAnestesicoAtual = row.PorteAnestesico;
            var ehSadtAtual = row.EhSadt;
            var temVideoAtual = row.TemPorteProprioVideo;
            await _db.Procedimentos
                .Where(p => p.CodigoTuss == codigo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Descricao, descricaoAtual)
                    .SetProperty(p => p.Porte, porteAtual)
                    .SetProperty(p => p.PorteAnestesico, porteAnestesicoAtual)
                    .SetProperty(p => p.EhSadt, ehSadtAtual)
                    .SetProperty(p => p.TemPorteProprioVideo, temVideoAtual),
                ct);
            atualizados++;
        }

        var novos = validRows.Values
            .Where(r => !existingCodes.Contains(r.CodigoTuss))
            .Select(r => Procedimento.Create(
                tenantId, r.CodigoTuss, r.Descricao, r.Porte,
                r.PorteAnestesico, r.EhSadt, r.TemPorteProprioVideo))
            .ToList();
        _db.Procedimentos.AddRange(novos);
        await _db.SaveChangesAsync(ct);

        return new ImportarCsvResult(novos.Count, atualizados, ignorados, erros);
    }

    private static string? GetColValue(string[] cols, Dictionary<string, int> colIdx, string name)
    {
        if (!colIdx.TryGetValue(name, out var idx) || idx >= cols.Length)
        {
            return null;
        }

        return cols[idx];
    }

    private static ValidationError? ValidarComando(SalvarProcedimentoCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.CodigoTuss))
        {
            return new ValidationError("Código TUSS é obrigatório.");
        }

        if (cmd.CodigoTuss.Trim().Length > 10)
        {
            return new ValidationError("Código TUSS deve ter no máximo 10 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(cmd.Descricao))
        {
            return new ValidationError("Descrição é obrigatória.");
        }

        if (cmd.PorteAnestesico.HasValue && (cmd.PorteAnestesico.Value < 0 || cmd.PorteAnestesico.Value > 8))
        {
            return new ValidationError("Porte Anestésico deve ser entre 0 e 8.");
        }

        return null;
    }

    private static ProcedimentoDto ToDto(Procedimento p) =>
        new(p.Id, p.CodigoTuss, p.Descricao, p.Porte, p.PorteAnestesico,
            p.EhSadt, p.TemPorteProprioVideo, p.Ativo, p.CriadoEm);
}
