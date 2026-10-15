using Library.Infrastructure.ExternalApis.DTOs;

namespace Library.Infrastructure.ExternalApis;

public interface IBookMetadataService
{
    Task<List<BookMetadataDto>> SearchBooksAsync(string query, int limit = 10, CancellationToken ct = default);
    Task<BookMetadataDto?> GetByIsbnAsync(string isbn, CancellationToken ct = default);
    Task<List<BookMetadataDto>> GetBooksBySubjectAsync(string subject, int limit = 20, CancellationToken ct = default);
}
