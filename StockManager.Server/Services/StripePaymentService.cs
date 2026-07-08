using Stripe;
using Stripe.Checkout;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class StripePaymentService
{
    private readonly StripeSettings _settings;
    private readonly PaymentIntentService _paymentIntentService;
    private readonly SessionService _sessionService;

    public StripePaymentService(StripeSettings settings)
    {
        _settings = settings;
        StripeConfiguration.ApiKey = _settings.SecretKey;
        _paymentIntentService = new PaymentIntentService();
        _sessionService = new SessionService();
    }

    public Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string? currency = null, string? description = null, Dictionary<string, string>? metadata = null)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Tutar sıfırdan büyük olmalıdır.");

        var options = new PaymentIntentCreateOptions
        {
            Amount = Convert.ToInt64(Math.Round(amount * 100)),
            Currency = (currency ?? _settings.Currency).ToLowerInvariant(),
            PaymentMethodTypes = new List<string> { "card" },
            Description = description,
            Metadata = metadata
        };

        return _paymentIntentService.CreateAsync(options);
    }

    public Task<Session> CreateCheckoutSessionAsync(string successUrl, string cancelUrl, IEnumerable<SessionLineItemOptions> lineItems)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = lineItems.ToList(),
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        return _sessionService.CreateAsync(options);
    }

    public string GetPublicKey() => _settings.PublicKey;
}
