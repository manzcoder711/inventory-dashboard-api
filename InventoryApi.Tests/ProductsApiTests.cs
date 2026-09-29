using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryApi.Data;
using InventoryApi.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApi.Tests;

/// <summary>
/// End-to-end over HTTP: routing, [Authorize], JWT validation and the controllers, as a
/// real visitor would hit them - against SQLite, never Azure SQL.
/// </summary>
public sealed class ProductsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ProductsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void TestHost_UsesSqlite_NotAzureSql()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
    }

    [Fact]
    public void TestHost_DoesNotLoadUserSecrets()
    {
        // User-secrets would replace appsettings.json's REPLACE_ME placeholder with the real
        // connection string. Assert.True rather than Assert.Contains: on failure Contains
        // prints the actual value, which would leak the real secret into the test output.
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("InventoryDb") ?? string.Empty;

        Assert.True(connectionString.Contains("REPLACE_ME"), "User-secrets were loaded into the test host.");
    }

    [Fact]
    public async Task GetProducts_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithGenericMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = "WrongPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageBody>();
        Assert.Equal("Invalid email or password.", body?.Message);
    }

    [Fact]
    public async Task DemoLogin_ThenGetProducts_ReturnsTheSeededProducts()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = DemoDataSeeder.DemoEmail, Password = DemoDataSeeder.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth?.Token));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Contains(products, p => p.Sku == "WM-1001");
    }

    private sealed record MessageBody(string Message);
}
