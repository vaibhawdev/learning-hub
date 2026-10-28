// ============================================================
// CASE 05: Feature Flags / A-B Testing with DI
// Demonstrates runtime selection of implementations based on flags.
// Uses: Keyed services, factory pattern, Singleton flag store.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Cases.FeatureFlags;

// ── Feature flag store (Singleton) ────────────────────────────────────────────
public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
    bool IsEnabledForUser(string featureName, string userId);
}

public class InMemoryFeatureFlagService : IFeatureFlagService
{
    // In production: load from config, Redis, LaunchDarkly, Azure App Config, etc.
    private readonly Dictionary<string, bool> _flags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NewCheckoutFlow"]       = true,
        ["RecommendationEngine"]  = false,
        ["NewPricingAlgorithm"]   = true,
        ["BetaSearchExperience"]  = false,
    };

    // Percentage-based rollout (users in bucket 0-100)
    private readonly Dictionary<string, int> _rolloutPercentages = new()
    {
        ["BetaSearchExperience"] = 20,  // 20% of users
    };

    public bool IsEnabled(string featureName)
        => _flags.TryGetValue(featureName, out var enabled) && enabled;

    public bool IsEnabledForUser(string featureName, string userId)
    {
        if (!_flags.TryGetValue(featureName, out var globalEnabled))
            return false;

        if (!globalEnabled)
        {
            if (!_rolloutPercentages.TryGetValue(featureName, out var pct))
                return false;
            // Stable bucket: same user always gets same result
            var bucket = Math.Abs(userId.GetHashCode()) % 100;
            return bucket < pct;
        }
        return true;
    }
}

// ── Recommendation engine — two implementations ───────────────────────────────
public interface IRecommendationEngine
{
    Task<List<string>> GetRecommendationsAsync(string userId, int count);
}

public class LegacyRecommendationEngine : IRecommendationEngine
{
    public async Task<List<string>> GetRecommendationsAsync(string userId, int count)
    {
        Console.WriteLine("[Legacy] Computing recommendations (rule-based)");
        await Task.Delay(30);
        return new List<string> { "Product A", "Product B", "Product C" }.Take(count).ToList();
    }
}

public class MLRecommendationEngine : IRecommendationEngine
{
    public async Task<List<string>> GetRecommendationsAsync(string userId, int count)
    {
        Console.WriteLine("[ML] Computing recommendations (neural net)");
        await Task.Delay(80);  // slower but smarter
        return new List<string> { "Product X", "Product Y", "Product Z" }.Take(count).ToList();
    }
}

// ── Feature-flag-driven factory (Scoped) ─────────────────────────────────────
// Decides at request time which implementation to return
public class FeatureFlagRecommendationEngineFactory
{
    private readonly IFeatureFlagService     _flags;
    private readonly IKeyedServiceProvider   _keyedProvider;
    private readonly ICurrentUserService     _currentUser;

    public FeatureFlagRecommendationEngineFactory(
        IFeatureFlagService   flags,
        IKeyedServiceProvider keyedProvider,
        ICurrentUserService   currentUser)
    {
        _flags         = flags;
        _keyedProvider = keyedProvider;
        _currentUser   = currentUser;
    }

    public IRecommendationEngine GetEngine()
    {
        var useML = _flags.IsEnabledForUser("RecommendationEngine", _currentUser.UserId);
        var key   = useML ? "ml" : "legacy";
        Console.WriteLine($"[FeatureFlag] Using '{key}' recommendation engine for user {_currentUser.UserId}");
        return _keyedProvider.GetRequiredKeyedService<IRecommendationEngine>(key);
    }
}

// ── Checkout service with two flows ──────────────────────────────────────────
public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(int cartId);
}

public record CheckoutResult(bool Success, string OrderId, string Flow);

public class LegacyCheckoutService : ICheckoutService
{
    public async Task<CheckoutResult> CheckoutAsync(int cartId)
    {
        Console.WriteLine($"[LegacyCheckout] Processing cart {cartId}");
        await Task.Delay(50);
        return new CheckoutResult(true, "ORD-001", "Legacy");
    }
}

public class NewCheckoutService : ICheckoutService
{
    public async Task<CheckoutResult> CheckoutAsync(int cartId)
    {
        Console.WriteLine($"[NewCheckout] Processing cart {cartId} with new flow");
        await Task.Delay(30);  // new flow is faster
        return new CheckoutResult(true, "ORD-001", "NewFlow");
    }
}

// ── Pricing strategy — same pattern, different dimension ─────────────────────
public interface IPricingStrategy
{
    decimal CalculatePrice(decimal basePrice, string userId);
}

public class StandardPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(decimal basePrice, string userId) => basePrice;
}

public class DynamicPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(decimal basePrice, string userId)
    {
        // Dynamic pricing based on demand, time, user history
        var multiplier = 0.95m + (Math.Abs(userId.GetHashCode()) % 10) * 0.01m;
        return Math.Round(basePrice * multiplier, 2);
    }
}

// ── API controller demonstrating runtime dispatch ─────────────────────────────
public class ShopController
{
    private readonly IFeatureFlagService                      _flags;
    private readonly FeatureFlagRecommendationEngineFactory   _engineFactory;
    private readonly IKeyedServiceProvider                    _keyedProvider;
    private readonly ICurrentUserService                      _currentUser;

    public ShopController(
        IFeatureFlagService flags,
        FeatureFlagRecommendationEngineFactory engineFactory,
        IKeyedServiceProvider keyedProvider,
        ICurrentUserService currentUser)
    {
        _flags         = flags;
        _engineFactory = engineFactory;
        _keyedProvider = keyedProvider;
        _currentUser   = currentUser;
    }

    public async Task<List<string>> GetRecommendationsAsync(int count)
    {
        var engine = _engineFactory.GetEngine();
        return await engine.GetRecommendationsAsync(_currentUser.UserId, count);
    }

    public async Task<CheckoutResult> CheckoutAsync(int cartId)
    {
        var useNewFlow = _flags.IsEnabled("NewCheckoutFlow");
        var checkout   = _keyedProvider.GetRequiredKeyedService<ICheckoutService>(
            useNewFlow ? "new" : "legacy");
        return await checkout.CheckoutAsync(cartId);
    }

    public decimal GetPrice(decimal basePrice)
    {
        var useDynamic = _flags.IsEnabled("NewPricingAlgorithm");
        var strategy   = _keyedProvider.GetRequiredKeyedService<IPricingStrategy>(
            useDynamic ? "dynamic" : "standard");
        return strategy.CalculatePrice(basePrice, _currentUser.UserId);
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class FeatureFlagRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Singleton — shared flag store
        services.AddSingleton<IFeatureFlagService, InMemoryFeatureFlagService>();

        // Keyed recommendation engines — Singleton (stateless ML models are expensive to load)
        services.AddKeyedSingleton<IRecommendationEngine, LegacyRecommendationEngine>("legacy");
        services.AddKeyedSingleton<IRecommendationEngine, MLRecommendationEngine>("ml");

        // Keyed checkout services — Scoped (they might interact with DbContext)
        services.AddKeyedScoped<ICheckoutService, LegacyCheckoutService>("legacy");
        services.AddKeyedScoped<ICheckoutService, NewCheckoutService>("new");

        // Keyed pricing strategies — Transient (stateless, cheap)
        services.AddKeyedTransient<IPricingStrategy, StandardPricingStrategy>("standard");
        services.AddKeyedTransient<IPricingStrategy, DynamicPricingStrategy>("dynamic");

        // Factory — Scoped (reads per-request user context)
        services.AddScoped<FeatureFlagRecommendationEngineFactory>();
        services.AddScoped<ICurrentUserService, FakeCurrentUserService>();
        services.AddScoped<ShopController>();

        return services;
    }
}

public interface ICurrentUserService { string UserId { get; } }
public class FakeCurrentUserService : ICurrentUserService { public string UserId => "user_42"; }
