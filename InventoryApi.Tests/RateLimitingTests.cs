using System.Net;
using System.Net.Http.Json;
using InventoryApi.Data;
using InventoryApi.DTOs;
using InventoryApi.Middleware;

namespace InventoryApi.Tests;

// A separate test class gets its own ApiFactory (so its own rate-limit counters): the burst of
// logins here can't use up the limit for the login tests in ProductsApiTests.
public sealed class RateLimitingTests : IClassFixture<ApiFactory>
{
    private const string FrontendOrigin = "http://localhost:4200";

    private readonly HttpClient _client;

    public RateLimitingTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_BeyondTheLimit_Returns429WithRetryAfterAndCorsHeaders()
    {
        var attempt = new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = "WrongPassword!" };

        for (var i = 0; i < RateLimitPolicies.AuthPermitLimit; i++)
        {
            var allowed = await _client.PostAsJsonAsync("/api/auth/login", attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(attempt),
        };
        request.Headers.Add("Origin", FrontendOrigin);
        var blocked = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Missing or zero Retry-After.");

        // Without this the browser hides the 429 from the frontend, which then shows
        // "Couldn't reach the server" instead of a rate-limit message.
        Assert.Equal(FrontendOrigin, blocked.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var body = await blocked.Content.ReadFromJsonAsync<MessageBody>();
        Assert.Equal("Too many attempts. Please wait a minute and try again.", body?.Message);
    }

    // Simulates Azure's front end, which appends the real visitor IP to X-Forwarded-For.
    [Fact]
    public async Task Limit_IsPerVisitor_AndCantBeDodgedBySpoofingTheHeader()
    {
        const string visitorA = "203.0.113.10";
        const string visitorB = "203.0.113.20";

        for (var i = 0; i < RateLimitPolicies.AuthPermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginFrom(visitorA));
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginFrom(visitorA));

        // A different visitor is unaffected - the limit isn't one shared, global bucket.
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginFrom(visitorB));

        // Visitor A sends "X-Forwarded-For: <B>" to pose as B; Azure then appends A's real IP.
        // Only the last entry is trusted, so A is still blocked.
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginFrom($"{visitorB}, {visitorA}"));
    }

    private async Task<HttpStatusCode> LoginFrom(string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = "WrongPassword!" }),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await _client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task ProductEndpoints_AreNotRateLimited()
    {
        for (var i = 0; i < RateLimitPolicies.AuthPermitLimit + 5; i++)
        {
            var response = await _client.GetAsync("/api/products");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private sealed record MessageBody(string Message);
}
