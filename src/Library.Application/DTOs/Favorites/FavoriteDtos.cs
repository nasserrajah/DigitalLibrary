namespace Library.Application.DTOs.Favorites;

public record FavoriteDto(Guid Id, Guid BookId, string Title, string Author,
    string? CoverImage, DateTime CreatedAt);