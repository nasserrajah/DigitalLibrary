using AutoMapper;
using Library.Application.DTOs.Auth;
using Library.Application.DTOs.Books;
using Library.Application.DTOs.Categories;
using Library.Application.DTOs.Favorites;
using Library.Application.DTOs.Library;
using Library.Application.DTOs.Notifications;
using Library.Domain.Entities;

namespace Library.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ApplicationUser, UserDto>()
            .ForCtorParam("Provider", o => o.MapFrom(s => s.Provider.ToString()));

        CreateMap<Book, BookDto>()
            .ForCtorParam("CategoryName", o => o.MapFrom(s => s.Category != null ? s.Category.Name : null));
        CreateMap<Book, BookSummaryDto>()
            .ForCtorParam("CategoryName", o => o.MapFrom(s => s.Category != null ? s.Category.Name : null));

        CreateMap<Category, CategoryDto>()
            .ForCtorParam("BookCount", o => o.MapFrom(s => s.Books.Count));
        CreateMap<Category, CategorySummaryDto>();

        CreateMap<Favorite, FavoriteDto>()
            .ForCtorParam("BookId",   o => o.MapFrom(s => s.BookId))
            .ForCtorParam("Title",    o => o.MapFrom(s => s.Book.Title))
            .ForCtorParam("Author",   o => o.MapFrom(s => s.Book.Author))
            .ForCtorParam("CoverImage", o => o.MapFrom(s => s.Book.CoverImage))
            .ForCtorParam("CreatedAt", o => o.MapFrom(s => s.CreatedAt));

        CreateMap<UserLibrary, LibraryItemDto>()
            .ForCtorParam("Id", o => o.MapFrom(s => s.Id))
            .ForCtorParam("BookId", o => o.MapFrom(s => s.BookId))
            .ForCtorParam("Title", o => o.MapFrom(s => s.Book.Title))
            .ForCtorParam("Author", o => o.MapFrom(s => s.Book.Author))
            .ForCtorParam("CoverImage", o => o.MapFrom(s => s.Book.CoverImage))
            .ForCtorParam("FileSize", o => o.MapFrom(s => s.FileSize))
            .ForCtorParam("Format", o => o.MapFrom(s => s.Book.Format))
            .ForCtorParam("AddedAt", o => o.MapFrom(s => s.CreatedAt));

        CreateMap<Notification, NotificationDto>();
    }
}