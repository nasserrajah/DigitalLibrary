using System.Reflection;
using FluentValidation;
using Library.Application.Interfaces;
using Library.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var asm = Assembly.GetExecutingAssembly();
        services.AddAutoMapper(asm);
        services.AddValidatorsFromAssembly(asm);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminStatsService, AdminStatsService>();

        return services;
    }
}