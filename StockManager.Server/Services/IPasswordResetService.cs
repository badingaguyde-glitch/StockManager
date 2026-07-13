using StockManager.Server.Models;

namespace StockManager.Server.Services;

public record PasswordResetResult(bool Success, string Message, int RemainingAttempts = 0);

public interface IPasswordResetService
{
    Task<string> GenerateAndStoreCodeAsync(User user);
    Task<PasswordResetResult> VerifyCodeAsync(User user, string code);
    Task ClearResetCodeAsync(User user);
    bool IsCodeExpired(User user);
    bool IsCodeCancelled(User user);
}
