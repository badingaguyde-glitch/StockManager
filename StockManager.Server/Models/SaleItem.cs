using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public class SaleItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("unitPrice")]
    [Range(0, 1000000000, ErrorMessage = "Birim fiyatı 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal UnitPrice { get; set; }

    [BsonElement("totalLinePrice")]
    [Range(0, 1000000000, ErrorMessage = "Satır toplamı 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal TotalLinePrice { get; set; }
}
