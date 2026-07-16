using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StripeCheckout = Stripe.Checkout;
using StockManager.Server.Services;
using StockManager.Server.Data;
using StockManager.Server.Models;


namespace StockManager.Server.Controllers
{
    [Authorize(Roles = "Admin,Personel")]
    public class SalesController : Controller
    {
        private readonly MongoDBContext _context;
        private readonly StockManager.Server.Services.IEmailService _emailService;
        private readonly StripePaymentService _stripePaymentService;
        private readonly ReceiptPdfService _receiptPdfService;

        private readonly StockManager.Server.Services.IAuditLogService _auditLogService;

        public SalesController(
            MongoDBContext context,
            StripePaymentService stripePaymentService,
            StockManager.Server.Services.IAuditLogService auditLogService,
            ReceiptPdfService receiptPdfService, StockManager.Server.Services.IEmailService emailService)
        {
            _context = context;
            _stripePaymentService = stripePaymentService;
            _auditLogService = auditLogService;
            _receiptPdfService = receiptPdfService;
            _emailService = emailService;
        }

        public async Task<IActionResult> POS()
        {
            ViewBag.Products = await _context.Products
                .Find(FilterDefinition<Product>.Empty)
                .ToListAsync();

            ViewBag.Customers = await _context.Customers
                .Find(FilterDefinition<Customer>.Empty)
                .ToListAsync();

            ViewBag.Warehouses = await _context.Warehouses
                .Find(w => w.IsActive)
                .SortBy(w => w.Name)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(String barcode)
        {
            var product = await _context.Products
                .Find(p => p.Barcode == barcode)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return Json(new { success = false, message = "Ürün bulunamadı." });
            }
            if (product.Quantity <= 0)
            {
                return Json(new { success = false, message = "Ürün stokta yok." });
            }
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name ?? "Belirtilmedi", "Sepete Ürün Ekleme", $"Ürün sepete eklendi: {product.Name} (ID: {product.Id})");

            return Json(new
            {
                success = true,
                id = product.Id,
                name = product.Name,
                salePrice = product.SalePrice,
                quantity = product.Quantity
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Sale sale)
        {
            if (sale.Items == null || sale.Items.Count == 0)
            {
                ModelState.AddModelError("", "Sepet boş olamaz.");
                return RedirectToAction(nameof(POS));
            }

            if (string.IsNullOrEmpty(sale.WarehouseId))
            {
                var defaultWh = await _context.Warehouses.Find(w => w.IsDefault).FirstOrDefaultAsync() ?? await _context.Warehouses.Find(_ => true).FirstOrDefaultAsync();
                sale.WarehouseId = defaultWh?.Id;
                sale.WarehouseName = defaultWh?.Name;
            }
            else if (string.IsNullOrEmpty(sale.WarehouseName))
            {
                var wh = await _context.Warehouses.Find(w => w.Id == sale.WarehouseId).FirstOrDefaultAsync();
                sale.WarehouseName = wh?.Name;
            }

            // 1. Contrôle préalable de la disponibilité
            foreach (var item in sale.Items)
            {
                var product = await _context.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product == null)
                {
                    TempData["error"] = "Seçilen bazı ürünler veritabanında bulunamadı.";
                    return RedirectToAction(nameof(POS));
                }

                if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();
                var whStock = product.WarehouseStocks.FirstOrDefault(ws => ws.WarehouseId == sale.WarehouseId);

                if (product.Quantity < item.Quantity || whStock == null || whStock.Quantity < item.Quantity)
                {
                    var hasUnreadNotification = await _context.Notifications
                        .Find(n => n.ProductId == product.Id && n.Message.Contains("yetersiz") && !n.IsRead)
                        .AnyAsync();

                    if (!hasUnreadNotification)
                    {
                        var notification = new Notification
                        {
                            Message = $"{product.Name} stoğunda yeterli ürün bulunmamaktadır! Satılmak İstenen: {item.Quantity}, Mevcut Stok: {product.Quantity} ({sale.WarehouseName}: {whStock?.Quantity ?? 0})",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow,
                            ProductId = product.Id
                        };
                        await _context.Notifications.InsertOneAsync(notification);
                    }

                    TempData["error"] = $"{product.Name} ürününün '{sale.WarehouseName ?? "Seçilen Depo"}' stoğunda yeterli ürün bulunmamaktadır. (Mevcut Depo Stoğu: {whStock?.Quantity ?? 0})";
                    return RedirectToAction(nameof(POS));
                }
            }

            // Préparation des métadonnées de la vente
            sale.SaleDate = DateTime.UtcNow;
            var count = await _context.Sales.CountDocumentsAsync(FilterDefinition<Sale>.Empty);
            sale.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D3}";
            sale.TotalAmount = sale.Items.Sum(i => i.Quantity * i.UnitPrice);
            sale.Currency = sale.Currency ?? "TRY";

            if (sale.PaymentType == PaymentType.Debt && !string.IsNullOrEmpty(sale.CustomerId))
            {
                var customer = await _context.Customers.Find(c => c.Id == sale.CustomerId).FirstOrDefaultAsync();
                if (customer != null && customer.Balance < sale.TotalAmount && Request.Form["forceDebt"] != "true")
                {
                    TempData["error"] = "Müşteri limiti/bakiyesi yetersiz, işleme devam edilsin mi? (POS ekranındaki uyarıyı onaylayarak devam edebilirsiniz.)";
                    return RedirectToAction(nameof(POS));
                }
            }

            var paymentIntent = await _stripePaymentService.CreatePaymentIntentAsync(
                sale.TotalAmount,
                sale.Currency,
                $"Fatura {sale.InvoiceNumber}",
                new Dictionary<string, string>
                {
            { "invoice_number", sale.InvoiceNumber },
            { "payment_type", sale.PaymentType.ToString() }
                });

            sale.StripePaymentIntentId = paymentIntent.Id;
            sale.StripeStatus = paymentIntent.Status;

            // Liste temporaire pour différer l'envoi d'e-mails
            var notificationsToSend = new List<(Product product, Notification notification)>();

            // 🔄 DÉBUT DE LA TRANSACTION
            using (var session = await _context.Client.StartSessionAsync())
            {
                session.StartTransaction();
                try
                {
                    // a. Enregistrer la vente
                    await _context.Sales.InsertOneAsync(session, sale);

                    // b. Décrémenter les stocks pour chaque article et logger le mouvement
                    foreach (var item in sale.Items)
                    {
                        var productFilter = Builders<Product>.Filter.Eq(p => p.Id, item.ProductId);
                        var product = await _context.Products.Find(session, productFilter).FirstOrDefaultAsync();
                        if (product != null)
                        {
                            product.Quantity -= item.Quantity;
                            if (product.WarehouseStocks == null) product.WarehouseStocks = new List<WarehouseStock>();
                            var ws = product.WarehouseStocks.FirstOrDefault(w => w.WarehouseId == sale.WarehouseId);
                            if (ws != null) ws.Quantity -= item.Quantity;

                            await _context.Products.ReplaceOneAsync(session, productFilter, product);

                            // Parti/Seri/SKT takibi aktifse FIFO/FEFO yöntemiyle partelerden stok düş ve durumu güncelle
                            if (product.HasBatchTracking || product.HasSerialTracking || product.HasExpiryTracking)
                            {
                                var activeBatches = await _context.ProductBatches
                                    .Find(session, b => b.ProductId == product.Id && b.Status == ProductBatchStatus.Active && (string.IsNullOrEmpty(sale.WarehouseId) || b.WarehouseId == sale.WarehouseId))
                                    .SortBy(b => b.ExpiryDate)
                                    .ToListAsync();

                                int remainingToDeduct = item.Quantity;
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

                            var stockMovement = new StockMovement
                            {
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                Type = StockMovementType.StockOut,
                                Date = DateTime.UtcNow,
                                CustomerId = sale.CustomerId,
                                WarehouseId = sale.WarehouseId,
                                WarehouseName = sale.WarehouseName,
                                Notes = $"POS Satışı - Fatura: {sale.InvoiceNumber}"
                            };
                            await _context.StockMovements.InsertOneAsync(session, stockMovement);

                            if (product.Quantity <= product.LowStockThreshold)
                            {
                                var notification = new Notification
                                {
                                    Message = $"{product.Name} (Barkod: {product.Barcode}) kritik stok limitinin altına düştü! Kalan: {product.Quantity}",
                                    IsRead = false,
                                    CreatedAt = DateTime.UtcNow,
                                    ProductId = product.Id
                                };

                                await _context.Notifications.InsertOneAsync(session, notification);
                                notificationsToSend.Add((product, notification));
                            }
                        }
                    }

                    // c. Mettre à jour le solde du client s'il y a une dette

                    if (!string.IsNullOrEmpty(sale.CustomerId))
                    {
                        var customerFilter = Builders<Customer>.Filter.Eq(c => c.Id, sale.CustomerId);
                        var decreaseBalance = Builders<Customer>.Update.Inc(c => c.Balance, -sale.TotalAmount);
                        await _context.Customers.UpdateOneAsync(session, customerFilter, decreaseBalance);
                    }

                    // 💾 Tout s'est bien déroulé -> Validation définitive en base de données
                    await session.CommitTransactionAsync();
                }
                catch (Exception ex)
                {
                    // ❌ En cas d'erreur -> Annulation complète de toutes les modifications
                    await session.AbortTransactionAsync();
                    TempData["error"] = $"Satış işlemi sırasında bir hata oluştu ve değişiklikler geri alındı: {ex.Message}";
                    return RedirectToAction(nameof(POS));
                }
            }
            // 🔄 FIN DE LA TRANSACTION

            // d. Envoi des alertes e-mail (effectué hors transaction pour ne pas ralentir la base de données en cas de latence SMTP)
            if (notificationsToSend.Count > 0)
            {
                try
                {
                    var adminUsers = await _context.Users.Find(u => u.Role == UserRole.Admin).ToListAsync();
                    var adminEmails = adminUsers.Select(u => u.Email).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    if (adminEmails.Count == 0)
                    {
                        adminEmails.Add("admin@stockmanager.com");
                    }

                    string? customerName = null;
                    if (!string.IsNullOrEmpty(sale.CustomerId))
                    {
                        var customer = await _context.Customers.Find(c => c.Id == sale.CustomerId).FirstOrDefaultAsync();
                        customerName = customer?.FullName;
                    }
                    var customerDetails = string.IsNullOrEmpty(customerName) ? "Müşteri Belirtilmedi" : $"Müşteri: {customerName}";

                    foreach (var email in adminEmails)
                    {
                        foreach (var (product, _) in notificationsToSend)
                        {
                            await _emailService.SendLowStockAlertAsync(email, product, DateTime.UtcNow, customerDetails);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"E-posta bildirim hatası (Satış sonrası): {ex.Message}");
                }
            }

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name ?? "Belirtilmedi", "Satış Yapma", $"Satış yapıldı: {sale.InvoiceNumber}, Tutar: {sale.TotalAmount} {sale.Currency}, Ödeme Tipi: {sale.PaymentType}");

            return RedirectToAction(nameof(Invoice), new { id = sale.Id });
        }
        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(string id)
        {
            var sale = await _context.Sales.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (sale == null) return NotFound();

            Customer? customer = null;
            if (!string.IsNullOrEmpty(sale.CustomerId))
            {
                customer = await _context.Customers.Find(c => c.Id == sale.CustomerId).FirstOrDefaultAsync();
            }

            var pdf = _receiptPdfService.GenerateReceiptPdf(sale, customer);
            return File(pdf, "application/pdf", $"fis_{sale.InvoiceNumber}.pdf");
        }

        public async Task<IActionResult> Invoice(string id)
        {
            var sale = await _context.Sales
                .Find(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (sale == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(sale.CustomerId))
            {
                ViewBag.Customer = await _context.Customers
                    .Find(c => c.Id == sale.CustomerId)
                    .FirstOrDefaultAsync();
            }

            return View(sale);
        }

        public async Task<IActionResult> History()
        {
            var sales = await _context.Sales
                .Find(FilterDefinition<Sale>.Empty)
                .SortByDescending(s => s.SaleDate)
                .ToListAsync();

            var customerIds = sales.Select(s => s.CustomerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var customers = await _context.Customers
                .Find(Builders<Customer>.Filter.In(c => c.Id, customerIds))
                .ToListAsync();

            var customerDict = customers
                .Where(c => !string.IsNullOrEmpty(c.Id))
                .GroupBy(c => c.Id!)
                .ToDictionary(g => g.Key, g => g.First().FullName ?? "İsimsiz Müşteri");
            ViewBag.CustomerNames = customerDict;

            return View(sales);
        }

        public async Task<IActionResult> Details(string id)
        {
            var sale = await _context.Sales
                .Find(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (sale == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(sale.CustomerId))
            {
                ViewBag.Customer = await _context.Customers
                    .Find(c => c.Id == sale.CustomerId)
                    .FirstOrDefaultAsync();
            }

            var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Find(Builders<Product>.Filter.In(p => p.Id, productIds))
                .ToListAsync();

            var productDict = products
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .GroupBy(p => p.Id!)
                .ToDictionary(g => g.Key, g => g.First());
            ViewBag.Products = productDict;

            return View(sale);
        }
    }
}