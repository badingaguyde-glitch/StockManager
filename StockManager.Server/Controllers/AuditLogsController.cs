using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? actionFilter,
        string? userFilter,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1)
    {
        var model = await _auditLogService.GetDashboardDataAsync(
            searchTerm, actionFilter, userFilter, startDate, endDate, page, 30);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOldLogs(int daysToKeep = 90)
    {
        await _auditLogService.ClearOldLogsAsync(daysToKeep);
        TempData["SuccessMessage"] = $"{daysToKeep} günden eski denetim kayıtları başarıyla temizlendi.";
        return RedirectToAction(nameof(Index));
    }
}
