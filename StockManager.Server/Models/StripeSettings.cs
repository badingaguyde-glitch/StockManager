namespace StockManager.Server.Models;

public class StripeSettings
{
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Currency { get; set; } = "try";
}
