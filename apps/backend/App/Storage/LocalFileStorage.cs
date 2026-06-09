using Microsoft.Extensions.Options;

namespace App.Storage;

internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _basePath = Path.GetFullPath(options.Value.BasePath);
    }

    public async Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, ct);
    }

    public async Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        var contentType = ContentTypeFromExtension(Path.GetExtension(key));
        return new FileStorageObject(bytes, contentType);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var normalized = key.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_basePath, normalized));
        if (!full.StartsWith(_basePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path traversal detected for key '{key}'.");
        }

        return full;
    }

    private static string ContentTypeFromExtension(string ext) =>
        ext.ToUpperInvariant() switch
        {
            ".PNG" => "image/png",
            ".JPG" or ".JPEG" => "image/jpeg",
            _ => "application/octet-stream",
        };
}
