using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public class Supplier
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [BsonElement("contactName")]
    public string? ContactName { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("balance")]
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
