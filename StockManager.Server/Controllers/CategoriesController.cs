using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class CategoriesController : Controller
{
    private readonly MongoDBContext _context;

    public CategoriesController(MongoDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Find(FilterDefinition<Category>.Empty)
            .SortBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        await _context.Categories.InsertOneAsync(category);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var category = await _context.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Category category)
    {
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        var filter = Builders<Category>.Filter.Eq(c => c.Id, category.Id);
        var update = Builders<Category>.Update
            .Set(c => c.Name, category.Name)
            .Set(c => c.Description, category.Description);

        await _context.Categories.UpdateOneAsync(filter, update);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await _context.Categories.DeleteOneAsync(c => c.Id == id);
        return RedirectToAction(nameof(Index));
    }
}
