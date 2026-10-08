using System.Text;
using FluentValidation.AspNetCore;
using Library.API.Extensions;
using Library.API.Middleware;
using Library.Application;
using Library.Domain.Constants;
using Library.Domain.Interfaces;
using Library.Infrastructure;
using Library.Infrastructure.Identity;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Environment Variables (for MonsterASP / IIS) ----------
builder.Configuration.AddEnvironmentVariables();

// ---------- Logging (safe for IIS) ----------
// على IIS، لا نكتب إلى ملفات — قد تكون المجلدات بدون صلاحيات كتابة
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// ---------- Layers ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---------- Controllers + Validators ----------
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();

// ---------- AuthN / AuthZ ----------
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

// ✅ فحص أن JWT Secret غير فارغ
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT Secret is missing or too short. Set Jwt__Secret environment variable (32+ characters).");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();

// ---------- CORS ----------
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ---------- Swagger ----------
builder.Services.AddSwaggerWithJwt();

// ---------- Build ----------
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// ✅ Swagger مُفعّل دائماً
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Library API v1");
    c.RoutePrefix = "swagger";
});

// ⚠️ على IIS/MonsterASP، IIS يتولى HTTPS — لا نُجبر إعادة التوجيه
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------- Seed (with better error handling) ----------
try
{
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    Log.Information("Database seeded successfully");
}
catch (Exception ex)
{
    // لا نُسقط التطبيق — نكتفي بتسجيل الخطأ
    Log.Error(ex, "Seed failed but app will continue");
}

Log.Information("Starting Digital Library API on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }