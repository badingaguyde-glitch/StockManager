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
            ViewBag.Products = products.Select(p => new SelectListItem($"{p.Name} (Stok: {p.Quantity})", p.Id)).ToList();
            ViewBag.Suppliers = suppliers.Select(s => new SelectListItem(s.CompanyName, s.Id)).ToList();
            ViewBag.Customers = customers.Select(c => new SelectListItem(c.FullName, c.Id)).ToList();
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
                return View(movement);
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

                    // a. Enregistrer le mouvement (en passant le paramètre session)
                    await _context.StockMovements.InsertOneAsync(session, movement);

                    var productFilter = Builders<Product>.Filter.Eq(p => p.Id, movement.ProductId);

                    // b. Appliquer le mouvement sur la quantité du produit
                    if (movement.Type == StockMovementType.StockIn)
                    {
                        var increaseQty = Builders<Product>.Update.Inc(p => p.Quantity, movement.Quantity);
                        await _context.Products.UpdateOneAsync(session, productFilter, increaseQty);

                        // c. Ajuster la dette/crédit chez le fournisseur (si applicable)
                        if (!string.IsNullOrEmpty(movement.SupplierId))
                        {
                            var product = await _context.Products.Find(session, productFilter).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                var totalCost = product.PurchasePrice * movement.Quantity;
                                var supplierFilter = Builders<Supplier>.Filter.Eq(s => s.Id, movement.SupplierId);
                                var increaseBalance = Builders<Supplier>.Update.Inc(s => s.Balance, totalCost);

                                await _context.Suppliers.UpdateOneAsync(session, supplierFilter, increaseBalance);
                            }
                        }
                    }
                    else if (movement.Type == StockMovementType.StockOut)
                    {
                        var decreaseQty = Builders<Product>.Update.Inc(p => p.Quantity, -movement.Quantity);
                        await _context.Products.UpdateOneAsync(session, productFilter, decreaseQty);
                    }
                    else if (movement.Type == StockMovementType.Adjustment)
                    {
                        var setQty = Builders<Product>.Update.Set(p => p.Quantity, movement.Quantity);
                        await _context.Products.UpdateOneAsync(session, productFilter, setQty);
                    }

                    // Récupérer le produit final pour vérifier le stock
                    checkProduct = await _context.Products.Find(session, p => p.Id == movement.ProductId).FirstOrDefaultAsync();
                    if (checkProduct != null && checkProduct.Quantity <= checkProduct.LowStockThreshold)
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
    }
}