using Library.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorageService> _log;

    public LocalFileStorageService(IHostEnvironment env, ILogger<LocalFileStorageService> log)
    {
        _log = log;
        _root = Path.Combine(env.ContentRootPath, "App_Data", "storage");
        Directory.CreateDirectory(_root);
    }

    public async Task<FileUploadResult> SaveAsync(Stream stream, string originalFileName, string folder, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var relKey = Path.Combine(folder, DateTime.UtcNow.ToString("yyyy/MM"), safeName).Replace('\\', '/');
        var fullPath = Path.Combine(_root, relKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fs = File.Create(fullPath);
        await stream.CopyToAsync(fs, ct);
        var size = new FileInfo(fullPath).Length;
        _log.LogInformation("Stored file {Key} ({Size} bytes)", relKey, size);
        return new FileUploadResult(relKey, size, originalFileName);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, storageKey);
        if (!File.Exists(full)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(full));
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, storageKey);
        if (!File.Exists(full)) return Task.FromResult(false);
        File.Delete(full);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_root, storageKey)));

    public string GetPublicUrl(string storageKey) => $"/api/files/{storageKey}";
}