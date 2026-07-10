namespace StockManager.Server.Services;

public interface IAuditLogService
{
    Task LogActionAsync(string userEmail, string username, string action, string details);
}