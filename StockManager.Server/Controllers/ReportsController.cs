using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Muhasebesi")]
public class ReportsController : Controller
{
    private readonly MongoDBContext _context;

    public ReportsController(MongoDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
        var sales = await _context.Sales.Find(FilterDefinition<Sale>.Empty).ToListAsync();

        ViewBag.TotalProducts = products.Count;
        ViewBag.TotalStockValue = products.Sum(p => p.Quantity * p.PurchasePrice);
        ViewBag.TotalSales = sales.Sum(s => s.TotalAmount);
        ViewBag.LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> DailyTurnover(DateTime? date)
    {
        var selectedDate = date ?? DateTime.Today;
        var start = selectedDate.Date;
        var end = start.AddDays(1).AddTicks(-1);

        var turnover = await _context.Sales
            .Find(s => s.SaleDate >= start && s.SaleDate <= end)
            .ToListAsync();

        var totalTurnover = turnover.Sum(s => s.TotalAmount);

        return Json(new { date = selectedDate.ToString("yyyy-MM-dd"), totalTurnover });
    }

    [HttpGet]
    public async Task<IActionResult> BestSellers()
    {
        var sales = await _context.Sales.Find(FilterDefinition<Sale>.Empty).ToListAsync();

        var bestSellers = sales
            .SelectMany(s => s.Items)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.TotalLinePrice)
            })
            .OrderByDescending(x => x.UnitsSold)
            .Take(10)
            .ToList();

        return Json(bestSellers);
    }

    [HttpGet]
    public async Task<IActionResult> SalesTrend(int days = 30)
    {
        var to = DateTime.Today;
        var from = to.AddDays(-days + 1);

        var sales = await _context.Sales
            .Find(s => s.SaleDate >= from.Date && s.SaleDate <= to.Date.AddDays(1).AddTicks(-1))
            .ToListAsync();

        var trend = Enumerable.Range(0, days)
            .Select(i =>
            {
                var day = from.Date.AddDays(i);
                var total = sales.Where(s => s.SaleDate.Date == day).Sum(s => s.TotalAmount);
                return new { date = day.ToString("yyyy-MM-dd"), total };
            })
            .ToList();

        return Json(trend);
    }

    [HttpGet]
    public async Task<IActionResult> CategoryDistribution()
    {
        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty).ToListAsync();

        var distribution = products
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                CategoryName = categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "(Belirtilmemiş)",
                StockValue = g.Sum(p => p.Quantity * p.PurchasePrice)
            })
            .OrderByDescending(x => x.StockValue)
            .ToList();

        return Json(distribution);
    }

    [HttpGet]
    public async Task<IActionResult> ProfitLoss(DateTime? startDate, DateTime? endDate)
    {
        var fromDate = startDate ?? DateTime.Today.AddDays(-30);
        var toDate = endDate ?? DateTime.Today;
        var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);

        var sales = await _context.Sales
            .Find(s => s.SaleDate >= fromDate.Date && s.SaleDate <= endOfDay)
            .ToListAsync();

        decimal revenue = sales.Sum(s => s.TotalAmount);
        decimal cost = 0m;

        foreach (var sale in sales)
        {
            foreach (var item in sale.Items)
            {
                var product = await _context.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product != null)
                {
                    cost += product.PurchasePrice * item.Quantity;
                }
            }
        }

        var profitLoss = revenue - cost;

        return Json(new { startDate = fromDate.ToString("yyyy-MM-dd"), endDate = toDate.ToString("yyyy-MM-dd"), revenue, cost, profitLoss });
    }

    [HttpGet]
    public async Task<IActionResult> RemainingStock()
    {
        var products = await _context.Products
            .Find(FilterDefinition<Product>.Empty)
            .SortBy(p => p.Name)
            .ToListAsync();

        var totalValue = products.Sum(p => p.Quantity * p.PurchasePrice);
        ViewBag.TotalValue = totalValue;

        return View(products);
    }
}
