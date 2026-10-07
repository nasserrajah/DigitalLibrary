using Library.Domain.Entities;
using Library.Domain.Interfaces;
using Library.Infrastructure.Email;
using Library.Infrastructure.Identity;
using Library.Infrastructure.Persistence;
using Library.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(cfg.GetConnectionString("Default") ?? "Data Source=library.db"));

        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 8;
            opt.User.RequireUniqueEmail = true;
            opt.SignIn.RequireConfirmedEmail = false; // flip in prod
            opt.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(cfg.GetSection("Jwt"));
        services.Configure<GoogleAuthSettings>(cfg.GetSection("Google"));
        services.Configure<FacebookAuthSettings>(cfg.GetSection("Facebook"));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, NullEmailService>();

        services.AddHttpClient<FacebookAuthProvider>();
        services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>(); // default google
        services.AddScoped<FacebookAuthProvider>();

        return services;
    }
}