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
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "*" })
    .AllowAnyHeader().AllowAnyMethod()));

// ---------- Swagger ----------
builder.Services.AddSwaggerWithJwt();

// ---------- Build ----------
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digital Library API v1"));
}

app.UseHttpsRedirection();
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

public partial class Program { } // for integration tests