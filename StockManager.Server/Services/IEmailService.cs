using StockManager.Server.Models;

namespace StockManager.Server.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendLowStockAlertAsync(string to, Product product, DateTime date, string? supplierOrCustomerName);
<<<<<<< HEAD
    Task SendPasswordResetCodeAsync(string to, string code, int validityMinutes);
=======
    Task SendPasswordResetCodeAsync(string to, string code);
    Task SendNewTemporaryPasswordAsync(string to, string newPassword);
>>>>>>> origin/EmreControllers
}