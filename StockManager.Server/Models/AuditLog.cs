using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

[BsonIgnoreExtraElements]
public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id {get;set;}

    [BsonElement("userEmail")]
    public string? UserEmail {get;set;}

    [BsonElement("username")]
    public string? Username {get;set;}

    [BsonElement("action")]
    public string? Action {get;set;}

    [BsonElement("timestamp")]
    public DateTime Timestamp {get;set;}

    [BsonElement("details")]
    public string? Details {get;set;}
}