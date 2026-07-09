using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockManager.Server.Services;

/// <summary>
/// Cloudinary için görsel yükleme servisi
/// </summary>
public class CloudinaryImageService : IImageUploadService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageService> _logger;
    private readonly CloudinarySettings _settings;

    public CloudinaryImageService(
        IOptions<CloudinarySettings> options,
        ILogger<CloudinaryImageService> logger)
    {
        _settings = options.Value;
        _logger = logger;

        var account = new Account(
            _settings.CloudName,
            _settings.ApiKey,
            _settings.ApiSecret);

        _cloudinary = new Cloudinary(account);
    }

    public async Task<ImageUploadResult> UploadImageAsync(
        IFormFile file,
        string folder = "stock-manager/products",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Dosya doğrulaması
            if (file == null || file.Length == 0)
            {
                return new ImageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Dosya boş veya geçersiz"
                };
            }

            // Dosya türü doğrulaması
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return new ImageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Geçersiz dosya türü. İzin verilen: JPG, PNG, GIF, WebP"
                };
            }

            // Maksimum dosya boyutu: 5MB
            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return new ImageUploadResult
                {
                    Success = false,
                    ErrorMessage = "Dosya boyutu 5MB'ı aşamaz"
                };
            }

            // Dosyayı stream'e dönüştür
            using var stream = file.OpenReadStream();

            // Cloudinary'ye yükle
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                PublicId = Guid.NewGuid().ToString(),
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError($"Cloudinary yükleme hatası: {uploadResult.Error.Message}");
                return new ImageUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Yükleme başarısız: {uploadResult.Error.Message}"
                };
            }

            _logger.LogInformation($"Görsel başarıyla yüklendi: {uploadResult.PublicId}");

            return new ImageUploadResult
            {
                Success = true,
                ImageUrl = uploadResult.SecureUrl.ToString(),
                PublicId = uploadResult.PublicId,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Görsel yükleme hatası: {ex.Message}");
            return new ImageUploadResult
            {
                Success = false,
                ErrorMessage = "Bir hata oluştu. Lütfen tekrar deneyin."
            };
        }
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return false;

            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
            {
                _logger.LogError($"Cloudinary silme hatası: {result.Error.Message}");
                return false;
            }

            _logger.LogInformation($"Görsel başarıyla silindi: {publicId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Görsel silme hatası: {ex.Message}");
            return false;
        }
    }

    public string GetOptimizedImageUrl(string? imageUrl, int width = 300, int height = 300, int quality = 80)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "/images/placeholder-product.png";

        if (!imageUrl.Contains("cloudinary.com"))
            return imageUrl;

        try
        {
            // Cloudinary URL'sini transform et
            var uri = new Uri(imageUrl);
            var pathParts = uri.PathAndQuery.Split(new[] { "/upload/" }, StringSplitOptions.None);

            if (pathParts.Length < 2)
                return imageUrl;

            // Transformation parametreleri ekle
            var transformation = $"w_{width},h_{height},c_fill,g_auto,q_{quality}";
            var optimizedUrl = $"{pathParts[0]}/upload/{transformation}/{pathParts[1]}";

            return optimizedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError($"URL optimize hatası: {ex.Message}");
            return imageUrl;
        }
    }

    public string GetThumbnailUrl(string? imageUrl, int size = 100)
    {
        return GetOptimizedImageUrl(imageUrl, size, size, 75);
    }
}

/// <summary>
/// Cloudinary ayarları
/// </summary>
public class CloudinarySettings
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int MaxFileSize { get; set; } = 5242880; // 5MB
}
