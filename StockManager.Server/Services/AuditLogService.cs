using MongoDB.Driver;
using StockManager.Server.Services;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class AuditLogService : IAuditLogService
{
    private readonly MongoDBContext _context;
    
    public AuditLogService(MongoDBContext context)
    {
        _context = context;
    }

    public async Task LogActionAsync(string userEmail, string username, string action, string details)
    {
        var auditLog = new AuditLog
        {
            UserEmail = userEmail ?? "Sistem",
            Username = username ?? "Sistem",
            Action = action,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        await _context.AuditLogs.InsertOneAsync(auditLog);
    }
}