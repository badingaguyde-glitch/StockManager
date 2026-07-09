using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

public class ProductsController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IImageUploadService _imageUploadService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        MongoDBContext context,
        IImageUploadService imageUploadService,
        ILogger<ProductsController> logger)
    {
        _context = context;
        _imageUploadService = imageUploadService;
        _logger = logger;
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
    public async Task<IActionResult> Create(ProductInputModel model, IFormFile? imageFile)
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

        // 🆕 Görsel yükleme
        string? imageUrl = null;
        string? cloudinaryPublicId = null;

        if (imageFile != null && imageFile.Length > 0)
        {
            var uploadResult = await _imageUploadService.UploadImageAsync(imageFile, "stock-manager/products");

            if (!uploadResult.Success)
            {
                ModelState.AddModelError("imageFile", uploadResult.ErrorMessage ?? "Görsel yükleme başarısız oldu");
                await PopulateDropdowns();
                return View(model);
            }

            imageUrl = uploadResult.ImageUrl;
            cloudinaryPublicId = uploadResult.PublicId;
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
            SupplierId = model.SupplierId,
            // 🆕 Görsel alanları
            ImageUrl = imageUrl,
            CloudinaryPublicId = cloudinaryPublicId,
            ImageUploadedAt = DateTime.UtcNow
        };

        await _context.Products.InsertOneAsync(product);
        
        TempData["success"] = "Ürün başarıyla eklendi.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var product = await _context.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound();

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
            SupplierId = product.SupplierId,
            // 🆕 Görsel bilgisi
            ExistingImageUrl = product.ImageUrl
        };

        await PopulateDropdowns();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProductInputModel model, IFormFile? imageFile)
    {
        if (id != model.Id)
            return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        var product = await _context.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound();

        // 🆕 Yeni görsel yükleme
        if (imageFile != null && imageFile.Length > 0)
        {
            // Eski görseli sil
            if (!string.IsNullOrWhiteSpace(product.CloudinaryPublicId))
            {
                await _imageUploadService.DeleteImageAsync(product.CloudinaryPublicId);
            }

            // Yeni görseli yükle
            var uploadResult = await _imageUploadService.UploadImageAsync(imageFile, "stock-manager/products");

            if (!uploadResult.Success)
            {
                ModelState.AddModelError("imageFile", uploadResult.ErrorMessage ?? "Görsel yükleme başarısız oldu");
                await PopulateDropdowns();
                return View(model);
            }

            product.ImageUrl = uploadResult.ImageUrl;
            product.CloudinaryPublicId = uploadResult.PublicId;
            product.ImageUploadedAt = DateTime.UtcNow;
        }

        product.Name = model.Name;
        product.Description = model.Description;
        product.PurchasePrice = model.PurchasePrice;
        product.SalePrice = model.SalePrice;
        product.Quantity = model.Quantity;
        product.LowStockThreshold = model.LowStockThreshold;
        product.CategoryId = model.CategoryId;
        product.SupplierId = model.SupplierId;

        var updateResult = await _context.Products.ReplaceOneAsync(
            p => p.Id == id,
            product);

        if (updateResult.ModifiedCount == 0)
        {
            TempData["error"] = "Ürün güncellenemedi.";
        }
        else
        {
            TempData["success"] = "Ürün başarıyla güncellendi.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var product = await _context.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound();

        // 🆕 Cloudinary'den görseli sil
        if (!string.IsNullOrWhiteSpace(product.CloudinaryPublicId))
        {
            await _imageUploadService.DeleteImageAsync(product.CloudinaryPublicId);
        }

        await _context.Products.DeleteOneAsync(p => p.Id == id);

        TempData["success"] = "Ürün başarıyla silindi.";
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

        ViewBag.Categories = new SelectList(categories, "Id", "Name");
        ViewBag.Suppliers = new SelectList(suppliers, "Id", "CompanyName");
    }
}
