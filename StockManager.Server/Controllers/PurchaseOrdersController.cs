using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;
using System.Security.Claims;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class PurchaseOrdersController : Controller
{
    private readonly MongoDBContext _context;
    private readonly PurchaseOrderPdfService _poPdfService;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;

    public PurchaseOrdersController(
        MongoDBContext context,
        PurchaseOrderPdfService poPdfService,
        IEmailService emailService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _poPdfService = poPdfService;
        _emailService = emailService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? statusFilter, string? supplierId)
    {
        var filterBuilder = Builders<PurchaseOrder>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<PurchaseOrderStatus>(statusFilter, out var status))
        {
            filter &= filterBuilder.Eq(po => po.Status, status);
        }

        if (!string.IsNullOrEmpty(supplierId))
        {
            filter &= filterBuilder.Eq(po => po.SupplierId, supplierId);
        }

        var orders = await _context.PurchaseOrders
            .Find(filter)
            .SortByDescending(po => po.OrderDate)
            .ToListAsync();

        var suppliers = await _context.Suppliers.Find(FilterDefinition<Supplier>.Empty).SortBy(s => s.CompanyName).ToListAsync();
        ViewBag.Suppliers = suppliers;
        ViewBag.CurrentStatus = statusFilter;
        ViewBag.CurrentSupplierId = supplierId;

        return View(orders);
    }

    public async Task<IActionResult> LowStock()
    {
        var lowStockProducts = await _context.Products
            .Find(p => p.Quantity <= p.LowStockThreshold)
            .SortBy(p => p.Quantity)
            .ToListAsync();

        var suppliers = await _context.Suppliers.Find(FilterDefinition<Supplier>.Empty).SortBy(s => s.CompanyName).ToListAsync();
        var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync();

        ViewBag.Suppliers = suppliers;
        ViewBag.Warehouses = warehouses;
        ViewBag.SuppliersMap = suppliers.ToDictionary(s => s.Id ?? "", s => s);

        return View(lowStockProducts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBulk(
        string supplierId,
        string? targetWarehouseId,
        string? notes,
        List<string> productIds,
        List<int> orderedQuantities,
        List<decimal> unitPrices)
    {
        if (string.IsNullOrEmpty(supplierId))
        {
            TempData["error"] = "Tedarikçi seçimi zorunludur.";
            return RedirectToAction(nameof(LowStock));
        }

        if (productIds == null || productIds.Count == 0)
        {
            TempData["error"] = "En az bir ürün seçmelisiniz.";
            return RedirectToAction(nameof(LowStock));
        }

        var supplier = await _context.Suppliers.Find(s => s.Id == supplierId).FirstOrDefaultAsync();
        if (supplier == null)
        {
            TempData["error"] = "Geçersiz tedarikçi seçimi.";
            return RedirectToAction(nameof(LowStock));
        }

        var targetWarehouse = !string.IsNullOrEmpty(targetWarehouseId)
            ? await _context.Warehouses.Find(w => w.Id == targetWarehouseId).FirstOrDefaultAsync()
            : await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync();

        var count = await _context.PurchaseOrders.CountDocumentsAsync(FilterDefinition<PurchaseOrder>.Empty);
        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D3}";

        var po = new PurchaseOrder
        {
            OrderNumber = orderNumber,
            SupplierId = supplier.Id!,
            SupplierName = supplier.CompanyName,
            SupplierEmail = supplier.Email,
            TargetWarehouseId = targetWarehouse?.Id,
            TargetWarehouseName = targetWarehouse?.Name ?? "Ana Depo",
            OrderDate = DateTime.UtcNow,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7),
            Status = PurchaseOrderStatus.Draft,
            Notes = notes,
            CreatedBy = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "Satın Alma Yetkilisi"
        };

        decimal totalAmount = 0;
        for (int i = 0; i < productIds.Count; i++)
        {
            var pid = productIds[i];
            var qty = orderedQuantities[i];
            var price = unitPrices[i];

            if (string.IsNullOrEmpty(pid) || qty <= 0) continue;

            var product = await _context.Products.Find(p => p.Id == pid).FirstOrDefaultAsync();
            if (product == null) continue;

            var item = new PurchaseOrderItem
            {
                ProductId = product.Id!,
                ProductName = product.Name,
                Barcode = product.Barcode,
                CurrentStock = product.Quantity,
                LowStockThreshold = product.LowStockThreshold,
                OrderedQuantity = qty,
                UnitPrice = price
            };

            po.Items.Add(item);
            totalAmount += item.TotalPrice;
        }

        if (po.Items.Count == 0)
        {
            TempData["error"] = "Seçilen ürünler işlenemedi veya miktarlar geçersiz.";
            return RedirectToAction(nameof(LowStock));
        }

        po.TotalAmount = totalAmount;

        // E-Posta gönderimi ve PDF üretimi
        var pdfBytes = _poPdfService.GeneratePurchaseOrderPdf(po, supplier);
        if (!string.IsNullOrEmpty(supplier.Email))
        {
            var subject = $"📦 Satın Alma Siparişi #{po.OrderNumber} - StockManager Pro";
            var body = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px;'>
    <h2 style='color: #2563eb; margin-top: 0;'>Satın Alma Siparişi #{po.OrderNumber}</h2>
    <p>Sayın <strong>{supplier.CompanyName}</strong> Yetkilisi,</p>
    <p>Firmanıza iletilen yeni satın alma siparişimize ait detaylı kurumsal sipariş formu PDF belgesi ekte bilgilerinize sunulmuştur.</p>
    <table style='width: 100%; border-collapse: collapse; margin: 15px 0; background: #f8fafc; border-radius: 6px;'>
        <tr><td style='padding: 8px; font-weight: bold;'>Sipariş Numarası:</td><td style='padding: 8px;'>{po.OrderNumber}</td></tr>
        <tr><td style='padding: 8px; font-weight: bold;'>Sipariş Tarihi:</td><td style='padding: 8px;'>{po.OrderDate.ToLocalTime():dd.MM.yyyy}</td></tr>
        <tr><td style='padding: 8px; font-weight: bold;'>Kalem Sayısı:</td><td style='padding: 8px;'>{po.Items.Count} Çeşit Ürün</td></tr>
        <tr><td style='padding: 8px; font-weight: bold;'>Toplam Tutar:</td><td style='padding: 8px; font-weight: bold; color: #2563eb;'>{po.TotalAmount:N2} ₺</td></tr>
    </table>
    <p style='font-size: 14px; color: #475569;'>Lütfen sipariş belgesini inceleyip onayı ve sevk tarihini tarafımıza bildiriniz.</p>
    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
    <p style='font-size: 12px; color: #94a3b8; margin: 0;'>StockManager Satın Alma Sistemi</p>
</div>";

            await _emailService.SendEmailWithAttachmentAsync(supplier.Email, subject, body, pdfBytes, $"PO_{po.OrderNumber}.pdf");
            po.Status = PurchaseOrderStatus.Sent;
            po.EmailSentAt = DateTime.UtcNow;
        }

        await _context.PurchaseOrders.InsertOneAsync(po);

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Satın Alma Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "PO Oluşturuldu", $"Sipariş #{po.OrderNumber} oluşturuldu ve {(po.Status == PurchaseOrderStatus.Sent ? supplier.Email + " adresine e-posta atıldı" : "kaydedildi")}.");

        TempData["success"] = $"Satın Alma Siparişi #{po.OrderNumber} başarıyla oluşturuldu {(po.Status == PurchaseOrderStatus.Sent ? "ve tedarikçiye e-posta ile PDF iletildi!" : "")}";
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAutoForAll()
    {
        var lowStockProducts = await _context.Products
            .Find(p => p.Quantity <= p.LowStockThreshold && !string.IsNullOrEmpty(p.SupplierId))
            .ToListAsync();

        if (lowStockProducts.Count == 0)
        {
            TempData["info"] = "Tedarikçisi tanımlı olan herhangi bir düşük stoklu ürün bulunmamaktadır.";
            return RedirectToAction(nameof(LowStock));
        }

        var supplierGroups = lowStockProducts.GroupBy(p => p.SupplierId).ToList();
        var defaultWarehouse = await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync();
        int createdCount = 0;
        int emailSentCount = 0;

        foreach (var group in supplierGroups)
        {
            var supplierId = group.Key;
            if (string.IsNullOrEmpty(supplierId)) continue;

            var supplier = await _context.Suppliers.Find(s => s.Id == supplierId).FirstOrDefaultAsync();
            if (supplier == null) continue;

            var count = await _context.PurchaseOrders.CountDocumentsAsync(FilterDefinition<PurchaseOrder>.Empty);
            var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{count + 1 + createdCount:D3}";

            var po = new PurchaseOrder
            {
                OrderNumber = orderNumber,
                SupplierId = supplier.Id!,
                SupplierName = supplier.CompanyName,
                SupplierEmail = supplier.Email,
                TargetWarehouseId = defaultWarehouse?.Id,
                TargetWarehouseName = defaultWarehouse?.Name ?? "Ana Depo",
                OrderDate = DateTime.UtcNow,
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7),
                Status = PurchaseOrderStatus.Draft,
                CreatedBy = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "Otomatik PO Motoru"
            };

            decimal totalAmount = 0;
            foreach (var product in group)
            {
                // Kritik eşiğin 2 katına tamamlama (veya minimum 10 adet sipariş verme)
                int orderQty = Math.Max(10, (product.LowStockThreshold * 2) - product.Quantity);
                var item = new PurchaseOrderItem
                {
                    ProductId = product.Id!,
                    ProductName = product.Name,
                    Barcode = product.Barcode,
                    CurrentStock = product.Quantity,
                    LowStockThreshold = product.LowStockThreshold,
                    OrderedQuantity = orderQty,
                    UnitPrice = product.PurchasePrice > 0 ? product.PurchasePrice : product.SalePrice * 0.7m
                };
                po.Items.Add(item);
                totalAmount += item.TotalPrice;
            }

            po.TotalAmount = totalAmount;

            var pdfBytes = _poPdfService.GeneratePurchaseOrderPdf(po, supplier);
            if (!string.IsNullOrEmpty(supplier.Email))
            {
                var subject = $"📦 Otomatik Satın Alma Siparişi #{po.OrderNumber} - StockManager Pro";
                var body = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px;'>
    <h2 style='color: #2563eb; margin-top: 0;'>Satın Alma Siparişi #{po.OrderNumber}</h2>
    <p>Sayın <strong>{supplier.CompanyName}</strong> Yetkilisi,</p>
    <p>StockManager Pro otomatik stok ikmal sistemi tarafından düşük stok seviyeleri nedeniyle oluşturulan yeni satın alma siparişimiz ekte PDF olarak iletilmiştir.</p>
    <p><strong>Kalem Sayısı:</strong> {po.Items.Count} Çeşit | <strong>Toplam Tutar:</strong> {po.TotalAmount:N2} ₺</p>
    <p style='font-size: 14px; color: #475569;'>Lütfen siparişi onaylayarak sevk sürecini başlatınız.</p>
</div>";
                await _emailService.SendEmailWithAttachmentAsync(supplier.Email, subject, body, pdfBytes, $"PO_{po.OrderNumber}.pdf");
                po.Status = PurchaseOrderStatus.Sent;
                po.EmailSentAt = DateTime.UtcNow;
                emailSentCount++;
            }

            await _context.PurchaseOrders.InsertOneAsync(po);
            createdCount++;
        }

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Satın Alma Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "Otomatik PO Oluşturuldu", $"{createdCount} tedarikçi için otomatik satın alma siparişi oluşturuldu ({emailSentCount} e-posta iletildi).");

        TempData["success"] = $"{createdCount} farklı tedarikçi için otomatik Satın Alma Siparişi (PO) oluşturuldu ve {emailSentCount} tedarikçiye PDF olarak e-posta gönderildi!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var po = await _context.PurchaseOrders.Find(po => po.Id == id).FirstOrDefaultAsync();
        if (po == null) return NotFound();

        var supplier = await _context.Suppliers.Find(s => s.Id == po.SupplierId).FirstOrDefaultAsync();
        ViewBag.Supplier = supplier;

        return View(po);
    }

    public async Task<IActionResult> DownloadPdf(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var po = await _context.PurchaseOrders.Find(po => po.Id == id).FirstOrDefaultAsync();
        if (po == null) return NotFound();

        var supplier = await _context.Suppliers.Find(s => s.Id == po.SupplierId).FirstOrDefaultAsync();
        var pdfBytes = _poPdfService.GeneratePurchaseOrderPdf(po, supplier);

        return File(pdfBytes, "application/pdf", $"SatinAlmaSiparisi_{po.OrderNumber}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var po = await _context.PurchaseOrders.Find(po => po.Id == id).FirstOrDefaultAsync();
        if (po == null) return NotFound();

        var supplier = await _context.Suppliers.Find(s => s.Id == po.SupplierId).FirstOrDefaultAsync();
        if (supplier == null || string.IsNullOrEmpty(supplier.Email))
        {
            TempData["error"] = "Tedarikçinin e-posta adresi bulunamadı.";
            return RedirectToAction(nameof(Details), new { id = po.Id });
        }

        var pdfBytes = _poPdfService.GeneratePurchaseOrderPdf(po, supplier);
        var subject = $"📦 Satın Alma Siparişi #{po.OrderNumber} - StockManager Pro";
        var body = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px;'>
    <h2 style='color: #2563eb; margin-top: 0;'>Satın Alma Siparişi #{po.OrderNumber}</h2>
    <p>Sayın <strong>{supplier.CompanyName}</strong> Yetkilisi,</p>
    <p>Firmanıza ait Satın Alma Sipariş belgemiz ekte PDF olarak bilginize sunulmuştur.</p>
    <p><strong>Toplam Tutar:</strong> {po.TotalAmount:N2} ₺</p>
</div>";

        await _emailService.SendEmailWithAttachmentAsync(supplier.Email, subject, body, pdfBytes, $"PO_{po.OrderNumber}.pdf");

        po.Status = PurchaseOrderStatus.Sent;
        po.EmailSentAt = DateTime.UtcNow;
        await _context.PurchaseOrders.ReplaceOneAsync(p => p.Id == po.Id, po);

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Satın Alma Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "PO E-Postası İletildi", $"Sipariş #{po.OrderNumber} PDF belgesi {supplier.Email} adresine gönderildi.");

        TempData["success"] = $"Sipariş PDF belgesi {supplier.Email} adresine başarıyla gönderildi.";
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string id, PurchaseOrderStatus status)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var po = await _context.PurchaseOrders.Find(po => po.Id == id).FirstOrDefaultAsync();
        if (po == null) return NotFound();

        var oldStatus = po.Status;
        po.Status = status;

        // Eğer ilk kez 'Completed / Teslim Alındı' yapılıyorsa stokları otomatik artır!
        if (status == PurchaseOrderStatus.Completed && oldStatus != PurchaseOrderStatus.Completed)
        {
            using var session = await _context.Client.StartSessionAsync();
            session.StartTransaction();
            try
            {
                foreach (var item in po.Items)
                {
                    var product = await _context.Products.Find(session, p => p.Id == item.ProductId).FirstOrDefaultAsync();
                    if (product == null) continue;

                    product.Quantity += item.OrderedQuantity;

                    if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();
                    var wStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == po.TargetWarehouseId);
                    if (wStock != null)
                    {
                        wStock.Quantity += item.OrderedQuantity;
                    }
                    else if (!string.IsNullOrEmpty(po.TargetWarehouseId))
                    {
                        product.WarehouseStocks.Add(new WarehouseStock
                        {
                            WarehouseId = po.TargetWarehouseId,
                            WarehouseName = po.TargetWarehouseName ?? "Ana Depo",
                            Quantity = item.OrderedQuantity
                        });
                    }

                    await _context.Products.ReplaceOneAsync(session, p => p.Id == product.Id, product);

                    var movement = new StockMovement
                    {
                        ProductId = product.Id!,
                        Quantity = item.OrderedQuantity,
                        Type = StockMovementType.StockIn,
                        Notes = $"PO #{po.OrderNumber} teslimatı - {po.SupplierName}",
                        WarehouseId = po.TargetWarehouseId,
                        WarehouseName = po.TargetWarehouseName ?? "Ana Depo"
                    };
                    await _context.StockMovements.InsertOneAsync(session, movement);
                }

                await session.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync();
                TempData["error"] = $"Stok güncelleme sırasında hata: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = po.Id });
            }
        }

        await _context.PurchaseOrders.ReplaceOneAsync(p => p.Id == po.Id, po);

        var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "sistem@stockmanager.com";
        var currentUserName = User.Identity?.Name ?? "Satın Alma Yetkilisi";
        await _auditLogService.LogActionAsync(currentUserEmail, currentUserName, "PO Durumu Güncellendi", $"Sipariş #{po.OrderNumber} durumu '{status}' olarak değiştirildi.");

        TempData["success"] = $"Sipariş durumu başarıyla '{status}' olarak güncellendi.";
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }
}
