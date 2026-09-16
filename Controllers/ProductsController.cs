using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.DTOs;
using InventoryApi.Models;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly InventoryDbContext _context;

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

    public ProductsController(InventoryDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts(CancellationToken ct)
    {
        var products = await _context.Products
            .AsNoTracking()
            .Select(ProjectToResponse)
            .ToListAsync(ct);

        return products;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id, CancellationToken ct)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ProjectToResponse)
            .FirstOrDefaultAsync(ct);

        if (product == null)
        {
            return NotFound();
        }

        return product;
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(CreateProductRequest request, CancellationToken ct)
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

        var response = new ProductResponse
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

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var existing = await _context.Products.FindAsync(new object?[] { id }, ct);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Sku = request.Sku;
        existing.Price = request.Price;
        existing.QuantityInStock = request.QuantityInStock;
        existing.Category = request.Category;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var product = await _context.Products.FindAsync(new object?[] { id }, ct);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
