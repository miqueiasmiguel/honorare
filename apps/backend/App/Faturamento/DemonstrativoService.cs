using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento;

internal sealed record CriarDemonstrativoCommand(
    Guid OperadoraId, string Competencia, DateOnly DataRecebimento, string? Observacao);

internal sealed record AtualizarDemonstrativoCommand(
    Guid OperadoraId, string Competencia, DateOnly DataRecebimento, string? Observacao);

internal sealed record AdicionarItemCommand(
    string Senha, string CodigoTuss, string? Descricao,
    decimal ValorApresentado, decimal ValorPago, string? MotivoGlosa);

internal sealed record ListarDemonstrativosQuery(
    Guid? OperadoraId, string? Competencia, int Pagina, int ItensPorPagina);

internal sealed record ListarDemonstrativosResult(
    IReadOnlyList<DemonstrativoDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record DemonstrativoDto(
    Guid Id, Guid OperadoraId, string OperadoraNome,
    string Competencia, DateOnly DataRecebimento, string? Observacao,
    int TotalItens, int ItensConciliados, DateTimeOffset CriadoEm);

internal sealed record ItemDemonstrativoDto(
    Guid Id, string Senha, string CodigoTuss, string? Descricao,
    decimal ValorApresentado, decimal ValorPago, decimal ValorGlosado,
    string? MotivoGlosa, Guid? ItemGuiaId, bool Conciliado);

internal sealed record DemonstrativoDetalheDto(DemonstrativoDto Header,
    IReadOnlyList<ItemDemonstrativoDto> Itens);

internal sealed class DemonstrativoService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    internal async Task<Result<DemonstrativoDetalheDto>> CriarAsync(
        CriarDemonstrativoCommand cmd, CancellationToken ct = default)
    {
        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<DemonstrativoDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var dem = Demonstrativo.Create(tenantId, cmd.OperadoraId, cmd.Competencia,
            cmd.DataRecebimento, cmd.Observacao);
        _db.Demonstrativos.Add(dem);
        await _db.SaveChangesAsync(ct);

        return await ObterDetalheDtoInternalAsync(dem.Id, ct);
    }

    internal async Task<ListarDemonstrativosResult> ListarAsync(
        ListarDemonstrativosQuery query, CancellationToken ct = default)
    {
        var q = from d in _db.Demonstrativos
                join op in _db.Operadoras on d.OperadoraId equals op.Id
                select new
                {
                    d.Id,
                    d.OperadoraId,
                    OperadoraNome = op.Nome,
                    d.Competencia,
                    d.DataRecebimento,
                    d.Observacao,
                    d.CriadoEm,
                };

        if (query.OperadoraId.HasValue)
        {
            q = q.Where(x => x.OperadoraId == query.OperadoraId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Competencia))
        {
            q = q.Where(x => x.Competencia == query.Competencia.Trim());
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(query.ItensPorPagina, 100);
        var skip = (query.Pagina - 1) * itensPorPagina;

        var pagina = await q
            .OrderByDescending(x => x.DataRecebimento)
            .ThenByDescending(x => x.CriadoEm)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var ids = pagina.Select(x => x.Id).ToList();
        var countsByDem = await _db.ItensDemonstrativo
            .Where(i => ids.Contains(i.DemonstrativoId))
            .GroupBy(i => i.DemonstrativoId)
            .Select(g => new
            {
                DemonstrativoId = g.Key,
                Total = g.Count(),
                Conciliados = g.Count(i => i.ItemGuiaId != null),
            })
            .ToListAsync(ct);

        var countsDict = countsByDem.ToDictionary(x => x.DemonstrativoId);

        var itens = pagina.Select(x =>
        {
            var counts = countsDict.TryGetValue(x.Id, out var c) ? c : null;
            return new DemonstrativoDto(x.Id, x.OperadoraId, x.OperadoraNome,
                x.Competencia, x.DataRecebimento, x.Observacao,
                counts?.Total ?? 0, counts?.Conciliados ?? 0, x.CriadoEm);
        }).ToList();

        return new ListarDemonstrativosResult(itens, total, query.Pagina, query.ItensPorPagina);
    }

    internal async Task<Result<DemonstrativoDetalheDto>> ObterPorIdAsync(
        Guid id, CancellationToken ct = default) =>
        await ObterDetalheDtoInternalAsync(id, ct);

    internal async Task<Result<DemonstrativoDetalheDto>> AtualizarAsync(
        Guid id, AtualizarDemonstrativoCommand cmd, CancellationToken ct = default)
    {
        var dem = await _db.Demonstrativos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dem is null)
        {
            return Result<DemonstrativoDetalheDto>.Fail(new NotFoundError("Demonstrativo não encontrado."));
        }

        var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == cmd.OperadoraId, ct);
        if (operadora is null)
        {
            return Result<DemonstrativoDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
        }

        dem.Atualizar(cmd.OperadoraId, cmd.Competencia, cmd.DataRecebimento, cmd.Observacao);
        await _db.SaveChangesAsync(ct);

        return await ObterDetalheDtoInternalAsync(id, ct);
    }

    internal async Task<Result> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var dem = await _db.Demonstrativos
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dem is null)
        {
            return Result.Fail(new NotFoundError("Demonstrativo não encontrado."));
        }

        var temConciliado = await _db.ItensDemonstrativo
            .AnyAsync(i => i.DemonstrativoId == id && i.ItemGuiaId != null, ct);
        if (temConciliado)
        {
            throw new InvalidOperationException(
                "Não é possível excluir um demonstrativo com itens conciliados.");
        }

        _db.Demonstrativos.Remove(dem);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    internal async Task<Result<DemonstrativoDetalheDto>> AdicionarItemAsync(
        Guid demonstrativoId, AdicionarItemCommand cmd, CancellationToken ct = default)
    {
        var dem = await _db.Demonstrativos.FirstOrDefaultAsync(d => d.Id == demonstrativoId, ct);
        if (dem is null)
        {
            return Result<DemonstrativoDetalheDto>.Fail(new NotFoundError("Demonstrativo não encontrado."));
        }

        var item = ItemDemonstrativo.Create(demonstrativoId, cmd.Senha, cmd.CodigoTuss,
            cmd.Descricao, cmd.ValorApresentado, cmd.ValorPago, cmd.MotivoGlosa);
        _db.ItensDemonstrativo.Add(item);
        await _db.SaveChangesAsync(ct);

        return await ObterDetalheDtoInternalAsync(demonstrativoId, ct);
    }

    internal async Task<Result> RemoverItemAsync(
        Guid demonstrativoId, Guid itemId, CancellationToken ct = default)
    {
        var item = await _db.ItensDemonstrativo
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.DemonstrativoId == demonstrativoId, ct);
        if (item is null)
        {
            return Result.Fail(new NotFoundError("Item não encontrado."));
        }

        if (item.ItemGuiaId != null)
        {
            throw new InvalidOperationException(
                "Não é possível remover um item conciliado.");
        }

        await _db.ItensDemonstrativo
            .Where(i => i.Id == itemId)
            .ExecuteDeleteAsync(ct);
        return Result.Ok();
    }

    private async Task<Result<DemonstrativoDetalheDto>> ObterDetalheDtoInternalAsync(
        Guid demonstrativoId, CancellationToken ct)
    {
        var header = await (from d in _db.Demonstrativos
                            join op in _db.Operadoras on d.OperadoraId equals op.Id
                            where d.Id == demonstrativoId
                            select new
                            {
                                d.Id,
                                d.OperadoraId,
                                OperadoraNome = op.Nome,
                                d.Competencia,
                                d.DataRecebimento,
                                d.Observacao,
                                d.CriadoEm,
                            })
                           .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return Result<DemonstrativoDetalheDto>.Fail(new NotFoundError("Demonstrativo não encontrado."));
        }

        var itens = await _db.ItensDemonstrativo
            .Where(i => i.DemonstrativoId == demonstrativoId)
            .Select(i => new ItemDemonstrativoDto(
                i.Id, i.Senha, i.CodigoTuss, i.Descricao,
                i.ValorApresentado, i.ValorPago, i.ValorGlosado,
                i.MotivoGlosa, i.ItemGuiaId, i.ItemGuiaId != null))
            .ToListAsync(ct);

        var totalItens = itens.Count;
        var itensConciliados = itens.Count(i => i.Conciliado);

        var dto = new DemonstrativoDto(
            header.Id, header.OperadoraId, header.OperadoraNome,
            header.Competencia, header.DataRecebimento, header.Observacao,
            totalItens, itensConciliados, header.CriadoEm);

        return Result<DemonstrativoDetalheDto>.Ok(new DemonstrativoDetalheDto(dto, itens));
    }
}
