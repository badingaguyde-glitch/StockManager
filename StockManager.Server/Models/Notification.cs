using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
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

    // PascalCase fallback properties (for legacy or differently cased database fields)
    [BsonElement("Message")]
    [BsonIgnoreIfNull]
    private string? MessageAlt { get => null; set => Message = value ?? string.Empty; }

    [BsonElement("IsRead")]
    [BsonIgnoreIfNull]
    private bool? IsReadAlt { get => null; set { if (value.HasValue) IsRead = value.Value; } }

    [BsonElement("CreatedAt")]
    [BsonIgnoreIfNull]
    private DateTime? CreatedAtAlt { get => null; set { if (value.HasValue) CreatedAt = value.Value; } }

    [BsonElement("ProductId")]
    [BsonIgnoreIfNull]
    private string? ProductIdAlt { get => null; set => ProductId = value; }
}