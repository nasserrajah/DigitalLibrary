using System.Net;
using System.Net.Http.Json;
using Library.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Library.IntegrationTests;

public class AuthFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public AuthFlowTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Register_Then_Login_Works()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var registerResp = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, "Password@1"));
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password@1"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }
}
