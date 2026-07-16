using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public class StockMovement
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("type")]
    public StockMovementType Type { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [BsonElement("supplierId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SupplierId { get; set; }

    [BsonElement("customerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CustomerId { get; set; }

    [BsonElement("warehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? WarehouseId { get; set; }

    [BsonElement("warehouseName")]
    public string? WarehouseName { get; set; }

    [BsonElement("stockTransferId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StockTransferId { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }
}

