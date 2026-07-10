using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
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
                System.Diagnostics.Debug.WriteLine($"[EMAIL SIMULATION] Alıcı: {to}, Konu: {subject}, İçerik: {body}");
                return;
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage(fromAddress, to, subject, body)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"E-posta gönderim hatası: {ex.Message}");
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
