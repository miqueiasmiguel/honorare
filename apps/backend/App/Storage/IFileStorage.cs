namespace App.Storage;

internal interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default);
    Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

internal sealed record FileStorageObject(byte[] Content, string ContentType);
