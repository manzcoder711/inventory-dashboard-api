using System.Net;
using InventoryApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApi.Tests;

// Its own ApiFactory, because the test breaks the database.
public sealed class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_AnswersWithoutLoginEvenWhenTheDatabaseIsBroken()
    {
        var client = _factory.CreateClient(); // starts the app (and its seeding) first

        // Stands in for a paused database: any endpoint that queried it would now fail.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Database.ExecuteSqlRawAsync("DROP TABLE Users");
            await context.Database.ExecuteSqlRawAsync("DROP TABLE Products");
        }

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        SecurityHeaderAssertions.AssertSecurityHeaders(response);
    }
}
