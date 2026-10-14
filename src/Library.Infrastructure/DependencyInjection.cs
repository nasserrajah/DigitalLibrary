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
            opt.SignIn.RequireConfirmedEmail = false;
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
        services.AddScoped<IEmailService, SmtpEmailService>();
        
        services.AddHttpClient<Library.Infrastructure.ExternalApis.IBookMetadataService,
                      Library.Infrastructure.ExternalApis.OpenLibraryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

        // ═══════════ External Providers ═══════════
        // Google — يستخدم Google.Apis.Auth (لا يحتاج HttpClient)
        services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();

        // Facebook — يحتاج HttpClient عبر typed client
        services.AddHttpClient<FacebookAuthProvider>();
        services.AddScoped<IExternalAuthProvider>(sp =>
            sp.GetRequiredService<FacebookAuthProvider>());

        return services;
    }
}