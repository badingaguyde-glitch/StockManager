using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public class Notification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("isRead")]
    public bool IsRead { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProductId { get; set; }

    // YENİ EKLENEN İLİŞKİ ALANLARI:
    [BsonElement("supplierId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SupplierId { get; set; }

    [BsonElement("stockMovementId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StockMovementId { get; set; }

    [BsonElement("saleId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SaleId { get; set; }
}