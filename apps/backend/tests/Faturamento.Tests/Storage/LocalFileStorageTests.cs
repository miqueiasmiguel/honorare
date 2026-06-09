using App.Storage;
using Microsoft.Extensions.Options;

namespace Faturamento.Tests.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var options = Options.Create(new StorageOptions { BasePath = _tempDir });
        _storage = new LocalFileStorage(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Deve_gravar_e_recuperar_bytes_com_o_mesmo_conteudo_round_trip_Async()
    {
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02 };
        await _storage.SaveAsync("test/file.png", content, "image/png");

        var result = await _storage.GetAsync("test/file.png");

        Assert.NotNull(result);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public async Task Deve_retornar_null_em_GetAsync_quando_a_chave_nao_existe_Async()
    {
        var result = await _storage.GetAsync("inexistente/file.png");

        Assert.Null(result);
    }

    [Fact]
    public async Task Deve_derivar_content_type_image_png_para_chave_png_Async()
    {
        var content = new byte[] { 1, 2, 3 };
        await _storage.SaveAsync("imagens/logo.png", content, "image/png");

        var result = await _storage.GetAsync("imagens/logo.png");

        Assert.NotNull(result);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public async Task Deve_derivar_content_type_image_jpeg_para_chave_jpg_Async()
    {
        var content = new byte[] { 1, 2, 3 };
        await _storage.SaveAsync("imagens/logo.jpg", content, "image/jpeg");

        var result = await _storage.GetAsync("imagens/logo.jpg");

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
    }

    [Fact]
    public async Task Deve_remover_o_arquivo_em_DeleteAsync_e_ser_idempotente_quando_ausente_Async()
    {
        var content = new byte[] { 1, 2, 3 };
        await _storage.SaveAsync("test/removivel.png", content, "image/png");

        await _storage.DeleteAsync("test/removivel.png");

        var result = await _storage.GetAsync("test/removivel.png");
        Assert.Null(result);

        // idempotente: não lança ao deletar de novo
        await _storage.DeleteAsync("test/removivel.png");
    }

    [Fact]
    public async Task Deve_rejeitar_key_com_path_traversal_Async()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _storage.SaveAsync("../../etc/passwd", new byte[] { 1 }, "text/plain"));
    }
}
