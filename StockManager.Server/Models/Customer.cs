using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Customer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("fullName")]
    [Required(ErrorMessage = "Müşteri adı zorunludur.")]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("email")]
    [EmailAddress(ErrorMessage = "Geçersiz e-posta adresi.")]
    public string? Email { get; set; }

    [BsonElement("balance")]
    [Range(0, 1000000000, ErrorMessage = "Bakiye 0 ile 1.000.000.000 TL arasında olmalıdır.")]
    public decimal Balance { get; set; }
}
