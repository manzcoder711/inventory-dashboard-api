using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.DTOs;
using InventoryApi.Models;

namespace InventoryApi.Services;

public class ProductService : IProductService
{
    private readonly InventoryDbContext _context;
    private readonly ILogger<ProductService> _logger;

    private static readonly Expression<Func<Product, ProductResponse>> ProjectToResponse = p => new ProductResponse
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Sku = p.Sku,
        Price = p.Price,
        QuantityInStock = p.QuantityInStock,
        Category = p.Category,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    public ProductService(InventoryDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ProductResponse>> GetAllAsync(CancellationToken ct)
    {
        return await _context.Products
            .AsNoTracking()
            .Select(ProjectToResponse)
            .ToListAsync(ct);
    }

    public async Task<ProductResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ProjectToResponse)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(ProductWriteResult Result, ProductResponse? Product)> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var skuInUse = await _context.Products.AnyAsync(p => p.Sku == request.Sku, ct);
        if (skuInUse)
        {
            return (ProductWriteResult.DuplicateSku, null);
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Sku = request.Sku,
            Price = request.Price,
            QuantityInStock = request.QuantityInStock,
            Category = request.Category,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Products.Add(product);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateSkuViolation(ex))
        {
            // Another request claimed this SKU between our check and the insert.
            _logger.LogWarning("Duplicate SKU {Sku} rejected by the unique index on create", request.Sku);
            return (ProductWriteResult.DuplicateSku, null);
        }

        _logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);

        return (ProductWriteResult.Success, new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Sku = product.Sku,
            Price = product.Price,
            QuantityInStock = product.QuantityInStock,
            Category = product.Category,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
        });
    }

    public async Task<ProductWriteResult> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var existing = await _context.Products.FindAsync(new object?[] { id }, ct);
        if (existing == null)
        {
            return ProductWriteResult.NotFound;
        }

        var skuInUse = await _context.Products.AnyAsync(p => p.Sku == request.Sku && p.Id != id, ct);
        if (skuInUse)
        {
            return ProductWriteResult.DuplicateSku;
        }

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Sku = request.Sku;
        existing.Price = request.Price;
        existing.QuantityInStock = request.QuantityInStock;
        existing.Category = request.Category;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateSkuViolation(ex))
        {
            _logger.LogWarning("Duplicate SKU {Sku} rejected by the unique index on update of product {ProductId}", request.Sku, id);
            return ProductWriteResult.DuplicateSku;
        }

        _logger.LogInformation("Product {ProductId} updated", id);

        return ProductWriteResult.Success;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var product = await _context.Products.FindAsync(new object?[] { id }, ct);
        if (product == null)
        {
            return false;
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} deleted", id);

        return true;
    }

    // 2601 = duplicate key in a unique index, 2627 = unique constraint violation.
    private static bool IsDuplicateSkuViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } sqlEx
        && sqlEx.Message.Contains("IX_Products_Sku");
}
