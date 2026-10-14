using Library.Infrastructure.ExternalApis.DTOs;

namespace Library.Infrastructure.ExternalApis;

public interface IBookMetadataService
{
    /// <summary>
    /// البحث العام بـ query (عنوان أو مؤلف)
    /// </summary>
    Task<List<BookMetadataDto>> SearchBooksAsync(
        string query, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// البحث بالـ ISBN
    /// </summary>
    Task<BookMetadataDto?> GetByIsbnAsync(
        string isbn, CancellationToken ct = default);

    /// <summary>
    /// جلب كتب تصنيف معيّن (الموضوع) — يستخدم /subjects/{subject}.json
    /// </summary>
    Task<List<BookMetadataDto>> GetBooksBySubjectAsync(
        string subject, int limit = 20, CancellationToken ct = default);
}