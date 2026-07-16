using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StockManager.Server.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockManager.Server.Models;
using MongoDB.Driver;

namespace StockManager.Server.Controllers
{
    [Authorize(Roles = "Admin,Personel")]
    public class StockMovementsController : Controller
    {
        private readonly MongoDBContext _context;
        private readonly StockManager.Server.Services.IEmailService _emailService;

        private readonly StockManager.Server.Services.IAuditLogService _auditLogService;

        public StockMovementsController(MongoDBContext context, StockManager.Server.Services.IEmailService emailService, StockManager.Server.Services.IAuditLogService auditLogService)
        {
            _context = context;
            _emailService = emailService;
            _auditLogService = auditLogService;
        }
        public async Task<IActionResult> Index()
        {
            var movements = await _context.StockMovements
                .Find(FilterDefinition<StockMovement>.Empty)
                .SortByDescending(m => m.Date)
                .ToListAsync();

            movements = movements.Where(m=> !string.IsNullOrEmpty(m.ProductId)).ToList();

            var productIds = movements.Where(m => !string.IsNullOrEmpty(m.ProductId)).Select(m => m.ProductId).Distinct().ToList();
            var supplierIds = movements.Where(m => !string.IsNullOrEmpty(m.SupplierId)).Select(m => m.SupplierId).Distinct().ToList();
            var customerIds = movements.Where(m => !string.IsNullOrEmpty(m.CustomerId)).Select(m => m.CustomerId).Distinct().ToList();
            var products = await _context.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
            var suppliers = await _context.Suppliers.Find(s => supplierIds.Contains(s.Id)).ToListAsync();
            var customers = await _context.Customers.Find(c => customerIds.Contains(c.Id)).ToListAsync();

            // Sözlükleri güvenli oluştur (Null ve mükerrer anahtar korumalı)
            ViewBag.ProductNames = products
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .GroupBy(p => p.Id!)
                .ToDictionary(g => g.Key, g => g.First().Name ?? "Bilinmeyen Ürün");

            ViewBag.SupplierNames = suppliers
                .Where(s => !string.IsNullOrEmpty(s.Id))
                .GroupBy(s => s.Id!)
                .ToDictionary(g => g.Key, g => g.First().CompanyName ?? "Bilinmeyen Tedarikçi");

            ViewBag.CustomerNames = customers
                .Where(c => !string.IsNullOrEmpty(c.Id))
                .GroupBy(c => c.Id!)
                .ToDictionary(g => g.Key, g => g.First().FullName ?? "Bilinmeyen Müşteri");

            return View(movements);
        }

        public async Task<IActionResult> Details(string id)
        {
            var movement = await _context.StockMovements
                .Find(m => m.Id == id)
                .FirstOrDefaultAsync();

            if (movement == null) return NotFound();

            var product = await _context.Products.Find(p => p.Id == movement.ProductId).FirstOrDefaultAsync();
            ViewBag.ProductName = product?.Name ?? "Bilinmeyen Ürün";

            if (!string.IsNullOrEmpty(movement.SupplierId))
            {
                var supplier = await _context.Suppliers.Find(s => s.Id == movement.SupplierId).FirstOrDefaultAsync();
                ViewBag.SupplierName = supplier?.CompanyName ?? "Bilinmeyen Tedarikçi";

            }

            if (!string.IsNullOrEmpty(movement.CustomerId))
            {
                var customer = await _context.Customers.Find(c => c.Id == movement.CustomerId).FirstOrDefaultAsync();
                ViewBag.CustomerName = customer?.FullName ?? "Bilinmeyen Müşteri";
            }

            return View(movement);
        }

        public async Task<IActionResult> Create()
        {
            var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
            var suppliers = await _context.Suppliers.Find(FilterDefinition<Supplier>.Empty).SortBy(s => s.CompanyName).ToListAsync();
            var customers = await _context.Customers.Find(FilterDefinition<Customer>.Empty).SortBy(c => c.FullName).ToListAsync();
            var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortBy(w => w.Name).ToListAsync();
            ViewBag.Products = products.Select(p => new SelectListItem($"{p.Name} (Stok: {p.Quantity})", p.Id)).ToList();
            ViewBag.Suppliers = suppliers.Select(s => new SelectListItem(s.CompanyName, s.Id)).ToList();
            ViewBag.Customers = customers.Select(c => new SelectListItem(c.FullName, c.Id)).ToList();
            ViewBag.Warehouses = warehouses;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockMovement movement)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(await _context.Products.Find(_ => true).ToListAsync(), "Id", "Name", movement.ProductId);
                ViewBag.Suppliers = new SelectList(await _context.Suppliers.Find(_ => true).ToListAsync(), "Id", "CompanyName", movement.SupplierId);
                ViewBag.Customers = new SelectList(await _context.Customers.Find(_ => true).ToListAsync(), "Id", "FullName", movement.CustomerId);
                ViewBag.Warehouses = await _context.Warehouses.Find(w => w.IsActive).SortBy(w => w.Name).ToListAsync();
                return View(movement);
            }

            if (string.IsNullOrEmpty(movement.WarehouseId))
            {
                var defaultWh = await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync() ?? await _context.Warehouses.Find(_ => true).FirstOrDefaultAsync();
                movement.WarehouseId = defaultWh?.Id;
                movement.WarehouseName = defaultWh?.Name;
            }
            else if (string.IsNullOrEmpty(movement.WarehouseName))
            {
                var wh = await _context.Warehouses.Find(w => w.Id == movement.WarehouseId).FirstOrDefaultAsync();
                movement.WarehouseName = wh?.Name;
            }

            Product? checkProduct = null;
            Notification? lowStockNotification = null;

            // 🔄 DÉBUT DE LA TRANSACTION
            using (var session = await _context.Client.StartSessionAsync())
            {
                session.StartTransaction();
                try
                {
                    movement.Date = DateTime.UtcNow;

                    var productFilter = Builders<Product>.Filter.Eq(p => p.Id, movement.ProductId);
                    checkProduct = await _context.Products.Find(session, productFilter).FirstOrDefaultAsync();
                    if (checkProduct == null)
                    {
                        await session.AbortTransactionAsync();
                        TempData["error"] = "Seçilen ürün bulunamadı.";
                        return RedirectToAction(nameof(Create));
                    }

                    if (checkProduct.WarehouseStocks == null) checkProduct.WarehouseStocks = new List<WarehouseStock>();
                    var whStock = checkProduct.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == movement.WarehouseId);

                    // b. Appliquer le mouvement sur la quantité du produit
                    if (movement.Type == StockMovementType.StockIn)
                    {
                        checkProduct.Quantity += movement.Quantity;
                        if (whStock != null)
                        {
                            whStock.Quantity += movement.Quantity;
                            whStock.WarehouseName = movement.WarehouseName ?? whStock.WarehouseName;
                        }
                        else
                        {
                            checkProduct.WarehouseStocks.Add(new WarehouseStock
                            {
                                WarehouseId = movement.WarehouseId!,
                                WarehouseName = movement.WarehouseName ?? "Depo",
                                Quantity = movement.Quantity
                            });
                        }

                        await _context.Products.ReplaceOneAsync(session, productFilter, checkProduct);

                        // c. Ajuster la dette/crédit chez le fournisseur (si applicable)
                        if (!string.IsNullOrEmpty(movement.SupplierId))
                        {
                            var totalCost = checkProduct.PurchasePrice * movement.Quantity;
                            var supplierFilter = Builders<Supplier>.Filter.Eq(s => s.Id, movement.SupplierId);
                            var increaseBalance = Builders<Supplier>.Update.Inc(s => s.Balance, totalCost);
                            await _context.Suppliers.UpdateOneAsync(session, supplierFilter, increaseBalance);
                        }
                    }
                    else if (movement.Type == StockMovementType.StockOut)
                    {
                        if (whStock == null || whStock.Quantity < movement.Quantity)
                        {
                            await session.AbortTransactionAsync();
                            TempData["error"] = $"'{checkProduct.Name}' ürününün '{movement.WarehouseName}' deposunda yeterli stoğu yok! (Mevcut: {whStock?.Quantity ?? 0})";
                            return RedirectToAction(nameof(Create));
                        }

                        checkProduct.Quantity -= movement.Quantity;
                        whStock.Quantity -= movement.Quantity;
                        await _context.Products.ReplaceOneAsync(session, productFilter, checkProduct);

                        // Parti/Seri/SKT takibi aktifse FIFO/FEFO yöntemiyle partilerden stok düş ve durumu güncelle
                        if (checkProduct.HasBatchTracking || checkProduct.HasSerialTracking || checkProduct.HasExpiryTracking)
                        {
                            var activeBatches = await _context.ProductBatches
                                .Find(session, b => b.ProductId == checkProduct.Id && b.Status == ProductBatchStatus.Active && (string.IsNullOrEmpty(movement.WarehouseId) || b.WarehouseId == movement.WarehouseId))
                                .SortBy(b => b.ExpiryDate)
                                .ToListAsync();

                            int remainingToDeduct = movement.Quantity;
                            foreach (var batch in activeBatches)
                            {
                                if (remainingToDeduct <= 0) break;

                                if (batch.Quantity <= remainingToDeduct)
                                {
                                    remainingToDeduct -= batch.Quantity;
                                    batch.Quantity = 0;
                                    batch.Status = ProductBatchStatus.Sold;
                                }
                                else
                                {
                                    batch.Quantity -= remainingToDeduct;
                                    remainingToDeduct = 0;
                                }

                                var batchFilter = Builders<ProductBatch>.Filter.Eq(b => b.Id, batch.Id);
                                await _context.ProductBatches.ReplaceOneAsync(session, batchFilter, batch);
                            }
                        }

                        if (!string.IsNullOrEmpty(movement.SupplierId))
                        {
                            var totalCost = checkProduct.PurchasePrice * movement.Quantity;
                            var supplierFilter = Builders<Supplier>.Filter.Eq(s => s.Id, movement.SupplierId);
                            var decreaseBalance = Builders<Supplier>.Update.Inc(s => s.Balance, -totalCost);
                            await _context.Suppliers.UpdateOneAsync(session, supplierFilter, decreaseBalance);
                        }
                    }
                    else if (movement.Type == StockMovementType.Adjustment)
                    {
                        if (whStock != null)
                        {
                            whStock.Quantity = movement.Quantity;
                            whStock.WarehouseName = movement.WarehouseName ?? whStock.WarehouseName;
                        }
                        else
                        {
                            checkProduct.WarehouseStocks.Add(new WarehouseStock
                            {
                                WarehouseId = movement.WarehouseId!,
                                WarehouseName = movement.WarehouseName ?? "Depo",
                                Quantity = movement.Quantity
                            });
                        }
                        checkProduct.Quantity = checkProduct.WarehouseStocks.Sum(ws => ws.Quantity);
                        await _context.Products.ReplaceOneAsync(session, productFilter, checkProduct);
                    }

                    // a. Enregistrer le mouvement
                    await _context.StockMovements.InsertOneAsync(session, movement);

                    if (checkProduct.Quantity <= checkProduct.LowStockThreshold)

                    {
                        lowStockNotification = new Notification
                        {
                            Message = $"{checkProduct.Name} (Barkod: {checkProduct.Barcode}) kritik stok limitinin altına düştü! Kalan: {checkProduct.Quantity}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow,
                            ProductId = checkProduct.Id
                        };

                        await _context.Notifications.InsertOneAsync(session, lowStockNotification);
                    }

                    // 💾 Validation des changements
                    await session.CommitTransactionAsync();
                }
                catch (Exception ex)
                {
                    // ❌ Annulation complète en cas d'échec
                    await session.AbortTransactionAsync();
                    ModelState.AddModelError("", $"İşlem sırasında bir hata oluştu ve iptal edildi: {ex.Message}");

                    ViewBag.Products = new SelectList(await _context.Products.Find(_ => true).ToListAsync(), "Id", "Name", movement.ProductId);
                    ViewBag.Suppliers = new SelectList(await _context.Suppliers.Find(_ => true).ToListAsync(), "Id", "CompanyName", movement.SupplierId);
                    ViewBag.Customers = new SelectList(await _context.Customers.Find(_ => true).ToListAsync(), "Id", "FullName", movement.CustomerId);
                    return View(movement);
                }
            }
            // 🔄 FIN DE LA TRANSACTION

            // d. Envoi des e-mails d'alerte et journalisation hors transaction
            if (lowStockNotification != null && checkProduct != null)
            {
                try
                {
                    var adminUsers = await _context.Users.Find(u => u.Role == UserRole.Admin).ToListAsync();
                    var adminEmails = adminUsers.Select(u => u.Email).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    if (adminEmails.Count == 0)
                    {
                        adminEmails.Add("admin@stockmanager.com");
                    }

                    string? supplierOrCustomerName = null;
                    if (!string.IsNullOrEmpty(movement.SupplierId))
                    {
                        var supplier = await _context.Suppliers.Find(s => s.Id == movement.SupplierId).FirstOrDefaultAsync();
                        supplierOrCustomerName = $"Tedarikçi: {supplier?.CompanyName}";
                    }
                    else if (!string.IsNullOrEmpty(movement.CustomerId))
                    {
                        var customer = await _context.Customers.Find(c => c.Id == movement.CustomerId).FirstOrDefaultAsync();
                        supplierOrCustomerName = $"Müşteri: {customer?.FullName}";
                    }
                    else
                    {
                        supplierOrCustomerName = "Belirtilmedi";
                    }

                    foreach (var email in adminEmails)
                    {
                        await _emailService.SendLowStockAlertAsync(email, checkProduct, DateTime.UtcNow, supplierOrCustomerName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"E-posta bildirim hatası (Mouvement stock): {ex.Message}");
                }
            }

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Stok Hareketi Oluşturma", $"Yeni stok hareketi oluşturuldu: {movement.Type}, Ürün: {checkProduct?.Name}");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SupplierReturn()
        {
            var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
            var suppliers = await _context.Suppliers.Find(FilterDefinition<Supplier>.Empty).SortBy(s => s.CompanyName).ToListAsync();
            var warehouses = await _context.Warehouses.Find(w => w.IsActive).SortBy(w => w.Name).ToListAsync();
            ViewBag.Products = products.Select(p => new SelectListItem($"{p.Name} (Stok: {p.Quantity} | Alış: {p.PurchasePrice:N2} ₺)", p.Id)).ToList();
            ViewBag.Suppliers = suppliers.Select(s => new SelectListItem($"{s.CompanyName} (Bakiye: {s.Balance:N2} ₺)", s.Id)).ToList();
            ViewBag.Warehouses = warehouses;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupplierReturn(string productId, string supplierId, int quantity, string? notes, string? warehouseId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(supplierId) || quantity <= 0)
            {
                TempData["error"] = "Lütfen ürün, tedarikçi ve geçerli bir miktar seçiniz.";
                return RedirectToAction(nameof(SupplierReturn));
            }

            if (string.IsNullOrEmpty(warehouseId))
            {
                var defaultWh = await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync() ?? await _context.Warehouses.Find(_ => true).FirstOrDefaultAsync();
                warehouseId = defaultWh?.Id;
            }

            var warehouse = await _context.Warehouses.Find(w => w.Id == warehouseId).FirstOrDefaultAsync();

            Product? product = null;
            using (var session = await _context.Client.StartSessionAsync())
            {
                session.StartTransaction();
                try
                {
                    product = await _context.Products.Find(session, p => p.Id == productId).FirstOrDefaultAsync();
                    if (product == null)
                    {
                        TempData["error"] = "Seçilen ürün bulunamadı.";
                        await session.AbortTransactionAsync();
                        return RedirectToAction(nameof(SupplierReturn));
                    }

                    if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();
                    var whStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == warehouseId);

                    if (whStock == null || whStock.Quantity < quantity)
                    {
                        TempData["error"] = $"'{product.Name}' ürününün '{warehouse?.Name ?? "Seçilen Depo"}' deposunda yeterli stoğu yok! (Mevcut: {whStock?.Quantity ?? 0})";
                        await session.AbortTransactionAsync();
                        return RedirectToAction(nameof(SupplierReturn));
                    }

                    var movement = new StockMovement
                    {
                        ProductId = productId,
                        SupplierId = supplierId,
                        Quantity = quantity,
                        Type = StockMovementType.StockOut,
                        Date = DateTime.UtcNow,
                        WarehouseId = warehouseId,
                        WarehouseName = warehouse?.Name,
                        Notes = string.IsNullOrWhiteSpace(notes) ? $"Tedarikçiye Stok İadesi ({warehouse?.Name})" : notes
                    };

                    await _context.StockMovements.InsertOneAsync(session, movement);

                    product.Quantity -= quantity;
                    whStock.Quantity -= quantity;
                    var productFilter = Builders<Product>.Filter.Eq(p => p.Id, productId);
                    await _context.Products.ReplaceOneAsync(session, productFilter, product);

                    var totalReturnCost = product.PurchasePrice * quantity;
                    var supplierFilter = Builders<Supplier>.Filter.Eq(s => s.Id, supplierId);
                    var decreaseBalance = Builders<Supplier>.Update.Inc(s => s.Balance, -totalReturnCost);
                    await _context.Suppliers.UpdateOneAsync(session, supplierFilter, decreaseBalance);

                    await session.CommitTransactionAsync();
                }
                catch (Exception ex)
                {
                    await session.AbortTransactionAsync();
                    TempData["error"] = $"İade işlemi sırasında hata oluştu: {ex.Message}";
                    return RedirectToAction(nameof(SupplierReturn));
                }
            }

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Tedarikçi Stok İadesi", $"Tedarikçiye stok iadesi yapıldı ({warehouse?.Name}). Ürün: {product?.Name}, Miktar: {quantity}, Düşülen Tutar: {(product?.PurchasePrice * quantity):N2} ₺");

            TempData["success"] = $"Stok iadesi ({warehouse?.Name}) başarıyla tamamlandı. {product?.Name} ürününden {quantity} adet çıkış yapıldı ve tedarikçi bakiyesinden {(product?.PurchasePrice * quantity):N2} ₺ düşüldü.";
            return RedirectToAction(nameof(Index));
        }
    }
}