using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using System.Text;
using System.Text.Json;

namespace StockManager.Server.Controllers;
[Authorize]
public class StockAiController: Controller
{
    private readonly StockManager.Server.Data.MongoDBContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public StockAiController(StockManager.Server.Data.MongoDBContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("message", out var messageProp) || string.IsNullOrWhiteSpace(messageProp.GetString()))
        {
            return Json(new {error = "Mesaj boş olamaz."});
        }

        string userMessage = messageProp.GetString()!;

        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY_2")?? _configuration["GEMINI_API_KEY_2"]??"";

        if (string.IsNullOrEmpty(apiKey))
        {
            return Json(new {reply = "API anahtarı bulunamadı. Lütfen yapılandırmayı kontrol edin."});
        }

        try
        {
            var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();

            var StockSummary = new StringBuilder();
            StockSummary.AppendLine("Mevcut Stok verileri:");
            foreach (var p in products)
            {
                StockSummary.AppendLine($"- Ürün Adı: {p.Name}, Stok Miktarı: {p.Quantity}, Fiyat: {p.SalePrice} TRY, Barkod: {p.Barcode}, Kategori: {_context.Categories.Find(c => c.Id == p.CategoryId).FirstOrDefault()?.Name ?? "Bilinmiyor"}, Tedarikçi: {_context.Suppliers.Find(s => s.Id == p.SupplierId).FirstOrDefault()?.CompanyName ?? "Bilinmiyor"}, kritik seviye: {p.LowStockThreshold}");
            }

            string systemInstruction ="Sen Stock Manager uygulamasının yardımcı yapay zeka asistanısın.\n" +
                "Kullanıcıya sadece sana sunulan 'Mevcut Stok Verileri' bağlamında yer alan bilgilere dayanarak cevap vereceksin.\n" +
                "ÖNEMLİ KURALLAR:\n" +
                "1. Geleceğe dair tahminler, öngörüler veya stok satış projeksiyonları ASLA yapmayacaksın.\n" +
                "2. Kullanıcı gelecek tahmini veya tahminleme yapmanı isterse, kuralların gereği sadece güncel durum hakkında bilgi verebileceğini belirtip bu isteği kibarca reddet.\n" +
                "3. Yalnızca mevcut stok durumu, kritik seviyedeki ürünler ve genel envanter sorularını yanıtla.\n" +
                "4. Cevaplarını kısa, net ve anlaşılır tut. Kullanıcının sorduğu dilde yanıt ver.";

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] {new {text = systemInstruction}}
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new {text =$"[kullanıcı mesajı]: {userMessage}\n\n[Mevcut Stok Verileri]:\n{StockSummary}"},
                        }
                    }
                }
            };
            var jsonContent= JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8,"application/json");

            string url =$"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                return Json(new { reply = $"Gemini API Hatası: Sunucu {response.StatusCode} kodu döndürdü." });
            }
            var responseData = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseData);
            
            // Cevap metnini ayıkla
            string aiReply = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "Üzgünüm, cevap üretilemedi.";
            return Json(new { reply = aiReply });
        }catch (Exception ex)
        {
            return Json(new { reply = $"Bir hata oluştu: {ex.Message}" });
        }
    }
}