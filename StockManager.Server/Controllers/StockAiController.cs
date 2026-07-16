using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using System.Text;
using System.Text.Json;

namespace StockManager.Server.Controllers;

[Authorize]
public class StockAiController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockAiController> _logger;

    public StockAiController(MongoDBContext context, IConfiguration configuration, ILogger<StockAiController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("message", out var messageProp) || string.IsNullOrWhiteSpace(messageProp.GetString()))
        {
            _logger.LogWarning("AI Asistanı isteği reddedildi: Boş mesaj gönderildi.");
            return Json(new { error = "Mesaj boş olamaz." });
        }

        string userMessage = messageProp.GetString()!;
        _logger.LogInformation("AI Asistanına yeni soru soruldu: {UserMessage}", userMessage);

        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
                        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY_2")
                        ?? _configuration["GEMINI_API_KEY"] 
                        ?? _configuration["GEMINI_API_KEY_2"] 
                        ?? "";

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Yapay Zeka API Hatası: 'GEMINI_API_KEY' veya 'GEMINI_API_KEY_2' ortam değişkeni bulunamadı.");
            return Json(new { reply = "Hata: Yapay zeka API anahtarı (.env dosyasında GEMINI_API_KEY veya GEMINI_API_KEY_2 olarak) bulunamadı." });
        }

        try
        {
            // MongoDB'den güncel envanteri al
            var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
            
            var stockSummary = new StringBuilder();
            stockSummary.AppendLine("Mevcut Stok Verileri:");
            foreach (var p in products)
            {
                stockSummary.AppendLine($"- Ürün Adı: {p.Name}, Barkod: {p.Barcode}, Stok Adedi: {p.Quantity}, Kritik Seviye Eşiği: {p.LowStockThreshold}, Fiyat: {p.SalePrice} TRY");
            }

            // Sistem talimatları (Gelecek tahmini yapmayı engeller)
            string systemInstruction = 
                "Sen Stock Manager uygulamasının yardımcı yapay zeka asistanısın.\n" +
                "Kullanıcıya sadece sana sunulan 'Mevcut Stok Verileri' bağlamında yer alan bilgilere dayanarak cevap vereceksiniz.\n" +
                "ÖNEMLİ KURALLAR:\n" +
                "1. Geleceğe dair tahminler, öngörüler veya stok satış projeksiyonları ASLA yapmayacaksın.\n" +
                "2. Kullanıcı gelecek tahmini veya tahminleme yapmanı isterse, kuralların gereği sadece güncel durum hakkında bilgi verebileceğini belirtip bu isteği kibarca reddet.\n" +
                "3. Yalnızca mevcut stok durumu, kritik seviyedeki ürünler ve genel envanter sorularını yanıtla.\n" +
                "4. Cevaplarını kısa, net ve anlaşılır tut. Kullanıcının sorduğu dilde yanıt ver.";

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"[Kullanıcı Sorusu]: {userMessage}\n\n[Bağlam (Mevcut Durum)]:\n{stockSummary}" }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={apiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errResponse = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API Hata Yanıtı: Kodu: {StatusCode}, Yanıt İçeriği: {Response}", response.StatusCode, errResponse);
                return Json(new { reply = $"Gemini API Hatası oluştu. HTTP Kodu: {response.StatusCode}." });
            }

            var responseData = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseData);
            
            string aiReply = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "Cevap üretilemedi.";

            _logger.LogInformation("AI Asistanı başarıyla yanıt üretti.");
            return Json(new { reply = aiReply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stok AI Asistanı isteği işlenirken beklenmeyen bir hata oluştu.");
            return Json(new { reply = $"Hata: {ex.Message}" });
        }
    }
}