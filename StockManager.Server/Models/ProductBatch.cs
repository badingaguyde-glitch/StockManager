using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class ProductBatch
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [Required(ErrorMessage = "Ürün seçimi zorunludur.")]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("barcode")]
    public string? Barcode { get; set; }

    [BsonElement("warehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? WarehouseId { get; set; }

    [BsonElement("warehouseName")]
    public string? WarehouseName { get; set; }

    // Parti / Lot Numarası (Örn: LOT-2026-07A)
    [BsonElement("batchNumber")]
    public string? BatchNumber { get; set; }

    // Seri Numarası / IMEI (Örn: 358921098123456)
    [BsonElement("serialNumber")]
    public string? SerialNumber { get; set; }

    [BsonElement("manufactureDate")]
    public DateTime? ManufactureDate { get; set; }

    // Son Kullanma Tarihi / SKT
    [BsonElement("expiryDate")]
    public DateTime? ExpiryDate { get; set; }

    [BsonElement("quantity")]
    [Range(1, 1000000, ErrorMessage = "Miktar en az 1 olmalıdır.")]
    public int Quantity { get; set; } = 1;

    [BsonElement("status")]
    public ProductBatchStatus Status { get; set; } = ProductBatchStatus.Active;

    [BsonElement("unitCost")]
    public decimal? UnitCost { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
