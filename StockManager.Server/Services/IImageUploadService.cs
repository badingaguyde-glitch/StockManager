namespace StockManager.Server.Services;

/// <summary>
/// Bulut tabanlı görsel yükleme servisi
/// Cloudinary ile entegrasyon sağlar
/// </summary>
public interface IImageUploadService
{
    /// <summary>
    /// Dosyayı Cloudinary'ye yükler ve URL döndürür
    /// </summary>
    /// <param name="file">Yüklenecek dosya</param>
    /// <param name="folder">Cloudinary klasörü (product, supplier vb.)</param>
    /// <param name="cancellationToken">İptal token'ı</param>
    /// <returns>Yüklenen görselin URL'i</returns>
    Task<ImageUploadResult> UploadImageAsync(
        IFormFile file,
        string folder = "stock-manager/products",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cloudinary'den görsel siler
    /// </summary>
    /// <param name="publicId">Cloudinary Public ID</param>
    /// <param name="cancellationToken">İptal token'ı</param>
    /// <returns>Silme işleminin başarı durumu</returns>
    Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Görsel URL'sini optimize eder (resize, format vb.)
    /// </summary>
    /// <param name="imageUrl">Orijinal URL</param>
    /// <param name="width">Genişlik (px)</param>
    /// <param name="height">Yükseklik (px)</param>
    /// <param name="quality">Kalite (1-100)</param>
    /// <returns>Optimize edilmiş URL</returns>
    string GetOptimizedImageUrl(string? imageUrl, int width = 300, int height = 300, int quality = 80);

    /// <summary>
    /// Thumbnail URL'sini döndürür
    /// </summary>
    /// <param name="imageUrl">Orijinal URL</param>
    /// <returns>Thumbnail URL'si</returns>
    string GetThumbnailUrl(string? imageUrl, int size = 100);
}

/// <summary>
/// Görsel yükleme sonuç modeli
/// </summary>
public class ImageUploadResult
{
    public bool Success { get; set; }
    public string? ImageUrl { get; set; }
    public string? PublicId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
