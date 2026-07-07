using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers
{
    public class SalesController : Controller
    {
        private readonly MongoDbContext _context;

        public SalesController(MongoDbContext context)
        {
            _context = context;
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

            await _context.Sales.InsertOneAsync(sale);

            foreach (var item in sale.Items)
            {
                var productFilter = Builders<Product>.Filter.Eq(p => p.Id, item.ProductId);
                var decreaseQty = Builders<Product>.Update.Inc(p => p.Quantity, -item.Quantity);
                await _context.Products.UpdateOneAsync(productFilter, decreaseQty);
            }
            if (sale.PaymentType == PaymentType.Debt && !string.IsNullOrEmpty(sale.CustomerId))
            {
                var customerFilter = Builders<Customer>.Filter.Eq(c => c.Id, sale.CustomerId);
                var increaseBalance = Builders<Customer>.Update.Inc(c => c.Balance, sale.TotalAmount);
                await _context.Customers.UpdateOneAsync(customerFilter, increaseBalance);
            }

            return RedirectToAction(nameof(Invoice), new { id = sale.Id });
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