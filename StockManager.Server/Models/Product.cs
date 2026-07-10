using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("barcode")]
    public string? Barcode { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("purchasePrice")]
    public decimal PurchasePrice { get; set; }

    [BsonElement("salePrice")]
    public decimal SalePrice { get; set; }

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("lowStockThreshold")]
    public int LowStockThreshold { get; set; }

    [BsonElement("categoryId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CategoryId { get; set; }

    [BsonElement("supplierId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SupplierId { get; set; }

    // 🆕 Yeni alan - Buluttaki görsel URL'i
    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    // 🆕 Görsel yükleme tarihi
    [BsonElement("imageUploadedAt")]
    public DateTime? ImageUploadedAt { get; set; }

    // 🆕 Cloudinary Image ID (silme işlemleri için)
    [BsonElement("cloudinaryPublicId")]
    public string? CloudinaryPublicId { get; set; }
}