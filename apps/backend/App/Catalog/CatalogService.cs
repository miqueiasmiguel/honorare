using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Catalog;

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
}
