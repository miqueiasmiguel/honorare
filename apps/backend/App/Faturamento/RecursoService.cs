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

internal sealed record GuiaNoRecursoDto(
    Guid Id, string Senha, DateOnly DataAtendimento,
    string? BeneficiarioNome, string? BeneficiarioCarteira,
    SituacaoGuia Situacao, int TotalItens);

internal sealed record RecursoDetalheDto(RecursoDto Header, IReadOnlyList<GuiaNoRecursoDto> Guias);

internal sealed record ListarRecursosQuery(
    Guid? OperadoraId, Guid? PrestadorId, int Pagina, int ItensPorPagina);

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

        var guias = await (
            from g in _db.Guias
            join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
            from b in bs.DefaultIfEmpty()
            where g.RecursoId == id
            select new
            {
                g.Id,
                g.Senha,
                g.DataAtendimento,
                g.Situacao,
                BeneficiarioNome = (string?)b.Nome,
                BeneficiarioCarteira = (string?)b.Carteira,
            }).ToListAsync(ct);

        var guiaIds = guias.Select(g => g.Id).ToList();
        var itemCounts = await _db.ItensGuia
            .Where(i => guiaIds.Contains(i.GuiaId))
            .GroupBy(i => i.GuiaId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        var headerDto = new RecursoDto(
            header.Id, header.OperadoraId, header.OperadoraNome,
            header.PrestadorId, header.PrestadorNome, header.PrestadorRegistroProfissional,
            header.Numero, header.DataEmissao, header.Observacao,
            guias.Count, header.CriadoEm);

        var guiaDtos = guias.Select(g => new GuiaNoRecursoDto(
            g.Id, g.Senha, g.DataAtendimento,
            g.BeneficiarioNome, g.BeneficiarioCarteira,
            g.Situacao,
            itemCounts.GetValueOrDefault(g.Id, 0))).ToList();

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
}
