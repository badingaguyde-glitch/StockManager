using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Supplier
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("companyName")]
    [Required(ErrorMessage = "Firma adı zorunludur.")]
    public string CompanyName { get; set; } = string.Empty;

    [BsonElement("contactName")]
    public string? ContactName { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("email")]
    [EmailAddress(ErrorMessage = "Geçersiz e-posta adresi.")]
    public string? Email { get; set; }

    [BsonElement("balance")]
    [Range(0, 1000000000, ErrorMessage = "Bakiye 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal Balance { get; set; }

    // 🆕 Yeni alan - Tedarikçi logosu URL'i
    [BsonElement("logoUrl")]
    public string? LogoUrl { get; set; }

    // 🆕 Logo yükleme tarihi
    [BsonElement("logoUploadedAt")]
    public DateTime? LogoUploadedAt { get; set; }

    // 🆕 Cloudinary Logo Public ID
    [BsonElement("cloudinaryLogoPublicId")]
    public string? CloudinaryLogoPublicId { get; set; }
}
