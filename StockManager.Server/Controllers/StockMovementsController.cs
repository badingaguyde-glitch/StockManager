using Microsoft.AspNetCore.Mvc;
using StockManager.Server.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockManager.Server.Models;
using MongoDB.Driver;

namespace StockManager.Server.Controllers
{
    public class StockMovementsController : Controller
    {
        private readonly MongoDBContext _context;

        public StockMovementsController(MongoDBContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var movements = await _context.StockMovements
                .Find(FilterDefinition<StockMovement>.Empty)
                .SortByDescending(m => m.Date)
                .ToListAsync();
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
            return RedirectToAction(nameof(Index));
        }
    }
}