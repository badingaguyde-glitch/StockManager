namespace StockManager.Server.Models;

public class StripePaymentIntentRequest
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
