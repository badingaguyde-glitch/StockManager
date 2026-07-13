using System.Globalization;

namespace StockManager.Server.Models;

public class AuditLogDashboardViewModel
{
    // Özet İstatistik Kartları (KPIs)
    public long TotalLogsCount { get; set; }
    public long TodayLogsCount { get; set; }
    public long ActiveUsersCount { get; set; }
    public long CriticalActionsCount { get; set; }

    // Grafik Verileri
    public List<AuditLogDailyCount> DailyActivityChart { get; set; } = new();
    public List<AuditLogActionDistribution> ActionTypeDistribution { get; set; } = new();

    // Filtre Alanları
    public string? SearchTerm { get; set; }
    public string? ActionFilter { get; set; }
    public string? UserEmailFilter { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Sayfalama (Pagination)
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public long TotalFilteredCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalFilteredCount / PageSize);

    // Listeleme Verileri
    public List<AuditLog> Logs { get; set; } = new();
    public List<string> DistinctActions { get; set; } = new();
    public List<string> DistinctUsers { get; set; } = new();
}

public class AuditLogDailyCount
{
    public string DateLabel { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AuditLogActionDistribution
{
    public string ActionName { get; set; } = string.Empty;
    public int Count { get; set; }
}
