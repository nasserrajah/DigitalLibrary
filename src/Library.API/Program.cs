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

// ---------- Logging ----------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: Serilog.RollingInterval.Day)
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

// ✅ Swagger مُفعّل دائماً (للتطوير والاختبار)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Library API v1");
    c.RoutePrefix = "swagger";
});

// ⚠️ على MonsterASP، IIS يتولى HTTPS — لا نُجبر إعادة التوجيه
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------- Seed ----------
using (var scope = app.Services.CreateScope())
{
    try { await DbSeeder.SeedAsync(scope.ServiceProvider); }
    catch (Exception ex) { Log.Fatal(ex, "Seed failed"); }
}

Log.Information("Starting Digital Library API");
app.Run();

public partial class Program { }