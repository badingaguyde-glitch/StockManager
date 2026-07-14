using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("purchasePrice")]
    [Range(0, 1000000000, ErrorMessage = "Alış fiyatı 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal PurchasePrice { get; set; }

    [BsonElement("salePrice")]
    [Range(0, 1000000000, ErrorMessage = "Satış fiyatı 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal SalePrice { get; set; }

    [BsonElement("quantity")]
    [Range(0, 1000000, ErrorMessage = "Stok miktarı 0 ile 1.000.000 arasında olmalıdır.")]
    public int Quantity { get; set; }

    [BsonElement("lowStockThreshold")]
    [Range(0, 1000000, ErrorMessage = "Kritik eşik 0 ile 1.000.000 arasında olmalıdır.")]
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