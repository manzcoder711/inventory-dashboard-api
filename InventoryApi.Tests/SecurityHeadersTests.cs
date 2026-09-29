using System.Net;
using System.Net.Http.Json;
using InventoryApi.Data;
using InventoryApi.DTOs;
using InventoryApi.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApi.Tests;

internal static class SecurityHeaderAssertions
{
    public static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.Single() : null;
}

public sealed class SecurityHeadersTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OrdinaryResponse_HasSecurityHeaders()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        SecurityHeaderAssertions.AssertSecurityHeaders(response);
    }

    // A 429 is written by the rate limiter, not a controller - a different code path.
    [Fact]
    public async Task RateLimitedResponse_HasSecurityHeaders()
    {
        var attempt = new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = "WrongPassword!" };
        HttpResponseMessage response;
        var attempts = 0;
        do
        {
            response = await _client.PostAsJsonAsync("/api/auth/login", attempt);
        }
        while (response.StatusCode != HttpStatusCode.TooManyRequests && ++attempts <= RateLimitPolicies.AuthPermitLimit);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        SecurityHeaderAssertions.AssertSecurityHeaders(response);
    }
}

// Its own ApiFactory, because breaking the database would break any other test sharing it.
public sealed class ErrorResponseHeadersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ErrorResponseHeadersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ServerError_StillHasSecurityHeaders()
    {
        var client = _factory.CreateClient(); // starts the app (and its seeding) first

        // Drop a table so the login query throws, and the global exception handler - which
        // clears the response's headers - writes the 500.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Database.ExecuteSqlRawAsync("DROP TABLE Users");
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = DemoDataSeeder.DemoPassword });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        SecurityHeaderAssertions.AssertSecurityHeaders(response);
    }
}
