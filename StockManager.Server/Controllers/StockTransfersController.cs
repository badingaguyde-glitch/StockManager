using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class StockTransfersController : Controller
{
    private readonly MongoDBContext _context;
    private readonly TransferPdfService _transferPdfService;
    private readonly IAuditLogService _auditLogService;

    public StockTransfersController(MongoDBContext context, TransferPdfService transferPdfService, IAuditLogService auditLogService)
    {
        _context = context;
        _transferPdfService = transferPdfService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var transfers = await _context.StockTransfers
            .Find(FilterDefinition<StockTransfer>.Empty)
            .SortByDescending(t => t.TransferDate)
            .ToListAsync();

        return View(transfers);
    }

    public async Task<IActionResult> Details(string id)
    {
        var transfer = await _context.StockTransfers.Find(t => t.Id == id).FirstOrDefaultAsync();
        if (transfer == null) return NotFound();

        return View(transfer);
    }

    public async Task<IActionResult> Create()
    {
        var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortBy(w => w.Name).ToListAsync();
        ViewBag.Warehouses = warehouses;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetWarehouseProducts(string warehouseId)
    {
        if (string.IsNullOrEmpty(warehouseId)) return Json(new List<object>());

        var products = await _context.Products
            .Find(p => p.WarehouseStocks != null && p.WarehouseStocks.Any(ws => ws.WarehouseId == warehouseId && ws.Quantity > 0))
            .SortBy(p => p.Name)
            .ToListAsync();

        var result = products.Select(p =>
        {
            var stock = p.WarehouseStocks?.FirstOrDefault(ws => ws.WarehouseId == warehouseId);
            return new
            {
                id = p.Id,
                name = p.Name,
                barcode = p.Barcode ?? "",
                quantity = stock?.Quantity ?? 0
            };
        });

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string sourceWarehouseId, string targetWarehouseId, string? notes, List<string> productIds, List<int> quantities, List<string?> itemNotes)
    {
        var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortBy(w => w.Name).ToListAsync();
        ViewBag.Warehouses = warehouses;

        if (string.IsNullOrEmpty(sourceWarehouseId) || string.IsNullOrEmpty(targetWarehouseId))
        {
            TempData["error"] = "Çıkış ve Giriş depoları seçilmelidir.";
            return View();
        }

        if (sourceWarehouseId == targetWarehouseId)
        {
            TempData["error"] = "Çıkış ve Giriş deposu aynı olamaz!";
            return View();
        }

        if (productIds == null || productIds.Count == 0)
        {
            TempData["error"] = "Transfer için en az bir ürün seçmelisiniz.";
            return View();
        }

        var sourceWarehouse = await _context.Warehouses.Find(w => w.Id == sourceWarehouseId).FirstOrDefaultAsync();
        var targetWarehouse = await _context.Warehouses.Find(w => w.Id == targetWarehouseId).FirstOrDefaultAsync();

        if (sourceWarehouse == null || targetWarehouse == null)
        {
            TempData["error"] = "Geçersiz depo seçimi.";
            return View();
        }

        using var session = await _context.Client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            var count = await _context.StockTransfers.CountDocumentsAsync(session, FilterDefinition<StockTransfer>.Empty);
            var transferNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D3}";

            var transfer = new StockTransfer
            {
                TransferNumber = transferNumber,
                SourceWarehouseId = sourceWarehouseId,
                SourceWarehouseName = sourceWarehouse.Name,
                TargetWarehouseId = targetWarehouseId,
                TargetWarehouseName = targetWarehouse.Name,
                TransferDate = DateTime.UtcNow,
                Status = StockTransferStatus.Completed,
                Notes = notes,
                CreatedBy = User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Sistem Yetkilisi"
            };

            for (int i = 0; i < productIds.Count; i++)
            {
                var pid = productIds[i];
                var qty = quantities[i];
                var iNote = itemNotes != null && i < itemNotes.Count ? itemNotes[i] : null;

                if (string.IsNullOrEmpty(pid) || qty <= 0) continue;

                var product = await _context.Products.Find(session, p => p.Id == pid).FirstOrDefaultAsync();
                if (product == null) continue;

                if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();

                var srcStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == sourceWarehouseId);
                int currentSrcQty = srcStock?.Quantity ?? 0;

                if (currentSrcQty < qty)
                {
                    await session.AbortTransactionAsync();
                    TempData["error"] = $"'{product.Name}' ürününün '{sourceWarehouse.Name}' deposundaki stoğu yetersiz! (Mevcut: {currentSrcQty}, İstenen: {qty})";
                    return View();
                }

                // Çıkış deposundan eksilt
                if (srcStock != null) srcStock.Quantity -= qty;

                // Giriş deposuna ekle
                var tgtStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == targetWarehouseId);
                if (tgtStock != null)
                {
                    tgtStock.Quantity += qty;
                    tgtStock.WarehouseName = targetWarehouse.Name;
                }
                else
                {
                    product.WarehouseStocks.Add(new WarehouseStock
                    {
                        WarehouseId = targetWarehouseId,
                        WarehouseName = targetWarehouse.Name,
                        Quantity = qty
                    });
                }

                // Toplam Quantity aynı kalır (depolar arası yer değiştirdi)
                await _context.Products.ReplaceOneAsync(session, p => p.Id == product.Id, product);

                // Transfer kalemi eklendi
                transfer.Items.Add(new StockTransferItem
                {
                    ProductId = product.Id!,
                    ProductName = product.Name,
                    Barcode = product.Barcode,
                    Quantity = qty,
                    Notes = iNote
                });

                // Stok Hareketleri (Log) oluştur
                var movOut = new StockMovement
                {
                    ProductId = product.Id!,
                    Quantity = qty,
                    Type = StockMovementType.TransferOut,
                    Date = DateTime.UtcNow,
                    WarehouseId = sourceWarehouseId,
                    WarehouseName = sourceWarehouse.Name,
                    Notes = $"[{transferNumber}] '{targetWarehouse.Name}' deposuna transfer çıkışı. {iNote}".Trim()
                };
                await _context.StockMovements.InsertOneAsync(session, movOut);

                var movIn = new StockMovement
                {
                    ProductId = product.Id!,
                    Quantity = qty,
                    Type = StockMovementType.TransferIn,
                    Date = DateTime.UtcNow,
                    WarehouseId = targetWarehouseId,
                    WarehouseName = targetWarehouse.Name,
                    Notes = $"[{transferNumber}] '{sourceWarehouse.Name}' deposundan transfer girişi. {iNote}".Trim()
                };
                await _context.StockMovements.InsertOneAsync(session, movIn);
            }

            if (transfer.Items.Count == 0)
            {
                await session.AbortTransactionAsync();
                TempData["error"] = "Geçerli bir ürün kalemi eklenmedi.";
                return View();
            }

            await _context.StockTransfers.InsertOneAsync(session, transfer);
            await session.CommitTransactionAsync();

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Depolar Arası Transfer", $"Stok transferi gerçekleştirildi: {transferNumber} ({sourceWarehouse.Name} -> {targetWarehouse.Name}, {transfer.Items.Sum(x => x.Quantity)} Adet)");

            TempData["success"] = $"Stok transferi '{transferNumber}' başarıyla tamamlandı.";
            return RedirectToAction(nameof(Details), new { id = transfer.Id });
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            TempData["error"] = $"Transfer işlemi sırasında hata oluştu: {ex.Message}";
            return View();
        }
    }

    public async Task<IActionResult> DownloadPdf(string id)
    {
        var transfer = await _context.StockTransfers.Find(t => t.Id == id).FirstOrDefaultAsync();
        if (transfer == null) return NotFound();

        var sourceWarehouse = await _context.Warehouses.Find(w => w.Id == transfer.SourceWarehouseId).FirstOrDefaultAsync();
        var targetWarehouse = await _context.Warehouses.Find(w => w.Id == transfer.TargetWarehouseId).FirstOrDefaultAsync();

        var pdfBytes = _transferPdfService.GenerateTransferPdf(transfer, sourceWarehouse, targetWarehouse);
        return File(pdfBytes, "application/pdf", $"{transfer.TransferNumber}_Sevk_Irsaliyesi.pdf");
    }
}
