using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StockManager.Server.Models;

public enum UserRole
{
    Admin,
    Personel,
    Muhasebeci
}

[BsonIgnoreExtraElements]
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

    // PascalCase fallback properties (for deserialization of existing DB records with uppercase keys)
    [BsonElement("Email")]
    [BsonIgnoreIfNull]
    private string? EmailAlt { get => null; set => Email = value ?? string.Empty; }

    [BsonElement("Username")]
    [BsonIgnoreIfNull]
    private string? UsernameAlt { get => null; set => Username = value; }

    [BsonElement("PasswordHash")]
    [BsonIgnoreIfNull]
    private string? PasswordHashAlt { get => null; set => PasswordHash = value ?? string.Empty; }

    [BsonElement("Role")]
    [BsonIgnoreIfNull]
    private UserRole? RoleAlt { get => null; set { if (value.HasValue) Role = value.Value; } }

}