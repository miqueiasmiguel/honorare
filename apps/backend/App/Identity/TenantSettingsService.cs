using App.Data;
using App.Storage;

namespace App.Identity;

internal sealed record TenantSettings(Guid Id, string Name, bool HasLogo, IReadOnlyList<string> CodigosNaoRecorriveis);

internal sealed class TenantSettingsService(AppDbContext db, ICurrentUser currentUser, IFileStorage storage)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IFileStorage _storage = storage;

    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private static string? DetectImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }

    internal async Task<Result<TenantSettings>> GetSettingsAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        return Result<TenantSettings>.Ok(new TenantSettings(tenant.Id, tenant.Name, tenant.LogoKey is not null, tenant.CodigosNaoRecorriveis));
    }

    internal async Task<Result<TenantSettings>> RenameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<TenantSettings>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (name.Trim().Length > 256)
        {
            return Result<TenantSettings>.Fail(new ValidationError("Nome deve ter no máximo 256 caracteres."));
        }

        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        tenant.Rename(name);
        await _db.SaveChangesAsync(ct);

        return Result<TenantSettings>.Ok(new TenantSettings(tenant.Id, tenant.Name, tenant.LogoKey is not null, tenant.CodigosNaoRecorriveis));
    }

    internal async Task<Result<TenantSettings>> UploadLogoAsync(byte[] content, CancellationToken ct = default)
    {
        if (content.Length == 0)
        {
            return Result<TenantSettings>.Fail(new ValidationError("Arquivo vazio."));
        }

        if (content.Length > MaxLogoBytes)
        {
            return Result<TenantSettings>.Fail(new ValidationError("Logo excede 2 MB."));
        }

        var contentType = DetectImageContentType(content);
        if (contentType is null)
        {
            return Result<TenantSettings>.Fail(new ValidationError("Formato inválido. Use PNG ou JPEG."));
        }

        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        var ext = contentType == "image/png" ? ".png" : ".jpg";
        var key = $"tenants/{tenant.Id}/logo{ext}";

        if (tenant.LogoKey is not null && tenant.LogoKey != key)
        {
            await _storage.DeleteAsync(tenant.LogoKey, ct);
        }

        await _storage.SaveAsync(key, content, contentType, ct);
        tenant.SetLogoKey(key);
        await _db.SaveChangesAsync(ct);

        return Result<TenantSettings>.Ok(new TenantSettings(tenant.Id, tenant.Name, true, tenant.CodigosNaoRecorriveis));
    }

    internal async Task<Result<FileStorageObject>> GetLogoAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<FileStorageObject>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        if (tenant.LogoKey is null)
        {
            return Result<FileStorageObject>.Fail(new NotFoundError("Logo não encontrada."));
        }

        var obj = await _storage.GetAsync(tenant.LogoKey, ct);
        if (obj is null)
        {
            return Result<FileStorageObject>.Fail(new NotFoundError("Logo não encontrada."));
        }

        return Result<FileStorageObject>.Ok(obj);
    }

    internal async Task<Result> DeleteLogoAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result.Fail(new NotFoundError("Tenant não encontrado."));
        }

        if (tenant.LogoKey is not null)
        {
            await _storage.DeleteAsync(tenant.LogoKey, ct);
            tenant.ClearLogoKey();
            await _db.SaveChangesAsync(ct);
        }

        return Result.Ok();
    }

    internal async Task<Result<TenantSettings>> AtualizarCodigosNaoRecorriveisAsync(
        IReadOnlyList<string> codigos, CancellationToken ct = default)
    {
        var normalizados = codigos.Select(c => (c ?? string.Empty).Trim())
            .Where(c => c.Length > 0).ToList();
        if (normalizados.Any(c => !c.All(char.IsAsciiDigit)))
        {
            return Result<TenantSettings>.Fail(
                new ValidationError("Código TUSS deve conter apenas dígitos."));
        }

        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        tenant.DefinirCodigosNaoRecorriveis(normalizados);
        await _db.SaveChangesAsync(ct);
        return Result<TenantSettings>.Ok(new TenantSettings(
            tenant.Id, tenant.Name, tenant.LogoKey is not null, tenant.CodigosNaoRecorriveis));
    }
}
