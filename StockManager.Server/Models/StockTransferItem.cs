using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public class StockTransferItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("barcode")]
    public string? Barcode { get; set; }

    [BsonElement("quantity")]
    [Range(1, 1000000, ErrorMessage = "Transfer miktarı en az 1 olmalıdır.")]
    public int Quantity { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }
}
