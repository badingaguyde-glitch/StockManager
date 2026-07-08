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

    public async Task<IActionResult> Index()
    {
        var products = await _context.Products
            .Find(FilterDefinition<Product>.Empty)
            .SortBy(p => p.Name)
            .ToListAsync();

        ViewBag.ShowLowStockAlert = products.Any(p => p.Quantity <= p.LowStockThreshold);
        ViewBag.LowStockCount = products.Count(p => p.Quantity <= p.LowStockThreshold);

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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(product);
        }

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var barcodeExists = await _context.Products
                .Find(p => p.Barcode == product.Barcode)
                .AnyAsync();

            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(Product.Barcode), "Bu barkod zaten kayıtlı.");
                await PopulateDropdowns();
                return View(product);
            }
        }

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
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product product)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(product);
        }

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var barcodeExists = await _context.Products
                .Find(p => p.Barcode == product.Barcode && p.Id != product.Id)
                .AnyAsync();

            if (barcodeExists)
            {
                ModelState.AddModelError(nameof(Product.Barcode), "Bu barkod başka bir ürün tarafından kullanılıyor.");
                await PopulateDropdowns();
                return View(product);
            }
        }

        var filter = Builders<Product>.Filter.Eq(p => p.Id, product.Id);
        var update = Builders<Product>.Update
            .Set(p => p.Barcode, product.Barcode)
            .Set(p => p.Name, product.Name)
            .Set(p => p.Description, product.Description)
            .Set(p => p.PurchasePrice, product.PurchasePrice)
            .Set(p => p.SalePrice, product.SalePrice)
            .Set(p => p.Quantity, product.Quantity)
            .Set(p => p.LowStockThreshold, product.LowStockThreshold)
            .Set(p => p.CategoryId, product.CategoryId)
            .Set(p => p.SupplierId, product.SupplierId);

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
