using System.ComponentModel.DataAnnotations;

namespace InventoryApi.DTOs;

public class UpdateProductRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be 0 or greater.")]
    public int QuantityInStock { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }
}
