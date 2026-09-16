using InventoryApi.DTOs;

namespace InventoryApi.Services;

public interface IProductService
{
    Task<List<ProductResponse>> GetAllAsync(CancellationToken ct);
    Task<ProductResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<bool> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
