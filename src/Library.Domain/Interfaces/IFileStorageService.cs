namespace Library.Domain.Interfaces;

public record FileUploadResult(string StorageKey, long Size, string OriginalName);

public interface IFileStorageService
{
    Task<FileUploadResult> SaveAsync(Stream stream, string originalFileName, string folder, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    string GetPublicUrl(string storageKey);
}