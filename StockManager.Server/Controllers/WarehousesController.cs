using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class WarehousesController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IAuditLogService _auditLogService;

    public WarehousesController(MongoDBContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var warehouses = await _context.Warehouses
            .Find(FilterDefinition<Warehouse>.Empty)
            .SortByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .ToListAsync();

        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();

        var stockCounts = new Dictionary<string, int>();
        var totalQuantities = new Dictionary<string, int>();

        foreach (var w in warehouses)
        {
            if (w.Id == null) continue;
            var wProducts = products.Where(p => p.WarehouseStocks != null && p.WarehouseStocks.Any(ws => ws.WarehouseId == w.Id && ws.Quantity > 0)).ToList();
            stockCounts[w.Id] = wProducts.Count;
            totalQuantities[w.Id] = products.Sum(p => p.WarehouseStocks?.Where(ws => ws.WarehouseId == w.Id).Sum(ws => ws.Quantity) ?? 0);
        }

        ViewBag.StockCounts = stockCounts;
        ViewBag.TotalQuantities = totalQuantities;

        return View(warehouses);
    }

    public async Task<IActionResult> Details(string id)
    {
        var warehouse = await _context.Warehouses.Find(w => w.Id == id).FirstOrDefaultAsync();
        if (warehouse == null) return NotFound();

        var products = await _context.Products
            .Find(p => p.WarehouseStocks != null && p.WarehouseStocks.Any(ws => ws.WarehouseId == id))
            .SortBy(p => p.Name)
            .ToListAsync();

        // Her ürün için bu depodaki miktar listesi
        var warehouseItems = products.Select(p =>
        {
            var stock = p.WarehouseStocks?.FirstOrDefault(ws => ws.WarehouseId == id);
            return new
            {
                Product = p,
                Quantity = stock?.Quantity ?? 0,
                LowStock = (stock?.Quantity ?? 0) <= p.LowStockThreshold
            };
        }).ToList();

        ViewBag.WarehouseItems = warehouseItems;
        return View(warehouse);
    }

    public IActionResult Create()
    {
        return View(new Warehouse());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Warehouse model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.Code = model.Code.Trim().ToUpperInvariant();
        model.CreatedAt = DateTime.UtcNow;

        // Kod kontrolü
        if (await _context.Warehouses.Find(w => w.Code == model.Code).AnyAsync())
        {
            ModelState.AddModelError(nameof(model.Code), "Bu depo kodu zaten kullanımda.");
            return View(model);
        }

        if (model.IsDefault)
        {
            var update = Builders<Warehouse>.Update.Set(w => w.IsDefault, false);
            await _context.Warehouses.UpdateManyAsync(w => w.IsDefault, update);
        }

        await _context.Warehouses.InsertOneAsync(model);

        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
        await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Depo Oluşturma", $"Yeni depo oluşturuldu: {model.Name} ({model.Code})");

        TempData["success"] = $"Depo '{model.Name}' başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var warehouse = await _context.Warehouses.Find(w => w.Id == id).FirstOrDefaultAsync();
        if (warehouse == null) return NotFound();
        return View(warehouse);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Warehouse model)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _context.Warehouses.Find(w => w.Id == id).FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        model.Code = model.Code.Trim().ToUpperInvariant();

        if (existing.Code != model.Code && await _context.Warehouses.Find(w => w.Code == model.Code && w.Id != id).AnyAsync())
        {
            ModelState.AddModelError(nameof(model.Code), "Bu depo kodu başka bir depo tarafından kullanılıyor.");
            return View(model);
        }

        if (model.IsDefault && !existing.IsDefault)
        {
            var updateAll = Builders<Warehouse>.Update.Set(w => w.IsDefault, false);
            await _context.Warehouses.UpdateManyAsync(w => w.IsDefault, updateAll);
        }
        else if (!model.IsDefault && existing.IsDefault)
        {
            ModelState.AddModelError(nameof(model.IsDefault), "Varsayılan depo özelliğini kaldıramazsınız. Başka bir depoyu varsayılan yapmalısınız.");
            return View(model);
        }

        var filter = Builders<Warehouse>.Filter.Eq(w => w.Id, id);
        var update = Builders<Warehouse>.Update
            .Set(w => w.Name, model.Name)
            .Set(w => w.Code, model.Code)
            .Set(w => w.Address, model.Address)
            .Set(w => w.Phone, model.Phone)
            .Set(w => w.Description, model.Description)
            .Set(w => w.IsDefault, model.IsDefault)
            .Set(w => w.IsActive, model.IsActive);

        await _context.Warehouses.UpdateOneAsync(filter, update);

        // İsim değiştiyse ürünlerdeki WarehouseName bilgisini de güncelle
        if (existing.Name != model.Name)
        {
            var products = await _context.Products.Find(p => p.WarehouseStocks != null && p.WarehouseStocks.Any(ws => ws.WarehouseId == id)).ToListAsync();
            foreach (var p in products)
            {
                var stock = p.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == id);
                if (stock != null)
                {
                    stock.WarehouseName = model.Name;
                    await _context.Products.ReplaceOneAsync(x => x.Id == p.Id, p);
                }
            }
        }

        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
        await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Depo Düzenleme", $"Depo güncellendi: {model.Name} ({model.Code})");

        TempData["success"] = $"Depo '{model.Name}' başarıyla güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var warehouse = await _context.Warehouses.Find(w => w.Id == id).FirstOrDefaultAsync();
        if (warehouse == null) return NotFound();

        if (warehouse.IsDefault)
        {
            TempData["error"] = "Varsayılan depo silinemez!";
            return RedirectToAction(nameof(Index));
        }

        // Stok kontrolü
        var hasStock = await _context.Products.Find(p => p.WarehouseStocks != null && p.WarehouseStocks.Any(ws => ws.WarehouseId == id && ws.Quantity > 0)).AnyAsync();
        if (hasStock)
        {
            TempData["error"] = $"'{warehouse.Name}' deposunda aktif stok bulunmaktadır. Silmeden önce stokları başka bir depoya transfer etmelisiniz.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Warehouses.DeleteOneAsync(w => w.Id == id);

        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
        await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Depo Silme", $"Depo silindi: {warehouse.Name}");

        TempData["success"] = $"Depo '{warehouse.Name}' başarıyla silindi.";
        return RedirectToAction(nameof(Index));
    }
}
