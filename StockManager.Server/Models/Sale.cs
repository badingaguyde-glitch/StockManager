using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Sale
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("saleDate")]
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    [BsonElement("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [BsonElement("totalAmount")]
    [Range(0, 1000000000, ErrorMessage = "Satış tutarı 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal TotalAmount { get; set; }

    [BsonElement("customerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CustomerId { get; set; }

    [BsonElement("paymentType")]
    public PaymentType PaymentType { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "TRY";

    [BsonElement("stripePaymentIntentId")]
    public string? StripePaymentIntentId { get; set; }

    [BsonElement("stripeStatus")]
    public string? StripeStatus { get; set; }

    [BsonElement("warehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? WarehouseId { get; set; }

    [BsonElement("warehouseName")]
    public string? WarehouseName { get; set; }

    [BsonElement("items")]
    public List<SaleItem> Items { get; set; } = new();
}

