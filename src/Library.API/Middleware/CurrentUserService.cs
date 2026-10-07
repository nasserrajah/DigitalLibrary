using System.Security.Claims;
using Library.Domain.Constants;
using Library.Domain.Interfaces;

namespace Library.API.Middleware;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) => _http = http;
    private ClaimsPrincipal? Principal => _http.HttpContext?.User;
    public Guid? UserId
    {
        get
        {
            var s = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(s, out var id) ? id : null;
        }
    }
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin => Principal?.IsInRole(RoleNames.Admin) ?? false;
}