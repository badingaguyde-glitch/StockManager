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

    public string TotalStockValueFormatted => TotalStockValue.ToString("C2", CultureInfo.GetCultureInfo("tr-TR"));
}
