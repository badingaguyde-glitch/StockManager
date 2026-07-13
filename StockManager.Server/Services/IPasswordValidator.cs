namespace StockManager.Server.Services;

public interface IPasswordValidator
{
    /// <summary>Şifre güçlülük kurallarını doğrular. Başarısızsa hata mesajı döner.</summary>
    string? Validate(string password, string? username = null);

    /// <summary>Şifre kurallarının kullanıcıya gösterilecek açıklaması.</summary>
    string GetRequirementsDescription();
}
