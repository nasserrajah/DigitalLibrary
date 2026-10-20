using Library.Infrastructure.Services;
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
using Library.Infrastructure.TTS;

namespace Library.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration cfg)
    {
        // ═══════════════════════ Database ═══════════════════════
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(cfg.GetConnectionString("Default")
                ?? "Data Source=library.db"));

        // ═══════════════════════ Identity ═══════════════════════
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

        // ═══════════════════════ Settings ═══════════════════════
        services.Configure<JwtSettings>(cfg.GetSection("Jwt"));
        services.Configure<GoogleAuthSettings>(cfg.GetSection("Google"));
        services.Configure<FacebookAuthSettings>(cfg.GetSection("Facebook"));

        // ═══════════════════════ Core Services ═══════════════════════
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, SmtpEmailService>();

        // ═══════════════════════ External Auth Providers ═══════════════════════
        // Google — uses Google.Apis.Auth (no HttpClient needed)
        services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();

        // Facebook — needs HttpClient (typed client)
        services.AddHttpClient<FacebookAuthProvider>();
        services.AddScoped<IExternalAuthProvider>(sp =>
            sp.GetRequiredService<FacebookAuthProvider>());

        // ═══════════════════════ TTS ═══════════════════════
        // Hakim AI: مجاني، بدون بطاقة ائتمان، يدعم العربية عبر REST API.
        // NullTtsProvider محفوظ كخيار احتياطي إذا أردت تعطيل التوليد مؤقتاً.
        services.AddHttpClient<HakimTtsProvider>();
        services.AddScoped<ITextToSpeechProvider, HakimTtsProvider>();

        // ═══════════════════════ Audio System ═══════════════════════
        services.AddScoped<IAudioService, AudioService>();

        return services;
    }
}