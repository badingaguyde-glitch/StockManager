using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;
using System.Security.Claims;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class BatchesController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IAuditLogService _auditLogService;

    public BatchesController(MongoDBContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? searchTerm, string? productId, string? statusFilter)
    {
        var filterBuilder = Builders<ProductBatch>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filter &= (filterBuilder.Regex(b => b.BatchNumber, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")) |
                       filterBuilder.Regex(b => b.SerialNumber, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")) |
                       filterBuilder.Regex(b => b.ProductName, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")));
        }

        if (!string.IsNullOrEmpty(productId))
        {
            filter &= filterBuilder.Eq(b => b.ProductId, productId);
        }

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<ProductBatchStatus>(statusFilter, out var status))
        {
            filter &= filterBuilder.Eq(b => b.Status, status);
        }

        var batches = await _context.ProductBatches
            .Find(filter)
            .SortByDescending(b => b.CreatedAt)
            .ToListAsync();

        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
        ViewBag.Products = products;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.CurrentProductId = productId;
        ViewBag.CurrentStatus = statusFilter;

        return View(batches);
    }

    public async Task<IActionResult> Create(string? productId)
    {
        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
        var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync();

        ViewBag.Products = products;
        ViewBag.Warehouses = warehouses;

        var model = new ProductBatch();
        if (!string.IsNullOrEmpty(productId))
        {
            model.ProductId = productId;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductBatch model, bool updateProductStock)
    {
        if (!ModelState.IsValid)
        {
            var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
            var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync();
            ViewBag.Products = products;
            ViewBag.Warehouses = warehouses;
            return View(model);
        }

        var product = await _context.Products.Find(p => p.Id == model.ProductId).FirstOrDefaultAsync();
        if (product == null)
        {
            TempData["error"] = "Seçilen ürün bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        model.ProductName = product.Name;
        model.Barcode = product.Barcode;
        model.CreatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(model.WarehouseId))
        {
            var warehouse = await _context.Warehouses.Find(w => w.Id == model.WarehouseId).FirstOrDefaultAsync();
            model.WarehouseName = warehouse?.Name ?? "Ana Depo";
        }
        else
        {
            var defaultWarehouse = await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync();
            model.WarehouseId = defaultWarehouse?.Id;
            model.WarehouseName = defaultWarehouse?.Name ?? "Ana Depo";
        }

        // Eğer seri no / IMEI verilmişse ve miktar 1'den büyük girildiyse uyarı veya otomatik 1 yap
        if (!string.IsNullOrWhiteSpace(model.SerialNumber))
        {
            model.Quantity = 1;
        }

        using var session = await _context.Client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            await _context.ProductBatches.InsertOneAsync(session, model);

            // Eğer "Bu partiyi/seriyi stok miktarına da ekle" seçeneği işaretliyse
            if (updateProductStock && model.Quantity > 0)
            {
                product.Quantity += model.Quantity;
                if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();

                var wStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == model.WarehouseId);
                if (wStock != null)
                {
                    wStock.Quantity += model.Quantity;
                }
                else if (!string.IsNullOrEmpty(model.WarehouseId))
                {
                    product.WarehouseStocks.Add(new WarehouseStock
                    {
                        WarehouseId = model.WarehouseId,
                        WarehouseName = model.WarehouseName ?? "Ana Depo",
                        Quantity = model.Quantity
                    });
                }

                await _context.Products.ReplaceOneAsync(session, p => p.Id == product.Id, product);

                var movement = new StockMovement
                {
                    ProductId = product.Id!,
                    Quantity = model.Quantity,
                    Type = StockMovementType.StockIn,
                    Notes = $"Parti/Seri Girişi: {(model.BatchNumber ?? model.SerialNumber ?? "Yeni Parti")}",
                    WarehouseId = model.WarehouseId,
                    WarehouseName = model.WarehouseName ?? "Ana Depo"
                };
                await _context.StockMovements.InsertOneAsync(session, movement);
            }

            await session.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            TempData["error"] = $"Kaydedilirken hata oluştu: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Sistem Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "Parti/Seri No Eklendi", $"Ürün '{product.Name}' için Parti/Seri kaydı oluşturuldu (Miktar: {model.Quantity}).");

        TempData["success"] = $"Parti / Seri kaydı başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Expiring()
    {
        var activeBatchesWithExpiry = await _context.ProductBatches
            .Find(b => b.ExpiryDate != null && b.Status == ProductBatchStatus.Active)
            .SortBy(b => b.ExpiryDate)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var expired = activeBatchesWithExpiry.Where(b => b.ExpiryDate < now).ToList();
        var expiringSoon = activeBatchesWithExpiry.Where(b => b.ExpiryDate >= now && b.ExpiryDate <= now.AddDays(30)).ToList();
        var expiringLater = activeBatchesWithExpiry.Where(b => b.ExpiryDate > now.AddDays(30) && b.ExpiryDate <= now.AddDays(90)).ToList();

        ViewBag.ExpiredCount = expired.Count;
        ViewBag.ExpiringSoonCount = expiringSoon.Count;
        ViewBag.ExpiringLaterCount = expiringLater.Count;

        ViewBag.ExpiredList = expired;
        ViewBag.ExpiringSoonList = expiringSoon;
        ViewBag.ExpiringLaterList = expiringLater;

        return View(activeBatchesWithExpiry);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string id, ProductBatchStatus status)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var batch = await _context.ProductBatches.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (batch == null) return NotFound();

        batch.Status = status;
        await _context.ProductBatches.ReplaceOneAsync(b => b.Id == batch.Id, batch);

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Sistem Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "Parti Durumu Değişti", $"Parti #{batch.BatchNumber ?? batch.SerialNumber ?? batch.Id} durumu '{status}' yapıldı.");

        TempData["success"] = $"Parti durumu başarıyla '{status}' olarak güncellendi.";
        return RedirectToAction(nameof(Expiring));
    }
}
