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

    // 🆕 Görsel yükleme alanları
    public IFormFile? ImageFile { get; set; }
    public string? ExistingImageUrl { get; set; }

    // Görsel validation
    public bool ValidateImage()
    {
        if (ImageFile == null)
            return true;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

        if (!allowedExtensions.Contains(fileExtension))
            return false;

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        return ImageFile.Length <= maxFileSize;
    }
}
