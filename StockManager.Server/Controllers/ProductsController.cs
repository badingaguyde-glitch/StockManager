using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

public class ProductsController : Controller
{
    private readonly MongoDBContext _context;

    public ProductsController(MongoDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, string? categoryId, string? supplierId, decimal? minPrice, decimal? maxPrice)
    {
        var filterBuilder = Builders<Product>.Filter;
        var filters = new List<FilterDefinition<Product>>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filters.Add(filterBuilder.Or(
                filterBuilder.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(s, "i")),
                filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(s, "i")),
                filterBuilder.Eq(p => p.Barcode, s)
            ));
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            filters.Add(filterBuilder.Eq(p => p.CategoryId, categoryId));
        }

        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filters.Add(filterBuilder.Eq(p => p.SupplierId, supplierId));
        }

        if (minPrice.HasValue)
        {
            filters.Add(filterBuilder.Gte(p => p.SalePrice, minPrice.Value));
        }

        if (maxPrice.HasValue)
        {
            filters.Add(filterBuilder.Lte(p => p.SalePrice, maxPrice.Value));
        }

        var finalFilter = filters.Count == 0 ? FilterDefinition<Product>.Empty : filterBuilder.And(filters);

        var products = await _context.Products
            .Find(finalFilter)
            .SortBy(p => p.Name)
            .ToListAsync();

        ViewBag.ShowLowStockAlert = products.Any(p => p.Quantity <= p.LowStockThreshold);
        ViewBag.LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold);

        // Preserve filter values for the view
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.SupplierId = supplierId;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;

        await PopulateDropdowns();

        return View(products);
    }

    public async Task<IActionResult> LowStock()
    {
        var products = await _context.Products
            .Find(p => p.Quantity <= p.LowStockThreshold)
            .SortBy(p => p.Name)
            .ToListAsync();

        ViewBag.ShowLowStockAlert = products.Any();
        ViewBag.LowStockCount = products.Count;

        return View("Index", products);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new ProductInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductInputModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Barcode))
        {
            var barcodeExists = await _context.Products
                .Find(p => p.Barcode == model.Barcode)
                .AnyAsync();

            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Bu barkod zaten kayıtlı.");
                await PopulateDropdowns();
                return View(model);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.SupplierName))
        {
            var supplier = new Supplier
            {
                CompanyName = model.SupplierName
            };

            await _context.Suppliers.InsertOneAsync(supplier);
            model.SupplierId = supplier.Id;
        }

        var product = new Product
        {
            Barcode = model.Barcode,
            Name = model.Name,
            Description = model.Description,
            PurchasePrice = model.PurchasePrice,
            SalePrice = model.SalePrice,
            Quantity = model.Quantity,
            LowStockThreshold = model.LowStockThreshold,
            CategoryId = model.CategoryId,
            SupplierId = model.SupplierId
        };

        await _context.Products.InsertOneAsync(product);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var product = await _context.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product == null)
        {
            return NotFound();
        }

        await PopulateDropdowns();
        var model = new ProductInputModel
        {
            Id = product.Id,
            Barcode = product.Barcode,
            Name = product.Name,
            Description = product.Description,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            Quantity = product.Quantity,
            LowStockThreshold = product.LowStockThreshold,
            CategoryId = product.CategoryId,
            SupplierId = product.SupplierId
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductInputModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Barcode))
        {
            var barcodeExists = await _context.Products
                .Find(p => p.Barcode == model.Barcode && p.Id != model.Id)
                .AnyAsync();

            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Bu barkod başka bir ürün tarafından kullanılıyor.");
                await PopulateDropdowns();
                return View(model);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.SupplierName))
        {
            var supplier = new Supplier
            {
                CompanyName = model.SupplierName
            };

            await _context.Suppliers.InsertOneAsync(supplier);
            model.SupplierId = supplier.Id;
        }

        var filter = Builders<Product>.Filter.Eq(p => p.Id, model.Id);
        var update = Builders<Product>.Update
            .Set(p => p.Barcode, model.Barcode)
            .Set(p => p.Name, model.Name)
            .Set(p => p.Description, model.Description)
            .Set(p => p.PurchasePrice, model.PurchasePrice)
            .Set(p => p.SalePrice, model.SalePrice)
            .Set(p => p.Quantity, model.Quantity)
            .Set(p => p.LowStockThreshold, model.LowStockThreshold)
            .Set(p => p.CategoryId, model.CategoryId)
            .Set(p => p.SupplierId, model.SupplierId);

        await _context.Products.UpdateOneAsync(filter, update);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await _context.Products.DeleteOneAsync(p => p.Id == id);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns()
    {
        var categories = await _context.Categories
            .Find(FilterDefinition<Category>.Empty)
            .SortBy(c => c.Name)
            .ToListAsync();

        var suppliers = await _context.Suppliers
            .Find(FilterDefinition<Supplier>.Empty)
            .SortBy(s => s.CompanyName)
            .ToListAsync();

        ViewBag.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id)).ToList();
        ViewBag.Suppliers = suppliers.Select(s => new SelectListItem(s.CompanyName, s.Id)).ToList();
    }
}
