using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public enum UserRole
{
    Admin,
    Personel,
    Muhasebeci
}

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id {get;set;}

    [BsonElement("username")]
    public string? Username {get;set;}

    [BsonElement("email")]
    public string Email {get;set;}=string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash {get;set;}=string.Empty;

    [BsonElement("role")]
    public UserRole Role {get;set;}=UserRole.Personel;

    [BsonElement("passwordResetCode")]
    public string? PasswordResetCode {get;set;}

    [BsonElement("passwordResetCodeExpireAt")]
    public DateTime? PasswordResetCodeExpireAt {get;set;}

    [BsonElement("pendingPasswordHash")]
    public string? PendingPasswordHash {get;set;}
}