using System.Net;
using System.Text.Json;
using Library.Application.Common;

namespace Library.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    { _next = next; _log = log; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";
            var body = ApiResponse<object>.Fail("An unexpected error occurred.", "Internal server error.");
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}