using System.Text;
using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Catalog;

internal sealed record ListarTabelasQuery(Guid? OperadoraId, string? CodigoTuss, int Pagina, int ItensPorPagina);

internal sealed record ListarTabelasResult(
    IReadOnlyList<TabelaDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record TabelaDto(
    Guid Id, Guid OperadoraId, Guid ProcedimentoId,
    string CodigoTuss, string Descricao, decimal Valor, DateTimeOffset AtualizadoEm);

internal sealed record SalvarTabelaCommand(Guid OperadoraId, Guid ProcedimentoId, decimal Valor);

internal sealed record ProcedimentoValorOperadoraItem(
    Guid OperadoraId,
    string OperadoraNome,
    TipoRuleSet TipoRuleSet,
    Guid? TabelaId,
    decimal? Valor,
    DateTimeOffset? AtualizadoEm);

internal sealed record UpsertValorResult(TabelaDto Tabela, bool Criado);

internal sealed record ListarPrestadoresQuery(string? Busca, bool? Ativo, int Pagina, int ItensPorPagina);

internal sealed record ListarPrestadoresResult(
    IReadOnlyList<PrestadorDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record PrestadorDto(
    Guid Id, string Nome, string? RegistroProfissional,
    bool Ativo, DateTimeOffset CriadoEm,
    string? EmailAcesso, bool TemUsuario);

internal sealed record CriarPrestadorCommand(string Nome, string? RegistroProfissional, string? EmailAcesso);

internal sealed record AtualizarPrestadorCommand(string Nome, string? RegistroProfissional, bool Ativo);

internal sealed record DeflatorDto(
    Guid Id, Guid PrestadorId, Guid OperadoraId, PosicaoExecutor Posicao, decimal Percentual);

internal sealed record SalvarDeflatorCommand(Guid OperadoraId, PosicaoExecutor Posicao, decimal Percentual);

internal sealed record ListarProcedimentosQuery(string? Busca, bool? Ativo, int Pagina, int ItensPorPagina);

internal sealed record ListarProcedimentosResult(
    IReadOnlyList<ProcedimentoDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record ProcedimentoDto(
    Guid Id, string CodigoTuss, string Descricao, string? Porte, string? PorteAnestesico,
    bool EhSadt, bool TemPorteProprioVideo, bool Ativo, DateTimeOffset CriadoEm);

internal sealed record SalvarProcedimentoCommand(
    string CodigoTuss, string Descricao, string? Porte, string? PorteAnestesico,
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

internal sealed record ListarBeneficiariosQuery(
    string? Carteira, string? Nome, int Pagina, int ItensPorPagina);

internal sealed record TabelaPorteAnestesicoItem(
    Guid Id, string PorteLetra, decimal ValorEnfermaria, decimal ValorApartamento,
    decimal? ValorAmbulatorial, DateTimeOffset AtualizadoEm);

internal sealed record ImportarCsvErro(int Linha, string Mensagem);

internal sealed record ImportarTabelaPorteResult(
    int PortesAtualizados,
    int ProcedimentosAtualizados,
    IReadOnlyList<string> ProcedimentosNaoEncontrados,
    IReadOnlyList<ImportarCsvErro> Erros);

internal sealed record ListarBeneficiariosResult(
    IReadOnlyList<BeneficiarioDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record BeneficiarioDto(
    Guid Id, string Carteira, string Nome, DateTimeOffset CriadoEm);

internal sealed record CriarBeneficiarioCommand(string Carteira, string Nome);

internal sealed record AtualizarBeneficiarioCommand(string Nome);

internal sealed record LookupOrCreateResult(BeneficiarioDto Beneficiario, bool Criado);

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

        if (await _db.Guias.AnyAsync(g => g.OperadoraId == id, ct))
        {
            return Result.Fail(new ConflictError("Operadora possui guias associadas."));
        }

        _db.Operadoras.Remove(op);
        await _db.SaveChangesAsync(ct);
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

        if (await _db.ItensGuia.AnyAsync(i => i.ProcedimentoId == id, ct))
        {
            return Result.Fail(new ConflictError("Procedimento possui guias associadas."));
        }

        _db.Procedimentos.Remove(proc);
        await _db.SaveChangesAsync(ct);
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

        var validRows = new Dictionary<string, (string CodigoTuss, string Descricao, string? Porte, string? PorteAnestesico, bool EhSadt, bool TemPorteProprioVideo)>(
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

            string? porteAnestesico = null;
            var porteAnestesicoStr = GetColValue(cols, colIdx, "PorteAnestesico")?.Trim();
            if (!string.IsNullOrEmpty(porteAnestesicoStr))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(porteAnestesicoStr, @"^[A-NP-Z]$"))
                {
                    erros.Add(new ErroCsvLinha(lineNum,
                        $"PorteAnestesico '{porteAnestesicoStr}' deve ser letra A–Z exceto O."));
                    continue;
                }

                porteAnestesico = porteAnestesicoStr;
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

        if (!string.IsNullOrEmpty(cmd.PorteAnestesico) &&
            !System.Text.RegularExpressions.Regex.IsMatch(cmd.PorteAnestesico, @"^[A-NP-Z]$"))
        {
            return new ValidationError("PorteAnestesico deve ser letra A–Z exceto O");
        }

        return null;
    }

    private static ProcedimentoDto ToDto(Procedimento p) =>
        new(p.Id, p.CodigoTuss, p.Descricao, p.Porte, p.PorteAnestesico,
            p.EhSadt, p.TemPorteProprioVideo, p.Ativo, p.CriadoEm);

    // ── TabelaProcedimento ────────────────────────────────────────────────────

    internal async Task<ListarTabelasResult> ListarTabelasAsync(
        ListarTabelasQuery query, CancellationToken ct = default)
    {
        var q = from t in _db.TabelasProcedimento
                join p in _db.Procedimentos on t.ProcedimentoId equals p.Id
                select new { t, p };

        if (query.OperadoraId.HasValue)
        {
            q = q.Where(x => x.t.OperadoraId == query.OperadoraId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CodigoTuss))
        {
            var pattern = $"%{query.CodigoTuss}%";
            q = q.Where(x => EF.Functions.ILike(x.p.CodigoTuss, pattern));
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var itens = await q
            .OrderBy(x => x.p.CodigoTuss)
            .Skip(skip)
            .Take(itensPorPagina)
            .Select(x => new TabelaDto(
                x.t.Id, x.t.OperadoraId, x.t.ProcedimentoId,
                x.p.CodigoTuss, x.p.Descricao, x.t.Valor, x.t.AtualizadoEm))
            .ToListAsync(ct);

        return new ListarTabelasResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<TabelaDto>> ObterTabelaPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var tabela = await (from t in _db.TabelasProcedimento
                            join p in _db.Procedimentos on t.ProcedimentoId equals p.Id
                            where t.Id == id
                            select new TabelaDto(
                                t.Id, t.OperadoraId, t.ProcedimentoId,
                                p.CodigoTuss, p.Descricao, t.Valor, t.AtualizadoEm))
            .FirstOrDefaultAsync(ct);

        if (tabela is null)
        {
            return Result<TabelaDto>.Fail(new NotFoundError("Entrada de tabela não encontrada."));
        }

        return Result<TabelaDto>.Ok(tabela);
    }

    internal async Task<Result<TabelaDto>> CriarTabelaAsync(
        SalvarTabelaCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Valor <= 0)
        {
            return Result<TabelaDto>.Fail(new ValidationError("Valor deve ser maior que zero."));
        }

        if (await _db.TabelasProcedimento.AnyAsync(
            t => t.OperadoraId == cmd.OperadoraId && t.ProcedimentoId == cmd.ProcedimentoId, ct))
        {
            return Result<TabelaDto>.Fail(
                new ConflictError("Já existe uma entrada para esta operadora e procedimento."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var tabela = TabelaProcedimento.Create(tenantId, cmd.OperadoraId, cmd.ProcedimentoId, cmd.Valor);
        _db.TabelasProcedimento.Add(tabela);
        await _db.SaveChangesAsync(ct);

        return await ObterTabelaPorIdAsync(tabela.Id, ct);
    }

    internal async Task<Result<TabelaDto>> AtualizarTabelaAsync(
        Guid id, SalvarTabelaCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Valor <= 0)
        {
            return Result<TabelaDto>.Fail(new ValidationError("Valor deve ser maior que zero."));
        }

        var tabela = await _db.TabelasProcedimento.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tabela is null)
        {
            return Result<TabelaDto>.Fail(new NotFoundError("Entrada de tabela não encontrada."));
        }

        tabela.AtualizarValor(cmd.Valor);
        await _db.SaveChangesAsync(ct);

        return await ObterTabelaPorIdAsync(id, ct);
    }

    internal async Task<Result> ExcluirTabelaAsync(Guid id, CancellationToken ct = default)
    {
        var tabela = await _db.TabelasProcedimento.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tabela is null)
        {
            return Result.Fail(new NotFoundError("Entrada de tabela não encontrada."));
        }

        _db.TabelasProcedimento.Remove(tabela);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    internal async Task<ImportarCsvResult> ImportarTabelaCsvAsync(
        Stream csvStream, Guid operadoraId, CancellationToken ct = default)
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
        var validRows = new List<(int LineNum, string CodigoTuss, decimal Valor)>();

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

            var valorStr = GetColValue(cols, colIdx, "Valor")?.Trim() ?? string.Empty;
            if (!decimal.TryParse(valorStr,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var valor))
            {
                erros.Add(new ErroCsvLinha(lineNum, $"Valor '{valorStr}' não é um número válido."));
                continue;
            }

            if (valor <= 0)
            {
                erros.Add(new ErroCsvLinha(lineNum, "Valor deve ser maior que zero."));
                continue;
            }

            validRows.Add((lineNum, codigoTuss, valor));
        }

        var codes = validRows.Select(r => r.CodigoTuss).Distinct().ToList();
        var procedimentos = await _db.Procedimentos
            .Where(p => codes.Contains(p.CodigoTuss))
            .Select(p => new { p.CodigoTuss, p.Id })
            .ToDictionaryAsync(p => p.CodigoTuss, p => p.Id, ct);

        var rowsParaUpsert = new List<(Guid ProcedimentoId, decimal Valor)>();
        foreach (var row in validRows)
        {
            if (!procedimentos.TryGetValue(row.CodigoTuss, out var procedimentoId))
            {
                erros.Add(new ErroCsvLinha(row.LineNum,
                    $"Código TUSS '{row.CodigoTuss}' não encontrado no catálogo."));
                continue;
            }

            rowsParaUpsert.Add((procedimentoId, row.Valor));
        }

        var procedimentoIds = rowsParaUpsert.Select(r => r.ProcedimentoId).ToList();
        var existentes = await _db.TabelasProcedimento
            .Where(t => t.OperadoraId == operadoraId && procedimentoIds.Contains(t.ProcedimentoId))
            .ToDictionaryAsync(t => t.ProcedimentoId, ct);

        var atualizados = 0;
        var novos = new List<TabelaProcedimento>();

        foreach (var row in rowsParaUpsert)
        {
            if (existentes.TryGetValue(row.ProcedimentoId, out var existente))
            {
                existente.AtualizarValor(row.Valor);
                atualizados++;
            }
            else
            {
                novos.Add(TabelaProcedimento.Create(tenantId, operadoraId, row.ProcedimentoId, row.Valor));
            }
        }

        _db.TabelasProcedimento.AddRange(novos);
        await _db.SaveChangesAsync(ct);

        return new ImportarCsvResult(novos.Count, atualizados, ignorados, erros);
    }

    // ── Valores por procedimento (camada de conveniência) ─────────────────────

    internal async Task<Result<IReadOnlyList<ProcedimentoValorOperadoraItem>>>
        ListarValoresPorProcedimentoAsync(Guid procedimentoId, CancellationToken ct = default)
    {
        var existe = await _db.Procedimentos.AnyAsync(p => p.Id == procedimentoId, ct);
        if (!existe)
        {
            return Result<IReadOnlyList<ProcedimentoValorOperadoraItem>>.Fail(
                new NotFoundError("Procedimento não encontrado."));
        }

        var itens = await (
            from o in _db.Operadoras
            where o.Ativa
            join t in _db.TabelasProcedimento.Where(x => x.ProcedimentoId == procedimentoId)
                on o.Id equals t.OperadoraId into ts
            from t in ts.DefaultIfEmpty()
            orderby o.Nome
            select new ProcedimentoValorOperadoraItem(
                o.Id,
                o.Nome,
                o.TipoRuleSet,
                t != null ? (Guid?)t.Id : null,
                t != null ? (decimal?)t.Valor : null,
                t != null ? (DateTimeOffset?)t.AtualizadoEm : null))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProcedimentoValorOperadoraItem>>.Ok(itens);
    }

    internal async Task<Result<UpsertValorResult>> UpsertValorAsync(
        Guid procedimentoId, Guid operadoraId, decimal valor, CancellationToken ct = default)
    {
        if (valor <= 0)
        {
            return Result<UpsertValorResult>.Fail(new ValidationError("Valor deve ser maior que zero."));
        }

        var proc = await _db.Procedimentos.FirstOrDefaultAsync(p => p.Id == procedimentoId, ct);
        if (proc is null)
        {
            return Result<UpsertValorResult>.Fail(new NotFoundError("Procedimento não encontrado."));
        }

        var op = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == operadoraId, ct);
        if (op is null)
        {
            return Result<UpsertValorResult>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        var tabela = await _db.TabelasProcedimento.FirstOrDefaultAsync(
            t => t.OperadoraId == operadoraId && t.ProcedimentoId == procedimentoId, ct);

        bool criado;
        if (tabela is null)
        {
            var tenantId = _currentUser.TenantId!.Value;
            tabela = TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, valor);
            _db.TabelasProcedimento.Add(tabela);
            criado = true;
        }
        else
        {
            tabela.AtualizarValor(valor);
            criado = false;
        }

        await _db.SaveChangesAsync(ct);

        var dto = new TabelaDto(
            tabela.Id, tabela.OperadoraId, tabela.ProcedimentoId,
            proc.CodigoTuss, proc.Descricao, tabela.Valor, tabela.AtualizadoEm);
        return Result<UpsertValorResult>.Ok(new UpsertValorResult(dto, criado));
    }

    internal async Task<Result> ExcluirValorPorProcOpAsync(
        Guid procedimentoId, Guid operadoraId, CancellationToken ct = default)
    {
        var tabela = await _db.TabelasProcedimento.FirstOrDefaultAsync(
            t => t.OperadoraId == operadoraId && t.ProcedimentoId == procedimentoId, ct);
        if (tabela is null)
        {
            return Result.Ok();
        }

        _db.TabelasProcedimento.Remove(tabela);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ── Prestador ─────────────────────────────────────────────────────────────

    internal async Task<ListarPrestadoresResult> ListarPrestadoresAsync(
        ListarPrestadoresQuery query, CancellationToken ct = default)
    {
        var q = _db.Prestadores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Busca))
        {
            var pattern = $"%{query.Busca}%";
            q = q.Where(p => EF.Functions.ILike(p.Nome, pattern));
        }

        if (query.Ativo.HasValue)
        {
            q = q.Where(p => p.Ativo == query.Ativo.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var itens = await q
            .OrderBy(p => p.Nome)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var idsList = itens.Select(p => p.Id).ToList();
        var comUsuario = (await _db.Users
            .Where(u => u.MedicoId.HasValue && idsList.Contains(u.MedicoId!.Value))
            .Select(u => u.MedicoId!.Value)
            .ToListAsync(ct))
            .ToHashSet();

        var dtos = itens
            .Select(p => ToDto(p, comUsuario.Contains(p.Id)))
            .ToList();

        return new ListarPrestadoresResult(dtos, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<PrestadorDto>> ObterPrestadorPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prestador is null)
        {
            return Result<PrestadorDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        var temUsuario = await _db.Users.AnyAsync(u => u.MedicoId == id, ct);
        return Result<PrestadorDto>.Ok(ToDto(prestador, temUsuario));
    }

    internal async Task<Result<PrestadorDto>> CriarPrestadorAsync(
        CriarPrestadorCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComandoPrestador(cmd.Nome);
        if (erro is not null)
        {
            return Result<PrestadorDto>.Fail(erro);
        }

        if (cmd.EmailAcesso is not null)
        {
#pragma warning disable CA1308 // e-mail deve ser normalizado em minúsculas por convenção
            var email = cmd.EmailAcesso.Trim().ToLowerInvariant();
#pragma warning restore CA1308
            if (!ApplicationUser.IsValidEmail(email))
            {
                return Result<PrestadorDto>.Fail(new ValidationError("E-mail de acesso inválido."));
            }

            if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return Result<PrestadorDto>.Fail(new ConflictError("E-mail já está em uso."));
            }
        }

        var tenantId = _currentUser.TenantId!.Value;
        var prestador = Prestador.Create(tenantId, cmd.Nome.Trim(), cmd.RegistroProfissional);
        _db.Prestadores.Add(prestador);

        if (cmd.EmailAcesso is not null)
        {
            prestador.SetEmailAcesso(cmd.EmailAcesso);
            var user = ApplicationUser.Create(prestador.EmailAcesso!, tenantId, prestador.Id);
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync(ct);
        return Result<PrestadorDto>.Ok(ToDto(prestador, cmd.EmailAcesso is not null));
    }

    internal async Task<Result<PrestadorDto>> AtualizarPrestadorAsync(
        Guid id, AtualizarPrestadorCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComandoPrestador(cmd.Nome);
        if (erro is not null)
        {
            return Result<PrestadorDto>.Fail(erro);
        }

        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prestador is null)
        {
            return Result<PrestadorDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        prestador.Atualizar(cmd.Nome.Trim(), cmd.RegistroProfissional, cmd.Ativo);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.MedicoId == id, ct);
        if (user is not null)
        {
            if (cmd.Ativo)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<PrestadorDto>.Ok(ToDto(prestador, user is not null));
    }

    internal async Task<Result> ExcluirPrestadorAsync(Guid id, CancellationToken ct = default)
    {
        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prestador is null)
        {
            return Result.Fail(new NotFoundError("Prestador não encontrado."));
        }

        if (await _db.Guias.AnyAsync(g => g.PrestadorId == id, ct))
        {
            return Result.Fail(new ConflictError("Prestador possui guias associadas."));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.MedicoId == id, ct);
        if (user?.GoogleId is not null)
        {
            return Result.Fail(new ConflictError(
                "Prestador possui usuário que já acessou o sistema. Desative-o em vez de excluir."));
        }

        if (user is not null)
        {
            _db.Users.Remove(user);
        }

        _db.Prestadores.Remove(prestador);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static PrestadorDto ToDto(Prestador p, bool temUsuario) =>
        new(p.Id, p.Nome, p.RegistroProfissional, p.Ativo, p.CriadoEm, p.EmailAcesso, temUsuario);

    private static ValidationError? ValidarComandoPrestador(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return new ValidationError("Nome é obrigatório.");
        }

        if (nome.Trim().Length > 150)
        {
            return new ValidationError("Nome deve ter no máximo 150 caracteres.");
        }

        return null;
    }

    // ── DeflatorPrestador ─────────────────────────────────────────────────────

    internal async Task<IReadOnlyList<DeflatorDto>> ListarDeflatoresAsync(
        Guid prestadorId, CancellationToken ct = default)
    {
        return await _db.DeflatoresPrestador
            .Where(d => d.PrestadorId == prestadorId)
            .OrderBy(d => d.Posicao)
            .Select(d => new DeflatorDto(d.Id, d.PrestadorId, d.OperadoraId, d.Posicao, d.Percentual))
            .ToListAsync(ct);
    }

    internal async Task<Result<DeflatorDto>> CriarDeflatorAsync(
        Guid prestadorId, SalvarDeflatorCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComandoDeflator(cmd);
        if (erro is not null)
        {
            return Result<DeflatorDto>.Fail(erro);
        }

        var prestador = await _db.Prestadores.FirstOrDefaultAsync(p => p.Id == prestadorId, ct);
        if (prestador is null)
        {
            return Result<DeflatorDto>.Fail(new NotFoundError("Prestador não encontrado."));
        }

        if (await _db.DeflatoresPrestador.AnyAsync(
            d => d.PrestadorId == prestadorId &&
                 d.OperadoraId == cmd.OperadoraId &&
                 d.Posicao == cmd.Posicao, ct))
        {
            return Result<DeflatorDto>.Fail(
                new ConflictError("Já existe um deflator para este prestador, operadora e posição."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var deflator = DeflatorPrestador.Create(
            tenantId, prestadorId, cmd.OperadoraId, cmd.Posicao, cmd.Percentual);
        _db.DeflatoresPrestador.Add(deflator);
        await _db.SaveChangesAsync(ct);

        return Result<DeflatorDto>.Ok(
            new DeflatorDto(deflator.Id, deflator.PrestadorId, deflator.OperadoraId,
                deflator.Posicao, deflator.Percentual));
    }

    internal async Task<Result<DeflatorDto>> AtualizarDeflatorAsync(
        Guid prestadorId, Guid id, SalvarDeflatorCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComandoDeflator(cmd);
        if (erro is not null)
        {
            return Result<DeflatorDto>.Fail(erro);
        }

        var deflator = await _db.DeflatoresPrestador
            .FirstOrDefaultAsync(d => d.Id == id && d.PrestadorId == prestadorId, ct);
        if (deflator is null)
        {
            return Result<DeflatorDto>.Fail(new NotFoundError("Deflator não encontrado."));
        }

        deflator.AtualizarPercentual(cmd.Percentual);
        await _db.SaveChangesAsync(ct);

        return Result<DeflatorDto>.Ok(
            new DeflatorDto(deflator.Id, deflator.PrestadorId, deflator.OperadoraId,
                deflator.Posicao, deflator.Percentual));
    }

    internal async Task<Result> ExcluirDeflatorAsync(
        Guid prestadorId, Guid id, CancellationToken ct = default)
    {
        var deflator = await _db.DeflatoresPrestador
            .FirstOrDefaultAsync(d => d.Id == id && d.PrestadorId == prestadorId, ct);
        if (deflator is null)
        {
            return Result.Fail(new NotFoundError("Deflator não encontrado."));
        }

        _db.DeflatoresPrestador.Remove(deflator);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static ValidationError? ValidarComandoDeflator(SalvarDeflatorCommand cmd)
    {
        if (cmd.Percentual <= 0)
        {
            return new ValidationError("Percentual deve ser maior que zero.");
        }

        if (cmd.Percentual > 200)
        {
            return new ValidationError("Percentual não pode exceder 200.");
        }

        return null;
    }

    // ── Beneficiário ──────────────────────────────────────────────────────────

    internal async Task<ListarBeneficiariosResult> ListarBeneficiariosAsync(
        ListarBeneficiariosQuery query, CancellationToken ct = default)
    {
        var q = _db.Beneficiarios.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Carteira))
        {
            var pattern = $"%{query.Carteira.Trim().ToUpperInvariant()}%";
            q = q.Where(b => EF.Functions.ILike(b.Carteira, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Nome))
        {
            var pattern = $"%{query.Nome}%";
            q = q.Where(b => EF.Functions.ILike(b.Nome, pattern));
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var itens = await q
            .OrderBy(b => b.Nome)
            .Skip(skip)
            .Take(itensPorPagina)
            .Select(b => new BeneficiarioDto(b.Id, b.Carteira, b.Nome, b.CriadoEm))
            .ToListAsync(ct);

        return new ListarBeneficiariosResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<BeneficiarioDto>> ObterBeneficiarioPorIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var beneficiario = await _db.Beneficiarios.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beneficiario is null)
        {
            return Result<BeneficiarioDto>.Fail(new NotFoundError("Beneficiário não encontrado."));
        }

        return Result<BeneficiarioDto>.Ok(ToBeneficiarioDto(beneficiario));
    }

    internal async Task<Result<BeneficiarioDto>> CriarBeneficiarioAsync(
        CriarBeneficiarioCommand cmd, CancellationToken ct = default)
    {
        var erro = ValidarComandoBeneficiario(cmd.Carteira, cmd.Nome);
        if (erro is not null)
        {
            return Result<BeneficiarioDto>.Fail(erro);
        }

        var carteiraNormalizada = cmd.Carteira.Trim().ToUpperInvariant();
        var existe = await _db.Beneficiarios
            .AnyAsync(b => b.Carteira == carteiraNormalizada, ct);
        if (existe)
        {
            return Result<BeneficiarioDto>.Fail(
                new ConflictError("Carteira já cadastrada neste tenant."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var beneficiario = App.Catalog.Beneficiario.Create(tenantId, cmd.Carteira, cmd.Nome);
        _db.Beneficiarios.Add(beneficiario);
        await _db.SaveChangesAsync(ct);
        return Result<BeneficiarioDto>.Ok(ToBeneficiarioDto(beneficiario));
    }

    internal async Task<Result<BeneficiarioDto>> AtualizarBeneficiarioAsync(
        Guid id, AtualizarBeneficiarioCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Nome))
        {
            return Result<BeneficiarioDto>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (cmd.Nome.Trim().Length > 150)
        {
            return Result<BeneficiarioDto>.Fail(
                new ValidationError("Nome deve ter no máximo 150 caracteres."));
        }

        var beneficiario = await _db.Beneficiarios.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beneficiario is null)
        {
            return Result<BeneficiarioDto>.Fail(new NotFoundError("Beneficiário não encontrado."));
        }

        beneficiario.Atualizar(cmd.Nome);
        await _db.SaveChangesAsync(ct);
        return Result<BeneficiarioDto>.Ok(ToBeneficiarioDto(beneficiario));
    }

    internal async Task<Result> ExcluirBeneficiarioAsync(Guid id, CancellationToken ct = default)
    {
        var beneficiario = await _db.Beneficiarios.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beneficiario is null)
        {
            return Result.Fail(new NotFoundError("Beneficiário não encontrado."));
        }

        // TODO F3.1: verificar Guias associadas antes de excluir (retornar 409 se houver)
        _db.Beneficiarios.Remove(beneficiario);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    internal async Task<Result<LookupOrCreateResult>> LookupOrCreateAsync(
        string carteira, string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(carteira))
        {
            return Result<LookupOrCreateResult>.Fail(new ValidationError("Carteira é obrigatória."));
        }

        var carteiraNormalizada = carteira.Trim().ToUpperInvariant();

        var existente = await _db.Beneficiarios
            .FirstOrDefaultAsync(b => b.Carteira == carteiraNormalizada, ct);

        if (existente is not null)
        {
            return Result<LookupOrCreateResult>.Ok(
                new LookupOrCreateResult(ToBeneficiarioDto(existente), false));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<LookupOrCreateResult>.Fail(new ValidationError("Nome é obrigatório."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var novo = Beneficiario.Create(tenantId, carteira, nome);
        _db.Beneficiarios.Add(novo);
        await _db.SaveChangesAsync(ct);

        return Result<LookupOrCreateResult>.Ok(new LookupOrCreateResult(ToBeneficiarioDto(novo), true));
    }

    private static BeneficiarioDto ToBeneficiarioDto(App.Catalog.Beneficiario b) =>
        new(b.Id, b.Carteira, b.Nome, b.CriadoEm);

    private static ValidationError? ValidarComandoBeneficiario(string carteira, string nome)
    {
        if (string.IsNullOrWhiteSpace(carteira))
        {
            return new ValidationError("Carteira é obrigatória.");
        }

        if (carteira.Trim().Length > 50)
        {
            return new ValidationError("Carteira deve ter no máximo 50 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return new ValidationError("Nome é obrigatório.");
        }

        if (nome.Trim().Length > 150)
        {
            return new ValidationError("Nome deve ter no máximo 150 caracteres.");
        }

        return null;
    }

    // ── TabelaPorteAnestesico ─────────────────────────────────────────────────

    internal async Task<IReadOnlyList<TabelaPorteAnestesicoItem>> ListarPortesAnestesicoAsync(
        Guid operadoraId, CancellationToken ct = default)
    {
        return await _db.TabelasPorteAnestesico
            .Where(t => t.OperadoraId == operadoraId)
            .OrderBy(t => t.PorteLetra)
            .Select(t => new TabelaPorteAnestesicoItem(
                t.Id, t.PorteLetra, t.ValorEnfermaria, t.ValorApartamento,
                t.ValorAmbulatorial, t.AtualizadoEm))
            .ToListAsync(ct);
    }

    internal async Task<ImportarTabelaPorteResult> ImportarTabelaUnimedAnestesistaAsync(
        Stream csvStream, Guid operadoraId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId!.Value;
        using var reader = new StreamReader(csvStream, Encoding.UTF8);

        var allLines = new List<string>();
        string? lida;
        while ((lida = await reader.ReadLineAsync(ct)) is not null)
        {
            allLines.Add(lida);
        }

        // skip 8 header lines + 1 data-header (line 9) = skip first 9 lines
        const int LinesToSkip = 9;
        var erros = new List<ImportarCsvErro>();

        var parsedRows = new List<(int LineNum, string CodigoTuss, string PorteLetra, decimal ValEnf, decimal ValAp)>();

        for (var i = LinesToSkip; i < allLines.Count; i++)
        {
            var lineNum = i + 1;
            var line = allLines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var cols = SplitCsvLine(line);
            if (cols.Length < 7)
            {
                erros.Add(new ImportarCsvErro(lineNum, "Linha com colunas insuficientes."));
                continue;
            }

            var codigoTuss = cols[0].Trim();
            var porteLetra = cols[6].Trim().ToUpperInvariant();
            var valEnfStr = cols[4].Trim().Replace(',', '.');
            var valApStr = cols[5].Trim().Replace(',', '.');

            if (string.IsNullOrEmpty(codigoTuss))
            {
                erros.Add(new ImportarCsvErro(lineNum, "CodigoTuss vazio."));
                continue;
            }

            if (string.IsNullOrEmpty(porteLetra) || porteLetra.Length != 1)
            {
                erros.Add(new ImportarCsvErro(lineNum, $"PorteLetra inválido: '{porteLetra}'."));
                continue;
            }

            if (!decimal.TryParse(valEnfStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valEnf) || valEnf < 0)
            {
                erros.Add(new ImportarCsvErro(lineNum, $"ValorEnfermaria inválido: '{cols[4]}'."));
                continue;
            }

            if (!decimal.TryParse(valApStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valAp) || valAp < 0)
            {
                erros.Add(new ImportarCsvErro(lineNum, $"ValorApartamento inválido: '{cols[5]}'."));
                continue;
            }

            parsedRows.Add((lineNum, codigoTuss, porteLetra, valEnf, valAp));
        }

        // upsert TabelaPorteAnestesico — one record per distinct PorteLetra
        var distinctPortes = parsedRows
            .GroupBy(r => r.PorteLetra)
            .Select(g => g.First())
            .ToList();

        var porteLetras = distinctPortes.Select(r => r.PorteLetra).ToList();
        var existingPortes = await _db.TabelasPorteAnestesico
            .Where(t => t.OperadoraId == operadoraId && porteLetras.Contains(t.PorteLetra))
            .ToDictionaryAsync(t => t.PorteLetra, ct);

        var portesAtualizados = 0;
        foreach (var row in distinctPortes)
        {
            if (existingPortes.TryGetValue(row.PorteLetra, out var existing))
            {
                existing.Atualizar(row.ValEnf, row.ValAp);
            }
            else
            {
                _db.TabelasPorteAnestesico.Add(
                    TabelaPorteAnestesico.Create(tenantId, operadoraId, row.PorteLetra, row.ValEnf, row.ValAp));
            }

            portesAtualizados++;
        }

        await _db.SaveChangesAsync(ct);

        // track which distinct TUSS from CSV exist in Procedimentos
        var distinctTuss = parsedRows.Select(r => r.CodigoTuss).Distinct().ToList();
        var foundTuss = await _db.Procedimentos
            .Where(p => distinctTuss.Contains(p.CodigoTuss))
            .Select(p => p.CodigoTuss)
            .ToHashSetAsync(ct);

        var procedimentosAtualizados = foundTuss.Count;
        var procedimentosNaoEncontrados = distinctTuss
            .Where(t => !foundTuss.Contains(t))
            .ToList();

        return new ImportarTabelaPorteResult(
            portesAtualizados,
            procedimentosAtualizados,
            procedimentosNaoEncontrados,
            erros);
    }

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var sb = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return [.. fields];
    }
}
