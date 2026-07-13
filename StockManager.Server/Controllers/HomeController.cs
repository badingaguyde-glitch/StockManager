using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using Microsoft.AspNetCore.Authorization;

namespace StockManager.Server.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly MongoDBContext _context;

        public HomeController(MongoDBContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Find(FilterDefinition<Product>.Empty)
                .ToListAsync();

            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);
            var todaySales = await _context.Sales
                .Find(s => s.SaleDate >= todayStart && s.SaleDate <= todayEnd)
                .ToListAsync();

            var viewModel = new HomeDashboardViewModel
            {
                CategoryCount = (int)await _context.Categories.CountDocumentsAsync(FilterDefinition<Category>.Empty),
                ProductCount = products.Count,
                CustomerCount = (int)await _context.Customers.CountDocumentsAsync(FilterDefinition<Customer>.Empty),
                SupplierCount = (int)await _context.Suppliers.CountDocumentsAsync(FilterDefinition<Supplier>.Empty),
                SalesCount = (int)await _context.Sales.CountDocumentsAsync(FilterDefinition<Sale>.Empty),
                TotalStockValue = products.Sum(p => p.Quantity * p.PurchasePrice),
                LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold),
                TodaySalesTotal = todaySales.Sum(s => s.TotalAmount)
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> QuickSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            query = query.Trim();
            var products = await _context.Products
                .Find(p => (p.Name != null && p.Name.ToLower().Contains(query.ToLower())) ||
                           (p.Barcode != null && p.Barcode.Contains(query)))
                .Limit(8)
                .ToListAsync();

            var result = products.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                barcode = p.Barcode,
                salePrice = p.SalePrice,
                quantity = p.Quantity,
                lowStock = p.Quantity <= p.LowStockThreshold
            });

            return Json(result);
        }
    }
}
