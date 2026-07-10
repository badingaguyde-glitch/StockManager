using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class Category
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }
}
