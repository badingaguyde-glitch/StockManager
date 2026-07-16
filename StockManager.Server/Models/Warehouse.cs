using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Warehouse
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    [Required(ErrorMessage = "Depo / Şube adı zorunludur.")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("code")]
    [Required(ErrorMessage = "Depo kodu zorunludur.")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("isDefault")]
    public bool IsDefault { get; set; } = false;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
