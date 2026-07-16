using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public class WarehouseStock
{
    [BsonElement("warehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WarehouseId { get; set; } = string.Empty;

    [BsonElement("warehouseName")]
    public string WarehouseName { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }
}
