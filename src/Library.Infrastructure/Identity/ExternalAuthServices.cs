using System.Net.Http.Json;              // ⬅️ السطر الجديد
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Library.Infrastructure.Identity;

public record ExternalUserInfo(string Email, string FullName, string ProviderId);

public interface IExternalAuthProvider
{
    Task<ExternalUserInfo?> ValidateAsync(string token, CancellationToken ct = default);
}

public class GoogleAuthProvider : IExternalAuthProvider
{
    private readonly GoogleAuthSettings _settings;
    public GoogleAuthProvider(IOptions<GoogleAuthSettings> s) => _settings = s.Value;

    public async Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _settings.ClientId } });
            return new ExternalUserInfo(payload.Email, payload.Name ?? payload.Email, payload.Subject);
        }
        catch { return null; }
    }
}

public class FacebookAuthProvider : IExternalAuthProvider
{
    private readonly HttpClient _http;
    private readonly FacebookAuthSettings _settings;
    public FacebookAuthProvider(HttpClient http, IOptions<FacebookAuthSettings> s)
    { _http = http; _settings = s.Value; }

    public async Task<ExternalUserInfo?> ValidateAsync(string accessToken, CancellationToken ct = default)
    {
        var url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<FbUser>(cancellationToken: ct);
        if (json?.Email is null) return null;
        return new ExternalUserInfo(json.Email, json.Name ?? json.Email, json.Id);
    }

    private record FbUser(string Id, string? Name, string? Email);
}