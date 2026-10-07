using Library.Domain.Enums;

namespace Library.Application.DTOs.Books;

public record BookDto(
    Guid Id, string Title, string? Description, string Author, string? Publisher,
    DateTime? PublicationDate, string? ISBN, string Language, string? CoverImage,
    string? FileName, long FileSize, BookFormat Format, bool IsPublished,
    Guid CategoryId, string? CategoryName, DateTime CreatedAt);

public record BookSummaryDto(
    Guid Id, string Title, string Author, string? CoverImage,
    long FileSize, BookFormat Format, Guid CategoryId, string? CategoryName);

public record CreateBookRequest(
    string Title, string? Description, string Author, string? Publisher,
    DateTime? PublicationDate, string? ISBN, string Language, BookFormat Format,
    Guid CategoryId, bool IsPublished = true);

public record UpdateBookRequest(
    string Title, string? Description, string Author, string? Publisher,
    DateTime? PublicationDate, string? ISBN, string Language, BookFormat Format,
    Guid CategoryId, bool IsPublished);