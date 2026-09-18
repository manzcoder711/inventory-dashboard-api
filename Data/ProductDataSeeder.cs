using InventoryApi.Models;

namespace InventoryApi.Data;

/// <summary>
/// Seeds realistic starter inventory only when the table is completely empty.
/// Deliberately not per-SKU idempotent - a visitor deleting an individual product
/// (accepted per Story 1.1) should stay deleted, not get resurrected on restart.
/// This only guards against the list going permanently empty after a full wipe.
/// </summary>
public static class ProductDataSeeder
{
    public static void SeedProducts(InventoryDbContext context)
    {
        if (context.Products.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;

        context.Products.AddRange(
            new Product { Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Sku = "WM-1001", Price = 19.99m, QuantityInStock = 80, Category = "Electronics", CreatedAt = now },
            new Product { Name = "Mechanical Keyboard", Description = "Tactile switches, full-size layout", Sku = "KB-2001", Price = 79.99m, QuantityInStock = 45, Category = "Electronics", CreatedAt = now },
            new Product { Name = "USB-C Hub", Description = "7-in-1 hub with HDMI and SD card reader", Sku = "HUB-3001", Price = 34.99m, QuantityInStock = 5, Category = "Electronics", CreatedAt = now },
            new Product { Name = "1080p Webcam", Description = "Autofocus webcam with built-in mic", Sku = "CAM-9001", Price = 49.99m, QuantityInStock = 25, Category = "Electronics", CreatedAt = now },
            new Product { Name = "Bluetooth Speaker", Description = "Portable speaker, 12-hour battery", Sku = "SPK-1101", Price = 59.99m, QuantityInStock = 0, Category = "Electronics", CreatedAt = now },
            new Product { Name = "Office Chair", Description = "Adjustable lumbar support, mesh back", Sku = "CHR-4001", Price = 189.99m, QuantityInStock = 12, Category = "Furniture", CreatedAt = now },
            new Product { Name = "Standing Desk", Description = "Electric height adjustment, 48-inch top", Sku = "DSK-5001", Price = 349.99m, QuantityInStock = 3, Category = "Furniture", CreatedAt = now },
            new Product { Name = "Notebook 12-Pack", Description = "Ruled, 80 pages each", Sku = "NB-6001", Price = 8.99m, QuantityInStock = 200, Category = "Office Supplies", CreatedAt = now },
            new Product { Name = "Stapler", Description = "Standard desktop stapler", Sku = "STP-7001", Price = 6.49m, QuantityInStock = 2, Category = "Office Supplies", CreatedAt = now },
            new Product { Name = "LED Desk Lamp", Description = "Dimmable, USB charging port", Sku = "LMP-8001", Price = 24.99m, QuantityInStock = 60, Category = "Office Supplies", CreatedAt = now }
        );

        context.SaveChanges();
    }
}
