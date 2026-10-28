// ============================================================
// SCRUTOR — Assembly scanning + decorator support
// dotnet add package Scrutor
// Scrutor does NOT replace the built-in container — it adds to it.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.Comparison.Scrutor;

// ── 1. Assembly Scanning ──────────────────────────────────────────────────────
// Register all classes in an assembly that implement a naming convention
// or implement a specific interface — without listing them one by one.
//
// builder.Services.Scan(scan => scan
//     .FromAssemblyOf<IOrderService>()          // scan the assembly containing IOrderService
//
//     // Register ALL classes ending in "Service" as their matching interface
//     .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Service")))
//         .AsImplementedInterfaces()
//         .WithScopedLifetime()
//
//     // Register ALL IValidator<T> implementations as transient
//     .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>)))
//         .AsImplementedInterfaces()
//         .WithTransientLifetime()
//
//     // Register ALL IRepository<T> implementations as scoped
//     .AddClasses(classes => classes.AssignableTo(typeof(IRepository<>)))
//         .AsImplementedInterfaces()
//         .WithScopedLifetime()
// );

// ── 2. Decorator pattern ─────────────────────────────────────────────────────
// Shown inline for compilation; in real app use Scrutor's .Decorate<T, TDecorator>()

public interface IOrderService
{
    Task<Order> PlaceOrderAsync(int productId, int qty);
}

public class OrderService : IOrderService
{
    public async Task<Order> PlaceOrderAsync(int productId, int qty)
    {
        Console.WriteLine($"[OrderService] Placing order: product={productId}, qty={qty}");
        await Task.Delay(20);
        return new Order(1, productId, qty);
    }
}

// Each decorator takes the inner service and wraps it
public class LoggingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;

    public LoggingOrderServiceDecorator(IOrderService inner) => _inner = inner;

    public async Task<Order> PlaceOrderAsync(int productId, int qty)
    {
        Console.WriteLine($"[LOG] Before PlaceOrderAsync({productId},{qty})");
        var result = await _inner.PlaceOrderAsync(productId, qty);
        Console.WriteLine($"[LOG] After PlaceOrderAsync → Order #{result.Id}");
        return result;
    }
}

public class TimingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;

    public TimingOrderServiceDecorator(IOrderService inner) => _inner = inner;

    public async Task<Order> PlaceOrderAsync(int productId, int qty)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _inner.PlaceOrderAsync(productId, qty);
        sw.Stop();
        Console.WriteLine($"[TIMING] PlaceOrderAsync took {sw.ElapsedMilliseconds}ms");
        return result;
    }
}

public class CachingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly Dictionary<string, Order> _cache = new();

    public CachingOrderServiceDecorator(IOrderService inner) => _inner = inner;

    public async Task<Order> PlaceOrderAsync(int productId, int qty)
    {
        var key = $"{productId}:{qty}";
        if (_cache.TryGetValue(key, out var cached))
        {
            Console.WriteLine("[CACHE HIT] Returning cached order");
            return cached;
        }
        var order = await _inner.PlaceOrderAsync(productId, qty);
        _cache[key] = order;
        return order;
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class ScrutorRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Register the innermost (real) implementation
        services.AddScoped<IOrderService, OrderService>();

        // Scrutor's Decorate wraps in the order called
        // Last Decorate = outermost (called first by callers)
        // services.Decorate<IOrderService, LoggingOrderServiceDecorator>();
        // services.Decorate<IOrderService, TimingOrderServiceDecorator>();
        // services.Decorate<IOrderService, CachingOrderServiceDecorator>();
        // → Call chain: Caching → Timing → Logging → OrderService

        // Manual equivalent without Scrutor (for illustration):
        services.AddScoped<IOrderService>(sp =>
        {
            IOrderService core    = new OrderService();
            IOrderService logging = new LoggingOrderServiceDecorator(core);
            IOrderService timing  = new TimingOrderServiceDecorator(logging);
            IOrderService caching = new CachingOrderServiceDecorator(timing);
            return caching;
        });

        return services;
    }
}

// ── Scrutor Assembly Scanning — marker interfaces ─────────────────────────────
// Mark services with a marker interface to control what gets scanned
public interface IScopedService  { }
public interface ISingletonService { }
public interface ITransientService { }

// Any class implementing IScopedService will be auto-registered as Scoped:
// services.Scan(scan => scan
//     .FromAssemblyOf<Startup>()
//     .AddClasses(c => c.AssignableTo<IScopedService>())
//     .AsImplementedInterfaces()
//     .WithScopedLifetime());

public interface IValidator<T> { }
public interface IRepository<T> { }

public record Order(int Id, int ProductId, int Quantity);
