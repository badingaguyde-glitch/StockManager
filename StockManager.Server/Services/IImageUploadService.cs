namespace StockManager.Server.Services;

public interface IImageUploadService
{
    Task<ImageUploadResult> UploadImageAsync(
    IFormFile file,
    string folder = "stock-manager/products",
    CancellationToken cancellationToken = default);
    Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default);

    string GetOptimizedImageUrl(string? imageUrl, int width = 300, int height = 300, int quality = 80);

    string GetThumbnailUrl(string? imageUrl, int size = 100);
}

public class ImageUploadResult
{
    public bool Success { get; set; }
    public string? ImageUrl { get; set; }
    public string? PublicId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
