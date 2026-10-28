// ============================================================
// KEYED / NAMED SERVICES (.NET 8+)
// Multiple implementations of same interface resolved by a key string/enum.
// Before .NET 8, use a factory or dictionary pattern instead.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.AdvancedPatterns.KeyedServices;

// ── Use case: Multiple payment gateways ───────────────────────────────────────
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(decimal amount, string currency, string token);
}

public class StripeGateway : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(decimal amount, string currency, string token)
    {
        Console.WriteLine($"[Stripe] Charging {amount} {currency}");
        await Task.Delay(50);
        return new PaymentResult(true, "stripe_txn_" + Guid.NewGuid().ToString("N")[..8]);
    }
}

public class PayPalGateway : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(decimal amount, string currency, string token)
    {
        Console.WriteLine($"[PayPal] Charging {amount} {currency}");
        await Task.Delay(70);
        return new PaymentResult(true, "pp_txn_" + Guid.NewGuid().ToString("N")[..8]);
    }
}

public class BraintreeGateway : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(decimal amount, string currency, string token)
    {
        Console.WriteLine($"[Braintree] Charging {amount} {currency}");
        await Task.Delay(60);
        return new PaymentResult(true, "bt_txn_" + Guid.NewGuid().ToString("N")[..8]);
    }
}

// ── Controller using keyed service (.NET 8+) ──────────────────────────────────
// The [FromKeyedServices] attribute resolves by key at injection time.
public class CheckoutController
{
    private readonly IPaymentGateway _stripe;
    private readonly IPaymentGateway _paypal;

    public CheckoutController(
        [FromKeyedServices("stripe")]   IPaymentGateway stripe,
        [FromKeyedServices("paypal")]   IPaymentGateway paypal)
    {
        _stripe = stripe;
        _paypal = paypal;
    }

    public async Task<PaymentResult> Checkout(string gateway, decimal amount)
    {
        var selected = gateway.ToLower() switch
        {
            "stripe"  => _stripe,
            "paypal"  => _paypal,
            _         => throw new ArgumentException($"Unknown gateway: {gateway}")
        };

        return await selected.ChargeAsync(amount, "USD", "tok_test");
    }
}

// ── Dynamic key resolution using IKeyedServiceProvider ────────────────────────
// When you don't know the key at compile time, use IKeyedServiceProvider.
public class DynamicGatewaySelector
{
    private readonly IKeyedServiceProvider _keyedProvider;

    public DynamicGatewaySelector(IKeyedServiceProvider keyedProvider)
        => _keyedProvider = keyedProvider;

    public async Task<PaymentResult> Process(string gatewayName, decimal amount)
    {
        var gateway = _keyedProvider.GetKeyedService<IPaymentGateway>(gatewayName)
            ?? throw new InvalidOperationException($"No gateway registered for key '{gatewayName}'");

        return await gateway.ChargeAsync(amount, "USD", "tok_dynamic");
    }
}

// ── Pattern pre .NET 8: Factory dictionary ────────────────────────────────────
// For .NET 6/7 you could use a factory or a dictionary of factories.
public interface IPaymentGatewayFactory
{
    IPaymentGateway GetGateway(string name);
}

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly Dictionary<string, IPaymentGateway> _gateways;

    public PaymentGatewayFactory(
        StripeGateway stripe, PayPalGateway paypal, BraintreeGateway braintree)
    {
        _gateways = new(StringComparer.OrdinalIgnoreCase)
        {
            ["stripe"]     = stripe,
            ["paypal"]     = paypal,
            ["braintree"]  = braintree,
        };
    }

    public IPaymentGateway GetGateway(string name)
        => _gateways.TryGetValue(name, out var gw)
            ? gw
            : throw new InvalidOperationException($"Gateway '{name}' not found.");
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class KeyedRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // .NET 8+ keyed registration
        services.AddKeyedSingleton<IPaymentGateway, StripeGateway>("stripe");
        services.AddKeyedSingleton<IPaymentGateway, PayPalGateway>("paypal");
        services.AddKeyedSingleton<IPaymentGateway, BraintreeGateway>("braintree");

        // Factory for controllers that resolve dynamically
        services.AddScoped<DynamicGatewaySelector>();

        // Pre-.NET 8 factory pattern
        services.AddSingleton<StripeGateway>();
        services.AddSingleton<PayPalGateway>();
        services.AddSingleton<BraintreeGateway>();
        services.AddSingleton<IPaymentGatewayFactory, PaymentGatewayFactory>();

        return services;
    }
}

public record PaymentResult(bool Success, string TransactionId);
