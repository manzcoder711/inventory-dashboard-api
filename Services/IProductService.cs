using InventoryApi.DTOs;

namespace InventoryApi.Services;

public enum ProductWriteResult
{
    Success,
    NotFound,
    DuplicateSku,
}

public interface IProductService
{
    Task<List<ProductResponse>> GetAllAsync(CancellationToken ct);
    Task<ProductResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<(ProductWriteResult Result, ProductResponse? Product)> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<ProductWriteResult> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
