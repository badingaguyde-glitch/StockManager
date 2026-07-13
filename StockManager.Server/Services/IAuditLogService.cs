using StockManager.Server.Models;

namespace StockManager.Server.Services;

public interface IAuditLogService
{
    Task LogActionAsync(string userEmail, string username, string action, string details);
    Task<AuditLogDashboardViewModel> GetDashboardDataAsync(
        string? searchTerm = null,
        string? actionFilter = null,
        string? userFilter = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 30);
    Task ClearOldLogsAsync(int daysToKeep);
}