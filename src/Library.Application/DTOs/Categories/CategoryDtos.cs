namespace Library.Application.DTOs.Categories;

public record CategoryDto(Guid Id, string Name, string? Description, string? Image,
    bool IsActive, int BookCount, DateTime CreatedAt);

public record CategorySummaryDto(Guid Id, string Name, string? Image);

public record CreateCategoryRequest(string Name, string? Description);
public record UpdateCategoryRequest(string Name, string? Description, bool IsActive);