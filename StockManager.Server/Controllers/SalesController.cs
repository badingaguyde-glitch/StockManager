using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StripeCheckout = Stripe.Checkout;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers
{
    [Authorize(Roles = "Admin,Personel")]
    public class SalesController : Controller
    {
        private readonly MongoDBContext _context;
        private readonly StockManager.Server.Services.IEmailService _emailService;
        private readonly StripePaymentService _stripePaymentService;
        private readonly ReceiptPdfService _receiptPdfService;

        public SalesController(
            MongoDBContext context,
            StripePaymentService stripePaymentService,
            ReceiptPdfService receiptPdfService, StockManager.Server.Services.IEmailService emailService)
        {
            _context = context;
            _stripePaymentService = stripePaymentService;
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

            sale.SaleDate = DateTime.UtcNow;
            var count = await _context.Sales.CountDocumentsAsync(FilterDefinition<Sale>.Empty);
            sale.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D3}";
            sale.TotalAmount = sale.Items.Sum(i => i.Quantity * i.UnitPrice);
            sale.Currency = sale.Currency ?? "TRY";

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

            await _context.Sales.InsertOneAsync(sale);

            foreach (var item in sale.Items)
            {
                var productFilter = Builders<Product>.Filter.Eq(p => p.Id, item.ProductId);
                var decreaseQty = Builders<Product>.Update.Inc(p => p.Quantity, -item.Quantity);
                await _context.Products.UpdateOneAsync(productFilter, decreaseQty);

                var product = await _context.Products.Find(productFilter).FirstOrDefaultAsync();
                if (product != null && product.Quantity <= product.LowStockThreshold)
                {
                    var notification = new Notification
                    {
                        Message = $"{product.Name} (Barkod: {product.Barcode}) kritik stok limitinin altına düştü! Kalan: {product.Quantity}",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        ProductId = product.Id
                    };
                    await _context.Notifications.InsertOneAsync(notification);
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
                        await _emailService.SendLowStockAlertAsync(email, product, DateTime.UtcNow, customerDetails);
                    }
                }
            }

            if (sale.PaymentType == PaymentType.Debt && !string.IsNullOrEmpty(sale.CustomerId))
            {
                var customerFilter = Builders<Customer>.Filter.Eq(c => c.Id, sale.CustomerId);
                var increaseBalance = Builders<Customer>.Update.Inc(c => c.Balance, sale.TotalAmount);
                await _context.Customers.UpdateOneAsync(customerFilter, increaseBalance);
            }

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

            return View(sale);
        }
    }
}