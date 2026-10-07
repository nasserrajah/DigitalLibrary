// ILibraryService.cs

using Library.Application.Common;
using Library.Application.DTOs.Auth;

namespace Library.Application.Interfaces;
public interface ILibraryService
{
    Task<PagedResult<LibraryItemDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default);
    Task<Result<LibraryItemDto>> AddAsync(Guid userId, AddToLibraryRequest req, CancellationToken ct = default);
    Task<Result<LibraryItemDto>> ImportAsync(Guid userId, Stream stream, string fileName, string title, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid userId, Guid libraryItemId, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(Guid userId, Guid libraryItemId, LibraryBookStatus status, CancellationToken ct = default);
}