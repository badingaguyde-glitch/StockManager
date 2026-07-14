using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Muhasebeci")]
public class ReportsController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IAuditLogService _auditLogService;

    public ReportsController(MongoDBContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
        var sales = await _context.Sales.Find(FilterDefinition<Sale>.Empty).ToListAsync();
        var customers = await _context.Customers.Find(FilterDefinition<Customer>.Empty).ToListAsync();

        ViewBag.TotalProducts = products.Count;
        ViewBag.TotalStockValue = products.Sum(p => p.Quantity * p.PurchasePrice);
        ViewBag.PotentialStockValue = products.Sum(p => p.Quantity * p.SalePrice);
        ViewBag.TotalSales = sales.Sum(s => s.TotalAmount);
        ViewBag.LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold);
        ViewBag.TotalCustomerBalance = customers.Sum(c => c.Balance);

        return View();
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DailyTurnover(DateTime? date)
    {
        var selectedDate = date ?? DateTime.Today;
        var start = selectedDate.Date;
        var end = start.AddDays(1).AddTicks(-1);

        var turnover = await _context.Sales
            .Find(s => s.SaleDate >= start && s.SaleDate <= end)
            .ToListAsync();

        var totalTurnover = turnover.Sum(s => s.TotalAmount);
        var saleCount = turnover.Count;

        return Json(new { date = selectedDate.ToString("yyyy-MM-dd"), totalTurnover, saleCount });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SalesTrend(int days = 30)
    {
        var to = DateTime.Today;
        var from = to.AddDays(-days + 1);

        var sales = await _context.Sales
            .Find(s => s.SaleDate >= from.Date && s.SaleDate <= to.Date.AddDays(1).AddTicks(-1))
            .ToListAsync();

        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        var trend = Enumerable.Range(0, days)
            .Select(i =>
            {
                var day = from.Date.AddDays(i);
                var daySales = sales.Where(s => s.SaleDate.Date == day);
                var total = daySales.Sum(s => s.TotalAmount);
                var count = daySales.Count();
                return new
                {
                    date = day.ToString("yyyy-MM-dd"),
                    label = day.ToString("dd MMM", trCulture),
                    total,
                    count
                };
            })
            .ToList();

        return Json(trend);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CategoryDistribution()
    {
        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty).ToListAsync();

        var distribution = products
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                CategoryName = categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "Kategorisiz",
                StockValue = g.Sum(p => p.Quantity * p.PurchasePrice),
                PotentialValue = g.Sum(p => p.Quantity * p.SalePrice),
                ProductCount = g.Count()
            })
            .OrderByDescending(x => x.StockValue)
            .ToList();

        return Json(distribution);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentTypeDistribution()
    {
        var sales = await _context.Sales.Find(FilterDefinition<Sale>.Empty).ToListAsync();

        var distribution = sales
            .GroupBy(s => s.PaymentType)
            .Select(g => new
            {
                PaymentType = g.Key switch
                {
                    PaymentType.Cash => "Nakit",
                    PaymentType.Card => "Kredi / Banka Kartı",
                    PaymentType.Debt => "Veresiye (Cari)",
                    _ => "Diğer"
                },
                TotalAmount = g.Sum(s => s.TotalAmount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        return Json(distribution);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ProfitLoss(DateTime? startDate, DateTime? endDate)
    {
        var fromDate = startDate ?? DateTime.Today.AddDays(-30);
        var toDate = endDate ?? DateTime.Today;
        var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);

        var sales = await _context.Sales
            .Find(s => s.SaleDate >= fromDate.Date && s.SaleDate <= endOfDay)
            .ToListAsync();

        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();
        var productDict = products
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToDictionary(p => p.Id!, p => p);

        decimal revenue = sales.Sum(s => s.TotalAmount);
        decimal cost = 0m;
        foreach (var sale in sales)
        {
            if (sale.Items == null) continue;
            foreach (var item in sale.Items)
            {
                if (item.ProductId != null && productDict.TryGetValue(item.ProductId, out var product))
                {
                    cost += product.PurchasePrice * item.Quantity;
                }
            }
        }

        var netRevenue = Math.Round(revenue / 1.20m, 2);
        var vatAmount = revenue - netRevenue;
        var profitLoss = netRevenue - cost;
        var profitMargin = netRevenue > 0 ? Math.Round((profitLoss / netRevenue) * 100, 2) : 0m;

        return Json(new
        {
            startDate = fromDate.ToString("yyyy-MM-dd"),
            endDate = toDate.ToString("yyyy-MM-dd"),
            revenue,
            netRevenue,
            vatAmount,
            cost,
            profitLoss,
            profitMargin,
            saleCount = sales.Count
        });
    }

    [HttpGet]
    public async Task<IActionResult> CriticalStockList()
    {
        var products = await _context.Products
            .Find(p => p.Quantity <= p.LowStockThreshold)
            .SortBy(p => p.Quantity)
            .Limit(15)
            .ToListAsync();

        var result = products.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            barcode = p.Barcode,
            quantity = p.Quantity,
            threshold = p.LowStockThreshold
        });

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> TopDebtorsList()
    {
        var customers = await _context.Customers
            .Find(c => c.Balance > 0)
            .SortByDescending(c => c.Balance)
            .Limit(10)
            .ToListAsync();

        var result = customers.Select(c => new
        {
            id = c.Id,
            fullName = c.FullName,
            phone = c.Phone,
            balance = c.Balance
        });

        return Json(result);
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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllSalesAndStockData()
    {
        await _context.Sales.DeleteManyAsync(FilterDefinition<Sale>.Empty);
        await _context.StockMovements.DeleteManyAsync(FilterDefinition<StockMovement>.Empty);

        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
        var username = User.Identity?.Name ?? "Belirtilmedi";
        await _auditLogService.LogActionAsync(userEmail, username, "Veri Sıfırlama", "Tüm satış geçmişi (Sales & SaleItems) ve tüm stok hareketleri (StockMovements) kalıcı olarak sıfırlandı.");

        TempData["success"] = "Tüm satış geçmişi ve stok hareketleri başarıyla sıfırlandı.";
        return RedirectToAction(nameof(Index));
    }
}
