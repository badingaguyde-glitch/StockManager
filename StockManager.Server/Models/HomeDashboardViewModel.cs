using System.Globalization;

namespace StockManager.Server.Models;

public class HomeDashboardViewModel
{
    public int CategoryCount { get; set; }
    public int ProductCount { get; set; }
    public int CustomerCount { get; set; }
    public int SupplierCount { get; set; }
    public int SalesCount { get; set; }
    public decimal TotalStockValue { get; set; }

    public int LowStockCount { get; set; }
    public decimal TodaySalesTotal { get; set; }

    // 🆕 Parti, SKT ve Satın Alma (PO) özet bilgileri
    public int ExpiringBatchCount { get; set; }
    public int ExpiredBatchCount { get; set; }
    public int PendingPOCount { get; set; }

    public string TotalStockValueFormatted => TotalStockValue.ToString("C2", CultureInfo.GetCultureInfo("tr-TR"));
    public string TodaySalesTotalFormatted => TodaySalesTotal.ToString("C2", CultureInfo.GetCultureInfo("tr-TR"));
}
