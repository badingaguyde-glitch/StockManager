using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

public class SuppliersController : Controller
{
    private readonly MongoDBContext _context;

    public SuppliersController(MongoDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _context.Suppliers
            .Find(FilterDefinition<Supplier>.Empty)
            .SortBy(s => s.CompanyName)
            .ToListAsync();

        return View(suppliers);
    }

    public async Task<IActionResult> Details(string id)
    {
        var supplier = await _context.Suppliers.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (supplier == null)
        {
            return NotFound();
        }

        // Related products
        var products = await _context.Products.Find(p => p.SupplierId == id).ToListAsync();

        // Stock movements related to supplier
        var stockMovements = await _context.StockMovements.Find(sm => sm.SupplierId == id).SortByDescending(sm => sm.Date).ToListAsync();

        // Sales that include products from this supplier
        var productIds = products.Select(p => p.Id).Where(x => x != null).ToList();
        var supplierSales = new List<Sale>();
        if (productIds.Count > 0)
        {
            supplierSales = await _context.Sales
                .Find(s => s.Items.Any(i => productIds.Contains(i.ProductId)))
                .SortByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        ViewBag.Products = products;
        ViewBag.StockMovements = stockMovements;
        ViewBag.SupplierSales = supplierSales;

        return View(supplier);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        await _context.Suppliers.InsertOneAsync(supplier);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var supplier = await _context.Suppliers.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (supplier == null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Supplier supplier)
    {
        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        var filter = Builders<Supplier>.Filter.Eq(s => s.Id, supplier.Id);
        var update = Builders<Supplier>.Update
            .Set(s => s.CompanyName, supplier.CompanyName)
            .Set(s => s.ContactName, supplier.ContactName)
            .Set(s => s.Phone, supplier.Phone)
            .Set(s => s.Email, supplier.Email)
            .Set(s => s.Balance, supplier.Balance);

        await _context.Suppliers.UpdateOneAsync(filter, update);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await _context.Suppliers.DeleteOneAsync(s => s.Id == id);
        return RedirectToAction(nameof(Index));
    }
}
