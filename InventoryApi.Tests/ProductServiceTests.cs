using InventoryApi.DTOs;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryApi.Tests;

public sealed class ProductServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private ProductService CreateService() =>
        new(_db.CreateContext(), NullLogger<ProductService>.Instance);

    private async Task<Product> SeedAsync(string sku, string name = "Seeded Product")
    {
        await using var context = _db.CreateContext();
        var product = new Product
        {
            Name = name,
            Sku = sku,
            Price = 10m,
            QuantityInStock = 5,
            CreatedAt = DateTime.UtcNow,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static CreateProductRequest CreateRequest(string sku) => new()
    {
        Name = "New Product",
        Sku = sku,
        Price = 19.99m,
        QuantityInStock = 3,
        Category = "Test",
    };

    private static UpdateProductRequest UpdateRequest(string sku) => new()
    {
        Name = "Renamed Product",
        Sku = sku,
        Price = 25m,
        QuantityInStock = 7,
        Category = "Updated",
    };

    // ---- Create ----

    [Fact]
    public async Task CreateAsync_WithUniqueSku_SavesAndReturnsProduct()
    {
        var before = DateTime.UtcNow;

        var (result, product) = await CreateService().CreateAsync(CreateRequest("NEW-001"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.Success, result);
        Assert.NotNull(product);
        Assert.True(product.Id > 0);
        Assert.InRange(product.CreatedAt, before, DateTime.UtcNow);
        Assert.Null(product.UpdatedAt);

        await using var context = _db.CreateContext();
        var saved = await context.Products.SingleAsync();
        Assert.Equal("DELIBERATELY-WRONG", saved.Sku);
        Assert.Equal(19.99m, saved.Price);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_ReturnsDuplicateSkuAndInsertsNothing()
    {
        await SeedAsync("DUP-001");

        var (result, product) = await CreateService().CreateAsync(CreateRequest("DUP-001"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.DuplicateSku, result);
        Assert.Null(product);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.Products.CountAsync());
    }

    // ---- Read ----

    [Fact]
    public async Task GetAllAsync_ReturnsEveryProduct()
    {
        await SeedAsync("A-001");
        await SeedAsync("B-001");

        var products = await CreateService().GetAllAsync(CancellationToken.None);

        Assert.Equal(["A-001", "B-001"], products.Select(p => p.Sku).Order());
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsProduct()
    {
        var seeded = await SeedAsync("GET-001", name: "Findable");

        var product = await CreateService().GetByIdAsync(seeded.Id, CancellationToken.None);

        Assert.NotNull(product);
        Assert.Equal("Findable", product.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingId_ReturnsNull()
    {
        var product = await CreateService().GetByIdAsync(999, CancellationToken.None);

        Assert.Null(product);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateAsync_WithExistingProduct_SavesChangesAndSetsUpdatedAt()
    {
        var seeded = await SeedAsync("UPD-001");

        var result = await CreateService().UpdateAsync(seeded.Id, UpdateRequest("UPD-002"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.Success, result);
        await using var context = _db.CreateContext();
        var saved = await context.Products.SingleAsync();
        Assert.Equal("Renamed Product", saved.Name);
        Assert.Equal("UPD-002", saved.Sku);
        Assert.Equal(7, saved.QuantityInStock);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingId_ReturnsNotFound()
    {
        var result = await CreateService().UpdateAsync(999, UpdateRequest("ANY-001"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.NotFound, result);
    }

    [Fact]
    public async Task UpdateAsync_ToAnotherProductsSku_ReturnsDuplicateSkuAndChangesNothing()
    {
        await SeedAsync("TAKEN-001", name: "Owner");
        var target = await SeedAsync("MINE-001", name: "Target");

        var result = await CreateService().UpdateAsync(target.Id, UpdateRequest("TAKEN-001"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.DuplicateSku, result);
        await using var context = _db.CreateContext();
        var unchanged = await context.Products.SingleAsync(p => p.Id == target.Id);
        Assert.Equal("MINE-001", unchanged.Sku);
        Assert.Equal("Target", unchanged.Name);
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnSku_Succeeds()
    {
        // The uniqueness check must exclude the product being edited, or re-saving
        // a product without changing its SKU would be rejected as a duplicate.
        var seeded = await SeedAsync("SAME-001");

        var result = await CreateService().UpdateAsync(seeded.Id, UpdateRequest("SAME-001"), CancellationToken.None);

        Assert.Equal(ProductWriteResult.Success, result);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesProduct()
    {
        var seeded = await SeedAsync("DEL-001");

        var deleted = await CreateService().DeleteAsync(seeded.Id, CancellationToken.None);

        Assert.True(deleted);
        await using var context = _db.CreateContext();
        Assert.False(await context.Products.AnyAsync());
    }

    [Fact]
    public async Task DeleteAsync_WithMissingId_ReturnsFalse()
    {
        var deleted = await CreateService().DeleteAsync(999, CancellationToken.None);

        Assert.False(deleted);
    }
}
