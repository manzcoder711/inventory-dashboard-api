using System.Linq.Expressions;
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

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
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
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);

        return new ProductResponse
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
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var existing = await _context.Products.FindAsync(new object?[] { id }, ct);
        if (existing == null)
        {
            return false;
        }

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Sku = request.Sku;
        existing.Price = request.Price;
        existing.QuantityInStock = request.QuantityInStock;
        existing.Category = request.Category;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} updated", id);

        return true;
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
}
