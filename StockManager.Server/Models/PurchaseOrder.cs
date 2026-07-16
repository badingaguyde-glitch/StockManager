using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class PurchaseOrder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("orderNumber")]
    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    [BsonElement("supplierId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SupplierId { get; set; } = string.Empty;

    [BsonElement("supplierName")]
    public string SupplierName { get; set; } = string.Empty;

    [BsonElement("supplierEmail")]
    public string? SupplierEmail { get; set; }

    [BsonElement("targetWarehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? TargetWarehouseId { get; set; }

    [BsonElement("targetWarehouseName")]
    public string? TargetWarehouseName { get; set; }

    [BsonElement("orderDate")]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [BsonElement("expectedDeliveryDate")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [BsonElement("status")]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [BsonElement("items")]
    public List<PurchaseOrderItem> Items { get; set; } = new();

    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("createdBy")]
    public string? CreatedBy { get; set; }

    [BsonElement("emailSentAt")]
    public DateTime? EmailSentAt { get; set; }
}
