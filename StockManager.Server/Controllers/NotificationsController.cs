using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly MongoDBContext _context;

    public NotificationsController(MongoDBContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnread()
    {
        var totalCount = await _context.Notifications
            .CountDocumentsAsync(n => !n.IsRead);

        var unread = await _context.Notifications
            .Find(n => !n.IsRead)
            .SortByDescending(n => n.CreatedAt)
            .Limit(5)
            .ToListAsync();

        return Json(new { count = totalCount, items = unread });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var filter = Builders<Notification>.Filter.Eq(n => n.Id, id);
        var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
        await _context.Notifications.UpdateOneAsync(filter, update);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var filter = Builders<Notification>.Filter.Eq(n => n.IsRead, false);
        var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
        await _context.Notifications.UpdateManyAsync(filter, update);
        return Json(new { success = true });
    }
}