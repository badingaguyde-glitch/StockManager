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

            var now = DateTime.UtcNow;
            var expiredCount = (int)await _context.ProductBatches.CountDocumentsAsync(b => b.ExpiryDate != null && b.Status == ProductBatchStatus.Active && b.ExpiryDate < now);
            var expiringSoonCount = (int)await _context.ProductBatches.CountDocumentsAsync(b => b.ExpiryDate != null && b.Status == ProductBatchStatus.Active && b.ExpiryDate >= now && b.ExpiryDate <= now.AddDays(30));
            var pendingPOCount = (int)await _context.PurchaseOrders.CountDocumentsAsync(po => po.Status != PurchaseOrderStatus.Completed && po.Status != PurchaseOrderStatus.Cancelled);

            var viewModel = new HomeDashboardViewModel
            {
                CategoryCount = (int)await _context.Categories.CountDocumentsAsync(FilterDefinition<Category>.Empty),
                ProductCount = products.Count,
                CustomerCount = (int)await _context.Customers.CountDocumentsAsync(FilterDefinition<Customer>.Empty),
                SupplierCount = (int)await _context.Suppliers.CountDocumentsAsync(FilterDefinition<Supplier>.Empty),
                SalesCount = (int)await _context.Sales.CountDocumentsAsync(FilterDefinition<Sale>.Empty),
                TotalStockValue = products.Sum(p => p.Quantity * p.PurchasePrice),
                LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold),
                TodaySalesTotal = todaySales.Sum(s => s.TotalAmount),
                ExpiredBatchCount = expiredCount,
                ExpiringBatchCount = expiringSoonCount,
                PendingPOCount = pendingPOCount
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

        [HttpGet]
        public async Task<IActionResult> GetKpiDetails(string kpiType)
        {
            if (string.IsNullOrEmpty(kpiType))
                return BadRequest("Geçersiz KPI tipi.");

            switch (kpiType.ToLower())
            {
                case "today-sales":
                    {
                        var todayStart = DateTime.Today;
                        var todayEnd = todayStart.AddDays(1).AddTicks(-1);
                        var sales = await _context.Sales
                            .Find(s => s.SaleDate >= todayStart && s.SaleDate <= todayEnd)
                            .SortByDescending(s => s.SaleDate)
                            .ToListAsync();

                        var customers = await _context.Customers.Find(FilterDefinition<Customer>.Empty).ToListAsync();
                        var customerMap = customers.ToDictionary(c => c.Id, c => c.FullName);

                        var data = sales.Select(s => new KpiTodaySaleDto
                        {
                            InvoiceNumber = s.InvoiceNumber,
                            TotalAmount = s.TotalAmount,
                            PaymentType = s.PaymentType.ToString() == "Stripe" ? "Kredi Kartı (Online)" : s.PaymentType.ToString(),
                            CustomerName = s.CustomerId != null && customerMap.TryGetValue(s.CustomerId, out var name) ? name : "Bireysel Müşteri",
                            Time = s.SaleDate.ToLocalTime().ToString("HH:mm")
                        }).ToList();

                        ViewBag.Title = "Bugünün Satış Fişleri";
                        ViewBag.KpiType = "today-sales";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                case "stock-value":
                    {
                        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty).ToListAsync();
                        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();

                        var data = categories.Select(c => {
                            var catProducts = products.Where(p => p.CategoryId == c.Id).ToList();
                            return new KpiCategoryValueDto
                            {
                                CategoryName = c.Name,
                                Color = c.Color,
                                Icon = c.Icon,
                                ProductCount = catProducts.Count,
                                TotalQuantity = catProducts.Sum(p => p.Quantity),
                                TotalValue = catProducts.Sum(p => p.Quantity * p.PurchasePrice)
                            };
                        }).OrderByDescending(x => x.TotalValue).ToList();

                        ViewBag.Title = "Kategori Bazlı Stok Değerleri (Maliyet)";
                        ViewBag.KpiType = "stock-value";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                case "low-stock":
                    {
                        var products = await _context.Products
                            .Find(p => p.Quantity <= p.LowStockThreshold)
                            .SortBy(p => p.Quantity)
                            .ToListAsync();

                        var data = products.Select(p => new KpiLowStockDto
                        {
                            Name = p.Name,
                            Barcode = p.Barcode,
                            Quantity = p.Quantity,
                            Threshold = p.LowStockThreshold,
                            PurchasePrice = p.PurchasePrice
                        }).ToList();

                        ViewBag.Title = "Kritik Stok Seviyesindeki Ürünler";
                        ViewBag.KpiType = "low-stock";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                case "product-portfolio":
                    {
                        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty).ToListAsync();
                        var products = await _context.Products.Find(FilterDefinition<Product>.Empty).ToListAsync();

                        var data = categories.Select(c => new KpiProductPortfolioDto
                        {
                            CategoryName = c.Name,
                            Color = c.Color,
                            Icon = c.Icon,
                            ProductCount = products.Count(p => p.CategoryId == c.Id),
                            TotalQuantity = products.Where(p => p.CategoryId == c.Id).Sum(p => p.Quantity)
                        }).OrderByDescending(x => x.ProductCount).ToList();

                        ViewBag.Title = "Kategori Bazlı Ürün Çeşitliliği & Miktarı";
                        ViewBag.KpiType = "product-portfolio";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                case "customers":
                    {
                        var customers = await _context.Customers
                            .Find(FilterDefinition<Customer>.Empty)
                            .SortByDescending(c => c.Balance)
                            .Limit(10)
                            .ToListAsync();

                        var data = customers.Select(c => new KpiCustomerDto
                        {
                            FullName = c.FullName,
                            Phone = c.Phone ?? "-",
                            Email = c.Email ?? "-",
                            Balance = c.Balance
                        }).ToList();

                        ViewBag.Title = "En Yüksek Bakiyeli Cari Müşteriler";
                        ViewBag.KpiType = "customers";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                case "total-sales":
                    {
                        var sales = await _context.Sales
                            .Find(FilterDefinition<Sale>.Empty)
                            .SortByDescending(s => s.SaleDate)
                            .Limit(10)
                            .ToListAsync();

                        var customers = await _context.Customers.Find(FilterDefinition<Customer>.Empty).ToListAsync();
                        var customerMap = customers.ToDictionary(c => c.Id, c => c.FullName);

                        var data = sales.Select(s => new KpiTotalSaleDto
                        {
                            InvoiceNumber = s.InvoiceNumber,
                            TotalAmount = s.TotalAmount,
                            PaymentType = s.PaymentType.ToString() == "Stripe" ? "Kredi Kartı (Online)" : s.PaymentType.ToString(),
                            CustomerName = s.CustomerId != null && customerMap.TryGetValue(s.CustomerId, out var name) ? name : "Bireysel Müşteri",
                            Date = s.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                        }).ToList();

                        ViewBag.Title = "Son Yapılan 10 Satış İşlemi";
                        ViewBag.KpiType = "total-sales";
                        return PartialView("_KpiDetailsPartial", data);
                    }

                default:
                    return BadRequest("Bilinmeyen KPI tipi.");
            }
        }
    }

    public class KpiTodaySaleDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class KpiCategoryValueDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class KpiLowStockDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public int Quantity { get; set; }
        public int Threshold { get; set; }
        public decimal PurchasePrice { get; set; }
    }

    public class KpiProductPortfolioDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalQuantity { get; set; }
    }

    public class KpiCustomerDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class KpiTotalSaleDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }
}
