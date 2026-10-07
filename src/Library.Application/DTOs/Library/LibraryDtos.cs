using Library.Domain.Enums;

namespace Library.Application.DTOs.Library;

public record LibraryItemDto(
    Guid Id, Guid BookId, string Title, string Author, string? CoverImage,
    long FileSize, BookFormat Format, LibraryBookStatus Status,
    bool IsImported, DateTime AddedAt);

public record AddToLibraryRequest(Guid BookId);
public record UpdateLibraryStatusRequest(LibraryBookStatus Status);