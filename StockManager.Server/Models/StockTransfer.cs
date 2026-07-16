using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class StockTransfer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("transferNumber")]
    [Required]
    public string TransferNumber { get; set; } = string.Empty;

    [BsonElement("sourceWarehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [Required(ErrorMessage = "Çıkış deposu seçilmelidir.")]
    public string SourceWarehouseId { get; set; } = string.Empty;

    [BsonElement("sourceWarehouseName")]
    public string SourceWarehouseName { get; set; } = string.Empty;

    [BsonElement("targetWarehouseId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [Required(ErrorMessage = "Giriş deposu seçilmelidir.")]
    public string TargetWarehouseId { get; set; } = string.Empty;

    [BsonElement("targetWarehouseName")]
    public string TargetWarehouseName { get; set; } = string.Empty;

    [BsonElement("transferDate")]
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    [BsonElement("status")]
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Completed;

    [BsonElement("items")]
    public List<StockTransferItem> Items { get; set; } = new();

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("createdBy")]
    public string? CreatedBy { get; set; }
}
