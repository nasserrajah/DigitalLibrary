using Library.Infrastructure.ExternalApis.DTOs;

namespace Library.Infrastructure.ExternalApis;

public interface IGutendexService
{
    Task<List<BookMetadataDto>> SearchAsync(string query, int limit = 20, CancellationToken ct = default);
    Task<List<BookMetadataDto>> GetByTopicAsync(string topic, int limit = 20, CancellationToken ct = default);
    Task<BookMetadataDto?> GetByIdAsync(int gutenbergId, CancellationToken ct = default);
}