// IBookService.cs
using Library.Application.Common;
using Library.Application.DTOs.Auth;

namespace Library.Application.Interfaces;
public interface IBookService
{
    Task<PagedResult<BookSummaryDto>> GetPagedAsync(PaginationQuery q, Guid? categoryId, CancellationToken ct = default);
    Task<PagedResult<BookSummaryDto>> SearchAsync(string query, PaginationQuery q, CancellationToken ct = default);
    Task<Result<BookDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<BookDto>> CreateAsync(CreateBookRequest req, CancellationToken ct = default);
    Task<Result<BookDto>> UpdateAsync(Guid id, UpdateBookRequest req, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<BookDto>> UploadFileAsync(Guid id, Stream stream, string fileName, CancellationToken ct = default);
    Task<Result<BookDto>> UploadCoverAsync(Guid id, Stream stream, string fileName, CancellationToken ct = default);
    Task<Result<(Stream, string, string)>> DownloadAsync(Guid id, CancellationToken ct = default);
}