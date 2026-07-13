namespace StockManager.Server.Models;

public class PasswordSecuritySettings
{
    public const string SectionName = "PasswordSecurity";

    /// <summary>Doğrulama kodunun geçerlilik süresi (dakika).</summary>
    public int CodeValidityMinutes { get; set; } = 5;

    /// <summary>Yanlış kod girişi limiti; aşılırsa kod iptal edilir.</summary>
    public int MaxCodeAttempts { get; set; } = 5;

    /// <summary>Doğrulama kodu uzunluğu.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Minimum şifre uzunluğu.</summary>
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>Büyük harf zorunluluğu.</summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>Küçük harf zorunluluğu.</summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>Rakam zorunluluğu.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>Özel karakter zorunluluğu.</summary>
    public bool RequireSpecialChar { get; set; } = true;
}
