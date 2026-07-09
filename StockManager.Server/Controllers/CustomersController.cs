using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class CustomersController : Controller
{
    private readonly MongoDBContext _context;

    public CustomersController(MongoDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var customers = await _context.Customers
            .Find(FilterDefinition<Customer>.Empty)
            .SortBy(c => c.FullName)
            .ToListAsync();

        return View(customers);
    }

    public async Task<IActionResult> Details(string id)
    {
        var customer = await _context.Customers.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        await _context.Customers.InsertOneAsync(customer);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var customer = await _context.Customers.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Customer customer)
    {
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var filter = Builders<Customer>.Filter.Eq(c => c.Id, customer.Id);
        var update = Builders<Customer>.Update
            .Set(c => c.FullName, customer.FullName)
            .Set(c => c.Phone, customer.Phone)
            .Set(c => c.Email, customer.Email)
            .Set(c => c.Balance, customer.Balance);

        await _context.Customers.UpdateOneAsync(filter, update);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await _context.Customers.DeleteOneAsync(c => c.Id == id);
        return RedirectToAction(nameof(Index));
    }
}
