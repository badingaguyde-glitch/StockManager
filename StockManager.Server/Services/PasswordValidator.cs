using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class PasswordValidator : IPasswordValidator
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "123456789", "qwerty", "abc123",
        "password1", "admin123", "letmein", "welcome", "monkey", "dragon"
    };

    private readonly PasswordSecuritySettings _settings;

    public PasswordValidator(IOptions<PasswordSecuritySettings> settings)
    {
        _settings = settings.Value;
    }

    public string? Validate(string password, string? username = null)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Şifre boş olamaz.";

        if (password.Length < _settings.MinPasswordLength)
            return $"Şifre en az {_settings.MinPasswordLength} karakter olmalıdır.";

        if (_settings.RequireUppercase && !password.Any(char.IsUpper))
            return "Şifre en az bir büyük harf içermelidir.";

        if (_settings.RequireLowercase && !password.Any(char.IsLower))
            return "Şifre en az bir küçük harf içermelidir.";

        if (_settings.RequireDigit && !password.Any(char.IsDigit))
            return "Şifre en az bir rakam içermelidir.";

        if (_settings.RequireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            return "Şifre en az bir özel karakter içermelidir (!@#$%^&* vb.).";

        if (!string.IsNullOrEmpty(username) &&
            password.Equals(username, StringComparison.OrdinalIgnoreCase))
            return "Şifre kullanıcı adıyla aynı olamaz.";

        if (CommonPasswords.Contains(password))
            return "Bu şifre çok yaygın kullanılmaktadır. Daha güçlü bir şifre seçiniz.";

        return null;
    }

    public string GetRequirementsDescription()
    {
        var parts = new List<string> { $"En az {_settings.MinPasswordLength} karakter" };

        if (_settings.RequireUppercase) parts.Add("büyük harf");
        if (_settings.RequireLowercase) parts.Add("küçük harf");
        if (_settings.RequireDigit) parts.Add("rakam");
        if (_settings.RequireSpecialChar) parts.Add("özel karakter");

        return "Şifre gereksinimleri: " + string.Join(", ", parts) + ".";
    }
}
