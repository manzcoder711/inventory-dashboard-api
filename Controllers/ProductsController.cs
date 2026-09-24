using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryApi.DTOs;
using InventoryApi.Services;

namespace InventoryApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts(CancellationToken ct)
    {
        return await _productService.GetAllAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return product is null ? NotFound() : product;
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(CreateProductRequest request, CancellationToken ct)
    {
        var (result, response) = await _productService.CreateAsync(request, ct);

        if (result == ProductWriteResult.DuplicateSku)
        {
            return Conflict(new { message = $"SKU '{request.Sku}' is already in use." });
        }

        return CreatedAtAction(nameof(GetProduct), new { id = response!.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var result = await _productService.UpdateAsync(id, request, ct);

        return result switch
        {
            ProductWriteResult.NotFound => NotFound(),
            ProductWriteResult.DuplicateSku => Conflict(new { message = $"SKU '{request.Sku}' is already in use." }),
            _ => NoContent(),
        };
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var deleted = await _productService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
