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

            var productIds = movements.Where(m=> !string.IsNullOrEmpty(m.ProductId)).Select(m => m.ProductId).Distinct().ToList();
            var supplierIds = movements.Where(m => !string.IsNullOrEmpty(m.SupplierId)).Select(m => m.SupplierId).Distinct().ToList();
            var customerIds = movements.Where(m => !string.IsNullOrEmpty(m.CustomerId)).Select(m => m.CustomerId).Distinct().ToList();
            var products = await _context.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
            var suppliers = await _context.Suppliers.Find(s => supplierIds.Contains(s.Id)).ToListAsync();
            var customers = await _context.Customers.Find(c => customerIds.Contains(c.Id)).ToListAsync();

            // Sözlükleri oluşturup ViewBag'e aktar (Null filtrelemeli ve uyarısız)
            ViewBag.ProductNames = products.Where(p => p.Id != null).ToDictionary(p => p.Id!, p => p.Name);
            ViewBag.SupplierNames = suppliers.Where(s => s.Id != null).ToDictionary(s => s.Id!, s => s.CompanyName);
            ViewBag.CustomerNames = customers.Where(c => c.Id != null).ToDictionary(c => c.Id!, c => c.FullName);
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
                var products = await _context.Products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
                var suppliers = await _context.Suppliers.Find(FilterDefinition<Supplier>.Empty).SortBy(s => s.CompanyName).ToListAsync();
                var customers = await _context.Customers.Find(FilterDefinition<Customer>.Empty).SortBy(c => c.FullName).ToListAsync();
                ViewBag.Products = products.Select(p => new SelectListItem($"{p.Name} (Stok: {p.Quantity})", p.Id)).ToList();
                ViewBag.Suppliers = suppliers.Select(s => new SelectListItem(s.CompanyName, s.Id)).ToList();
                ViewBag.Customers = customers.Select(c => new SelectListItem(c.FullName, c.Id)).ToList();
                return View(movement);
            }
            movement.Date = DateTime.UtcNow;
            await _context.StockMovements.InsertOneAsync(movement);

            var productFilter = Builders<Product>.Filter.Eq(p => p.Id, movement.ProductId);

            if (movement.Type == StockMovementType.StockIn)
            {
                var increaseQty = Builders<Product>.Update
                    .Inc(p => p.Quantity, movement.Quantity);
                await _context.Products.UpdateOneAsync(productFilter, increaseQty);

                if (!string.IsNullOrEmpty(movement.SupplierId))
                {
                    var product = await _context.Products
                        .Find(productFilter)
                        .FirstOrDefaultAsync();

                    if (product != null)
                    {
                        var totalCost = product.PurchasePrice * movement.Quantity;
                        var supplierFilter = Builders<Supplier>.Filter.Eq(s => s.Id, movement.SupplierId);
                        var increaseBalance = Builders<Supplier>.Update
                            .Inc(s => s.Balance, totalCost);
                        await _context.Suppliers.UpdateOneAsync(supplierFilter, increaseBalance);
                    }
                }
            }
            else if (movement.Type == StockMovementType.StockOut)
            {
                var decreaseQty = Builders<Product>.Update
                    .Inc(p => p.Quantity, -movement.Quantity);
                await _context.Products.UpdateOneAsync(productFilter, decreaseQty);
            }
            else if (movement.Type == StockMovementType.Adjustment)
            {
                var setQty = Builders<Product>.Update
                    .Set(p => p.Quantity, movement.Quantity);
                await _context.Products.UpdateOneAsync(productFilter, setQty);
            }
            var checkProduct = await _context.Products.Find(p => p.Id == movement.ProductId).FirstOrDefaultAsync();
            if (checkProduct != null && checkProduct.Quantity <= checkProduct.LowStockThreshold)
            {
                var notification = new Notification
                {
                    Message = $"{checkProduct.Name} (Barkod: {checkProduct.Barcode}) kritik stok limitinin altına düştü! Kalan: {checkProduct.Quantity}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    ProductId = checkProduct.Id
                };
                await _context.Notifications.InsertOneAsync(notification);
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

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
            await _auditLogService.LogActionAsync(userEmail, User.Identity?.Name, "Stok Hareketi Oluşturma", $"Yeni stok hareketi oluşturuldu: {movement.Type}, Ürün: {checkProduct?.Name}");

            return RedirectToAction(nameof(Index));
        }
    }
}