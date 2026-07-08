using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers
{
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

            var viewModel = new HomeDashboardViewModel
            {
                CategoryCount = (int)await _context.Categories.CountDocumentsAsync(FilterDefinition<Category>.Empty),
                ProductCount = products.Count,
                CustomerCount = (int)await _context.Customers.CountDocumentsAsync(FilterDefinition<Customer>.Empty),
                SupplierCount = (int)await _context.Suppliers.CountDocumentsAsync(FilterDefinition<Supplier>.Empty),
                SalesCount = (int)await _context.Sales.CountDocumentsAsync(FilterDefinition<Sale>.Empty),
                TotalStockValue = products.Sum(p => p.Quantity * p.SalePrice)
            };

            return View(viewModel);
        }
    }
}
