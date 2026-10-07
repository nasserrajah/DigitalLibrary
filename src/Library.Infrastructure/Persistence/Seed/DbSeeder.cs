using Library.Domain.Constants;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    private static readonly string[] DefaultCategories =
    {
        "Programming","Technology","Science","Mathematics","History",
        "Literature","Education","Business","Self Development","Psychology",
        "Languages","Religion","Health","Sports","Fiction","Novels","Children"
    };

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        await db.Database.MigrateAsync();

        foreach (var role in RoleNames.All)
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole<Guid>(role));

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(DefaultCategories.Select(name => new Category { Name = name, IsActive = true }));
            await db.SaveChangesAsync();
            log.LogInformation("Seeded {Count} categories", DefaultCategories.Length);
        }

        var adminEmail = cfg["Seed:AdminEmail"] ?? "admin@library.local";
        var adminPass  = cfg["Seed:AdminPassword"] ?? "Admin@12345";

        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true
            };
            var res = await userMgr.CreateAsync(admin, adminPass);
            if (res.Succeeded) await userMgr.AddToRoleAsync(admin, RoleNames.Admin);
            else log.LogError("Failed to seed admin: {Errors}", string.Join(",", res.Errors.Select(e => e.Description)));
        }
    }
}