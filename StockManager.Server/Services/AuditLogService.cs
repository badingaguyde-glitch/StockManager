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

    public async Task<AuditLogDashboardViewModel> GetDashboardDataAsync(
        string? searchTerm = null,
        string? actionFilter = null,
        string? userFilter = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 30)
    {
        var builder = Builders<AuditLog>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(searchTerm, "i");
            var searchFilter = builder.Regex(x => x.Details, regex)
                             | builder.Regex(x => x.Action, regex)
                             | builder.Regex(x => x.UserEmail, regex)
                             | builder.Regex(x => x.Username, regex);
            filter &= searchFilter;
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            filter &= builder.Eq(x => x.Action, actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            filter &= builder.Eq(x => x.UserEmail, userFilter);
        }

        if (startDate.HasValue)
        {
            filter &= builder.Gte(x => x.Timestamp, startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            filter &= builder.Lte(x => x.Timestamp, endOfDay);
        }

        var totalLogsCount = await _context.AuditLogs.CountDocumentsAsync(builder.Empty);
        var totalFilteredCount = await _context.AuditLogs.CountDocumentsAsync(filter);

        var todayStart = DateTime.UtcNow.Date;
        var todayLogsCount = await _context.AuditLogs.CountDocumentsAsync(
            builder.Gte(x => x.Timestamp, todayStart));

        var criticalRegex = new MongoDB.Bson.BsonRegularExpression("Silme|Hata|Kritik|İptal", "i");
        var criticalActionsCount = await _context.AuditLogs.CountDocumentsAsync(
            builder.Regex(x => x.Action, criticalRegex));

        var distinctActions = (await _context.AuditLogs.Distinct(x => x.Action, builder.Empty).ToListAsync())
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .OrderBy(x => x)
            .ToList();

        var distinctUsers = (await _context.AuditLogs.Distinct(x => x.UserEmail, builder.Empty).ToListAsync())
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .OrderBy(x => x)
            .ToList();

        var activeUsersCount = distinctUsers.Count;

        var logs = await _context.AuditLogs
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Limit(pageSize)
            .ToListAsync();

        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        var recentLogs = await _context.AuditLogs
            .Find(builder.Gte(x => x.Timestamp, sevenDaysAgo))
            .ToListAsync();

        var dailyChart = new List<AuditLogDailyCount>();
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        for (int i = 6; i >= 0; i--)
        {
            var day = DateTime.UtcNow.Date.AddDays(-i);
            var count = recentLogs.Count(x => x.Timestamp.Date == day);
            dailyChart.Add(new AuditLogDailyCount
            {
                DateLabel = day.ToString("dd MMM", trCulture),
                Count = count
            });
        }

        var actionDistribution = recentLogs
            .Where(x => !string.IsNullOrEmpty(x.Action))
            .GroupBy(x => x.Action!)
            .Select(g => new AuditLogActionDistribution
            {
                ActionName = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(7)
            .ToList();

        return new AuditLogDashboardViewModel
        {
            TotalLogsCount = totalLogsCount,
            TodayLogsCount = todayLogsCount,
            ActiveUsersCount = activeUsersCount,
            CriticalActionsCount = criticalActionsCount,
            DailyActivityChart = dailyChart,
            ActionTypeDistribution = actionDistribution,
            SearchTerm = searchTerm,
            ActionFilter = actionFilter,
            UserEmailFilter = userFilter,
            StartDate = startDate,
            EndDate = endDate,
            CurrentPage = page,
            PageSize = pageSize,
            TotalFilteredCount = totalFilteredCount,
            Logs = logs,
            DistinctActions = distinctActions,
            DistinctUsers = distinctUsers
        };
    }

    public async Task ClearOldLogsAsync(int daysToKeep)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        var filter = Builders<AuditLog>.Filter.Lt(x => x.Timestamp, cutoffDate);

        // 1. Silinecek eski logları veritabanından çek
        var logsToBackup = await _context.AuditLogs.Find(filter).ToListAsync();

        if (logsToBackup != null && logsToBackup.Count > 0)
        {
            // 2. Backups dizinini oluştur ve yedekleme yap
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Backups");
            if (!System.IO.Directory.Exists(backupDir))
            {
                System.IO.Directory.CreateDirectory(backupDir);
            }

            var backupPath = System.IO.Path.Combine(backupDir, $"auditlogs_backup_{timestamp}.csv");
            var csv = new System.Text.StringBuilder();
            csv.Append('\uFEFF'); // Excel'de Türkçe/Fransızca karakterlerin bozulmasını önlemek için UTF-8 BOM
            csv.AppendLine("Id;Timestamp;UserEmail;Username;Action;Details");

            foreach (var log in logsToBackup)
            {
                var id = log.Id ?? "";
                var time = log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var email = EscapeCsvField(log.UserEmail);
                var user = EscapeCsvField(log.Username);
                var action = EscapeCsvField(log.Action);
                var details = EscapeCsvField(log.Details);

                csv.AppendLine($"{id};{time};{email};{user};{action};{details}");
            }

            await System.IO.File.WriteAllTextAsync(backupPath, csv.ToString(), System.Text.Encoding.UTF8);
        }

        // 3. Eski logları sil
        await _context.AuditLogs.DeleteManyAsync(filter);
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        var val = field.Replace("\"", "\"\"");
        if (val.Contains(";") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
        {
            return $"\"{val}\"";
        }
        return val;
    }
}