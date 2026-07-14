using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin,Personel")]
public class CustomersController : Controller
{
    private readonly MongoDBContext _context;
    private readonly IAuditLogService _auditLogService;

    public CustomersController(MongoDBContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
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
            .Set(c => c.CompanyName, customer.CompanyName)
            .Set(c => c.Phone, customer.Phone)
            .Set(c => c.Email, customer.Email)
            .Set(c => c.Address, customer.Address)
            .Set(c => c.TaxOffice, customer.TaxOffice)
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

    public async Task<IActionResult> Collection()
    {
        // Açık borcu olan (veya bakiyesi 0'dan farklı olan) müşterileri getiriyoruz
        var customers = await _context.Customers
            .Find(c => c.Balance < 0 || c.Balance > 0)
            .SortBy(c => c.FullName)
            .ToListAsync();

        return View(customers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(string customerId, decimal amount)
    {
        if (amount <= 0)
        {
            TempData["error"] = "Tahsilat tutarı 0'dan büyük olmalıdır.";
            return RedirectToAction(nameof(Collection));
        }

        var customer = await _context.Customers.Find(c => c.Id == customerId).FirstOrDefaultAsync();
        if (customer == null)
        {
            TempData["error"] = "Müşteri bulunamadı.";
            return RedirectToAction(nameof(Collection));
        }

        // Bakiye negatifse borç demektir, tahsilat bakiyeyi sıfıra yaklaştırır (+amount)
        var update = Builders<Customer>.Update.Inc(c => c.Balance, customer.Balance < 0 ? amount : -amount);
        await _context.Customers.UpdateOneAsync(c => c.Id == customerId, update);

        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Belirtilmedi";
        var username = User.Identity?.Name ?? "Belirtilmedi";
        await _auditLogService.LogActionAsync(userEmail, username, "Veresiye Tahsilat", $"{customer.FullName} müşterisinden {amount:N2} ₺ tahsilat yapıldı.");

        TempData["success"] = $"{customer.FullName} müşterisinden {amount:N2} ₺ tahsilat başarıyla yapıldı.";
        return RedirectToAction(nameof(Collection));
    }
}
