using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public class PurchaseOrderItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("barcode")]
    public string? Barcode { get; set; }

    [BsonElement("currentStock")]
    public int CurrentStock { get; set; }

    [BsonElement("lowStockThreshold")]
    public int LowStockThreshold { get; set; }

    [BsonElement("orderedQuantity")]
    [Range(1, 1000000, ErrorMessage = "Sipariş miktarı en az 1 olmalıdır.")]
    public int OrderedQuantity { get; set; }

    [BsonElement("unitPrice")]
    public decimal UnitPrice { get; set; }

    [BsonElement("totalPrice")]
    public decimal TotalPrice => OrderedQuantity * UnitPrice;

    [BsonElement("notes")]
    public string? Notes { get; set; }
}
