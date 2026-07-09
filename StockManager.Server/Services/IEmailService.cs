using StockManager.Server.Models;

namespace StockManager.Server.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendLowStockAlertAsync(string to, Product product, DateTime date, string? supplierOrCustomerName);
}