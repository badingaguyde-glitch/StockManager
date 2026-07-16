using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Server.Services;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using System.Linq;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogService _auditLogService;
    private readonly MongoDBContext _mongoDbContext;

    public AuditLogsController(IAuditLogService auditLogService, MongoDBContext mongoDbContext)
    {
        _auditLogService = auditLogService;
        _mongoDbContext = mongoDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? actionFilter,
        string? userFilter,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1)
    {
        var model = await _auditLogService.GetDashboardDataAsync(
            searchTerm, actionFilter, userFilter, startDate, endDate, page, 30);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOldLogs(int daysToKeep = 90)
    {
        await _auditLogService.ClearOldLogsAsync(daysToKeep);
        TempData["SuccessMessage"] = $"{daysToKeep} günden eski denetim kayıtları başarıyla temizlendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DbCheck()
    {
        // 1. Ürünlerdeki eksik alanlar ve kritik seviyeler
        var products = await _mongoDbContext.Products.Find(Builders<Product>.Filter.Empty).ToListAsync();
        var productIssues = new List<dynamic>();
        var lowStockProducts = new List<Product>();

        foreach (var p in products)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Barcode)) missingFields.Add("Barkod");
            if (string.IsNullOrWhiteSpace(p.Description)) missingFields.Add("Açıklama");
            if (string.IsNullOrWhiteSpace(p.CategoryId)) missingFields.Add("Kategori");
            if (string.IsNullOrWhiteSpace(p.SupplierId)) missingFields.Add("Tedarikçi");
            if (string.IsNullOrWhiteSpace(p.ImageUrl)) missingFields.Add("Ürün Görseli");

            if (missingFields.Any())
            {
                productIssues.Add(new {
                    ProductId = p.Id ?? "Bilinmiyor",
                    ProductName = p.Name,
                    MissingFields = missingFields
                });
            }

            if (p.Quantity <= p.LowStockThreshold)
            {
                lowStockProducts.Add(p);
            }
        }

        // 2. Müşterilerdeki eksik alanlar
        var customers = await _mongoDbContext.Customers.Find(Builders<Customer>.Filter.Empty).ToListAsync();
        var customerIssues = new List<dynamic>();
        foreach (var c in customers)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(c.Email)) missingFields.Add("E-posta");
            if (string.IsNullOrWhiteSpace(c.Phone)) missingFields.Add("Telefon");

            if (missingFields.Any())
            {
                customerIssues.Add(new {
                    CustomerId = c.Id ?? "Bilinmiyor",
                    CustomerName = c.FullName,
                    MissingFields = missingFields
                });
            }
        }

        // 3. Tedarikçilerdeki eksik alanlar
        var suppliers = await _mongoDbContext.Suppliers.Find(Builders<Supplier>.Filter.Empty).ToListAsync();
        var supplierIssues = new List<dynamic>();
        foreach (var s in suppliers)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(s.Email)) missingFields.Add("E-posta");
            if (string.IsNullOrWhiteSpace(s.Phone)) missingFields.Add("Telefon");
            if (string.IsNullOrWhiteSpace(s.ContactName)) missingFields.Add("İletişim Kişisi");

            if (missingFields.Any())
            {
                supplierIssues.Add(new {
                    SupplierId = s.Id ?? "Bilinmiyor",
                    SupplierName = s.CompanyName,
                    MissingFields = missingFields
                });
            }
        }

        // 4. Güncelleme ve Silmeler (Son 300 log kaydı)
        var allLogs = await _auditLogService.GetDashboardDataAsync(null, null, null, null, null, 1, 300);
        var modificationLogs = allLogs.Logs
            .Where(l => l.Action != null && 
                       (l.Action.Contains("Update") || 
                        l.Action.Contains("Edit") || 
                        l.Action.Contains("Delete") || 
                        l.Action.Contains("Remove") || 
                        l.Action.Contains("Clear") || 
                        l.Action.Contains("Reset")))
            .ToList();

        // Verileri ViewBag ile taşıyarak model sınıfı bağımlılığını kaldırıyoruz
        ViewBag.ProductIssues = productIssues;
        ViewBag.LowStockProducts = lowStockProducts;
        ViewBag.CustomerIssues = customerIssues;
        ViewBag.SupplierIssues = supplierIssues;
        ViewBag.ModificationLogs = modificationLogs;

        return View();
    }
}
