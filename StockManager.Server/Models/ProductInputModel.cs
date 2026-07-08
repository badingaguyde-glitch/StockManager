namespace StockManager.Server.Models;

public class ProductInputModel
{
    public string? Id { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public string? CategoryId { get; set; }
    public string? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}
