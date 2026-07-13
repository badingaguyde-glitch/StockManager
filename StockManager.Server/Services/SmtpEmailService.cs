using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? _configuration["SmtpSettings:Host"] ?? "smtp.mailtrap.io";
            var smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? _configuration["SmtpSettings:Port"] ?? "2525");
            var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? _configuration["SmtpSettings:Username"] ?? "";
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? _configuration["SmtpSettings:Password"] ?? "";
            var fromAddress = Environment.GetEnvironmentVariable("SMTP_FROM") ?? _configuration["SmtpSettings:From"] ?? "noreply@stockmanager.com";

            // MailMessage formatı için geçerli bir e-posta adresi olmalıdır
            if (!fromAddress.Contains("@"))
            {
                fromAddress = smtpUser.Contains("@") ? smtpUser : "noreply@stockmanager.com";
            }

            // Bilgiler eksikse çökme yerine konsola simüle et
            if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                _logger.LogWarning($"[EMAIL SIMULATION] SMTP credentials missing. To: {to}, Subject: {subject}");
                return;
            }

            _logger.LogInformation($"Attempting to send email via SMTP to {to} using host {smtpHost}:{smtpPort}...");

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var fromMailAddress = new MailAddress(fromAddress, "StockManager");
            var mailMessage = new MailMessage(fromMailAddress, new MailAddress(to))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Email successfully sent to {to}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"E-posta gönderim hatası (SMTP Error): {ex.Message}");
        }
    }

    public async Task SendLowStockAlertAsync(string to, Product product, DateTime date, string? supplierOrCustomerName)
    {
        var subject = $"⚠️ Kritik Stok Uyarısı: {product.Name}";
        
        var supplierOrCustomerRow = "";
        if (!string.IsNullOrEmpty(supplierOrCustomerName))
        {
            supplierOrCustomerRow = $@"
            <tr style=""border-bottom: 1px solid #eeeeee;"">
                <td style=""padding: 12px 15px; font-weight: bold; color: #555555;"">İlişkili Cari (Tedarikçi/Müşteri):</td>
                <td style=""padding: 12px 15px; color: #111111;"">{supplierOrCustomerName}</td>
            </tr>";
        }

        var body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);"">
    <div style=""background: linear-gradient(135deg, #d32f2f, #f44336); color: #ffffff; padding: 20px; text-align: center;"">
        <h2 style=""margin: 0; font-size: 24px; font-weight: 600; letter-spacing: 0.5px;"">⚠️ Kritik Stok Uyarısı</h2>
    </div>
    <div style=""padding: 24px; background-color: #fafafa; color: #333333; line-height: 1.6;"">
        <p style=""margin-top: 0; font-size: 16px;"">Sayın Yetkili,</p>
        <p style=""font-size: 15px;"">Sistemdeki bir ürünün stok miktarı kritik eşiğin altına düşmüştür. Detaylar aşağıda yer almaktadır:</p>
        
        <table style=""width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #ffffff; border-radius: 6px; overflow: hidden;"">
            <tr style=""border-bottom: 1px solid #eeeeee;"">
                <td style=""padding: 12px 15px; font-weight: bold; color: #555555; width: 35%;"">Ürün Adı:</td>
                <td style=""padding: 12px 15px; color: #111111;"">{product.Name}</td>
            </tr>
            <tr style=""border-bottom: 1px solid #eeeeee;"">
                <td style=""padding: 12px 15px; font-weight: bold; color: #555555;"">Barkod:</td>
                <td style=""padding: 12px 15px; color: #111111;"">{product.Barcode ?? "-"}</td>
            </tr>
            <tr style=""border-bottom: 1px solid #eeeeee;"">
                <td style=""padding: 12px 15px; font-weight: bold; color: #555555;"">Kalan Stok:</td>
                <td style=""padding: 12px 15px; color: #d32f2f; font-weight: bold;"">{product.Quantity} (Kritik Eşik: {product.LowStockThreshold})</td>
            </tr>
            <tr style=""border-bottom: 1px solid #eeeeee;"">
                <td style=""padding: 12px 15px; font-weight: bold; color: #555555;"">İşlem Zamanı:</td>
                <td style=""padding: 12px 15px; color: #111111;"">{date.ToLocalTime():dd.MM.yyyy HH:mm:ss}</td>
            </tr>
            {supplierOrCustomerRow}
        </table>
        
        <p style=""font-size: 14px; color: #666666; font-style: italic; margin-bottom: 0;"">Lütfen en kısa sürede stok güncellemesi veya tedarik siparişi oluşturunuz.</p>
    </div>
    <div style=""background-color: #f1f1f1; padding: 15px; text-align: center; font-size: 12px; color: #888888; border-top: 1px solid #e0e0e0;"">
        Bu e-posta <strong>StockManager</strong> sistemi tarafından otomatik olarak oluşturulmuştur.
    </div>
</div>";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendPasswordResetCodeAsync(string to, string code)
    {
        var subject = "🔐 Şifre Değiştirme Doğrulama Kodu";
        var body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);"">
    <div style=""background: linear-gradient(135deg, #1e3c72, #2a5298); color: #ffffff; padding: 20px; text-align: center;"">
        <h2 style=""margin: 0; font-size: 22px; font-weight: 600;"">Şifre Değiştirme İsteği</h2>
    </div>
    <div style=""padding: 24px; background-color: #fafafa; color: #333333; line-height: 1.6; text-align: center;"">
        <p style=""margin-top: 0; font-size: 16px; text-align: left;"">Sayın Kullanıcı,</p>
        <p style=""font-size: 15px; text-align: left;"">Hesabınızın şifresini değiştirmek için bir istek aldık. Lütfen aşağıdaki 6 haneli doğrulama kodunu açılan sayfaya giriniz:</p>
        
        <div style=""background-color: #ffffff; border: 1px dashed #2a5298; border-radius: 6px; display: inline-block; padding: 15px 30px; margin: 20px 0; font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #1e3c72;"">
            {code}
        </div>
        
        <p style=""font-size: 13px; color: #888888; text-align: left;"">Bu kod 15 dakika boyunca geçerlidir. İstekte bulunan siz değilseniz bu e-postayı dikkate almayınız.</p>
    </div>
    <div style=""background-color: #f1f1f1; padding: 15px; text-align: center; font-size: 11px; color: #888888; border-top: 1px solid #e0e0e0;"">
        Bu e-posta <strong>StockManager</strong> sistemi tarafından otomatik olarak oluşturulmuştur.
    </div>
</div>";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendPasswordResetCodeAsync(string to, string code, int validityMinutes)
    {
        var subject = "StockManager - Şifre Sıfırlama Doğrulama Kodu";
        var body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;"">
    <div style=""background: #0d6efd; color: #ffffff; padding: 20px; text-align: center;"">
        <h2 style=""margin: 0;"">Şifre Sıfırlama</h2>
    </div>
    <div style=""padding: 24px; background-color: #fafafa; color: #333;"">
        <p>Şifrenizi sıfırlamak için aşağıdaki doğrulama kodunu kullanın:</p>
        <p style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; text-align: center; color: #0d6efd; margin: 24px 0;"">{code}</p>
        <p style=""color: #666; font-size: 14px;"">Bu kod <strong>{validityMinutes} dakika</strong> geçerlidir. 5 kez yanlış girilirse kod iptal edilir.</p>
        <p style=""color: #888; font-size: 13px; margin-bottom: 0;"">Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>
    </div>
</div>";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendNewTemporaryPasswordAsync(string to, string newPassword)
    {
        var subject = "🔑 Yeni Geçici Giriş Şifreniz";
        var body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 500px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);"">
    <div style=""background: linear-gradient(135deg, #2b5876, #4e4376); color: #ffffff; padding: 20px; text-align: center;"">
        <h2 style=""margin: 0; font-size: 22px; font-weight: 600;"">Hesabınız Sıfırlandı</h2>
    </div>
    <div style=""padding: 24px; background-color: #fafafa; color: #333333; line-height: 1.6; text-align: center;"">
        <p style=""margin-top: 0; font-size: 16px; text-align: left;"">Sayın Çalışanımız,</p>
        <p style=""font-size: 15px; text-align: left;"">Giriş şifreniz sistem yöneticisi (Admin) tarafından sıfırlanmıştır. Yeni geçici şifreniz aşağıda yer almaktadır:</p>
        
        <div style=""background-color: #ffffff; border: 1px solid #4e4376; border-radius: 6px; display: inline-block; padding: 12px 25px; margin: 20px 0; font-size: 22px; font-weight: bold; color: #2b5876;"">
            {newPassword}
        </div>
        
        <p style=""font-size: 14px; text-align: left; font-weight: bold; color: #d32f2f;"">Önemli Güvenlik Uyarısı:</p>
        <p style=""font-size: 13px; color: #555555; text-align: left; margin-top: 5px;"">
            Güvenliğiniz için sisteme ilk girişinizden sonra sağ üstteki kullanıcı menüsünden <strong>'Profil Ayarlarım'</strong> sayfasına giderek şifrenizi güncellemeyi unutmayınız.
        </p>
    </div>
    <div style=""background-color: #f1f1f1; padding: 15px; text-align: center; font-size: 11px; color: #888888; border-top: 1px solid #e0e0e0;"">
        Bu e-posta <strong>StockManager</strong> sistemi tarafından otomatik olarak oluşturulmuştur.
    </div>
</div>";

        await SendEmailAsync(to, subject, body);
    }
}
