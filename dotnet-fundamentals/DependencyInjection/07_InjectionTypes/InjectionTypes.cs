// ============================================================
// INJECTION TYPES — Constructor, Property, Method
// What can be injected into what, and the rules that govern each.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.InjectionTypes;

// ════════════════════════════════════════════════════════════
// 1. CONSTRUCTOR INJECTION (preferred — use this 99% of the time)
// ════════════════════════════════════════════════════════════

// Dependencies are declared as constructor parameters.
// The container resolves all parameters automatically.
// If a parameter can't be resolved, the container throws at startup (ValidateOnBuild=true).

public class ConstructorInjectionExample
{
    // All dependencies are required — constructor enforces this
    private readonly IOrderRepository  _orders;
    private readonly IEmailService     _email;
    private readonly ILogger<ConstructorInjectionExample> _logger;

    public ConstructorInjectionExample(
        IOrderRepository  orders,
        IEmailService     email,
        ILogger<ConstructorInjectionExample> logger)
    {
        // Guard clauses ensure the contract
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _email  = email  ?? throw new ArgumentNullException(nameof(email));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Benefits:
    // ✅ Dependencies are visible and enforced at compile time
    // ✅ Class is always in a valid state after construction
    // ✅ Mockable in unit tests by passing fake implementations
    // ✅ Works with all DI containers (built-in, Autofac, etc.)
    // ✅ Static analysis can trace the full dependency graph
}

// ════════════════════════════════════════════════════════════
// 2. PROPERTY INJECTION (use sparingly — optional deps only)
// ════════════════════════════════════════════════════════════

// Dependencies are set via public properties AFTER construction.
// .NET built-in DI does NOT support property injection automatically.
// Must be done manually, or via Autofac/SimpleInjector.

public class PropertyInjectionExample
{
    // Must have a default — the property might not be set
    public ILogger? Logger   { get; set; }
    public ICache?  Cache    { get; set; }

    private readonly IOrderRepository _orders;  // still required — in constructor

    public PropertyInjectionExample(IOrderRepository orders) => _orders = orders;

    public async Task<List<Order>> GetOrdersAsync()
    {
        Logger?.Log("GetOrdersAsync called");  // optional — null-safe access

        if (Cache is not null && Cache.TryGet("orders", out List<Order>? cached))
            return cached!;

        var orders = await _orders.GetAllAsync();
        Cache?.Set("orders", orders, TimeSpan.FromMinutes(5));
        return orders;
    }

    // When to use:
    // ✅ Truly optional cross-cutting concerns (logging, caching)
    // ✅ Legacy code where constructor injection is too disruptive
    // ✅ Circular dependency breaks (last resort — fix the design instead)
    //
    // When NOT to use:
    // ❌ Required dependencies (class can be in invalid state if not set)
    // ❌ New code where constructor injection is feasible
    // ❌ Hides dependencies — harder to understand what the class needs
}

// Manual property injection in .NET built-in DI (via factory):
// services.AddScoped<PropertyInjectionExample>(sp =>
// {
//     var instance = new PropertyInjectionExample(sp.GetRequiredService<IOrderRepository>());
//     instance.Logger = sp.GetService<ILogger>();  // optional
//     instance.Cache  = sp.GetService<ICache>();   // optional
//     return instance;
// });

// ════════════════════════════════════════════════════════════
// 3. METHOD INJECTION (rare — dependency needed in one operation only)
// ════════════════════════════════════════════════════════════

// A dependency is passed as a parameter to a specific method.
// Not injected by the DI container — caller provides it.

public class MethodInjectionExample
{
    // ExportService needs a formatter, but only for this one export operation.
    // The formatter might vary per call (PDF vs CSV vs JSON).
    public async Task ExportAsync(IExportFormatter formatter)
    {
        var data = await GetDataAsync();
        var output = formatter.Format(data);
        Console.WriteLine($"Exported {output.Length} bytes");
    }

    private Task<byte[]> GetDataAsync() => Task.FromResult(new byte[] { 1, 2, 3 });

    // When to use:
    // ✅ The dependency is specific to ONE operation, not the whole class
    // ✅ The caller selects the implementation (e.g., "export as PDF")
    // ✅ Testing a single operation with different formatters
    //
    // When NOT to use:
    // ❌ If the dependency is needed by MULTIPLE methods — use constructor injection
    // ❌ If the same call is made many times — overhead of passing the dep each time
}

// ════════════════════════════════════════════════════════════
// WHAT CAN BE INJECTED INTO WHAT
// ════════════════════════════════════════════════════════════

// ── Rule table ────────────────────────────────────────────────────────────────
//
// Consumer Lifetime │ Can inject these lifetimes
// ─────────────────────────────────────────────────────────────────────────────
// Singleton         │ Singleton ✅ | Scoped ❌ (captive!) | Transient ⚠️ (captured)
// Scoped            │ Singleton ✅ | Scoped ✅              | Transient ✅
// Transient         │ Singleton ✅ | Scoped ✅              | Transient ✅
//
// ⚠️ = technically allowed but the transient becomes effectively singleton

// ── Special injections available in .NET DI ──────────────────────────────────

public class SpecialInjections
{
    // 1. IServiceProvider — injects the current scope's provider (use carefully)
    public SpecialInjections(IServiceProvider sp) { }

    // 2. IServiceScopeFactory — create child scopes (use in Singleton needing Scoped)
    //    public MyService(IServiceScopeFactory scopeFactory) { }

    // 3. ILogger<T> — generic logger (registered by AddLogging())
    //    public MyService(ILogger<MyService> logger) { }

    // 4. IOptions<T> — strongly typed config (registered by Configure<T>())
    //    public MyService(IOptions<MySettings> options) { }
    //    public MyService(IOptionsSnapshot<MySettings> options) { }  // Scoped — refreshes per request
    //    public MyService(IOptionsMonitor<MySettings> options) { }  // Singleton — live updates

    // 5. IConfiguration — raw configuration (also injectable directly)
    //    public MyService(IConfiguration config) { }

    // 6. IEnumerable<T> — inject ALL registrations of T
    //    public MyService(IEnumerable<IValidator> validators) { }

    // 7. IKeyedServiceProvider (.NET 8+) — resolve by key at runtime
    //    public MyService(IKeyedServiceProvider keyedSp) { }

    // 8. Func<T> — factory pattern without IServiceScopeFactory
    //    public MyService(Func<IMyService> factory) { }
    //    services.AddTransient<Func<IMyService>>(sp => () => sp.GetRequiredService<IMyService>());
}

// ── IOptions<T> vs IOptionsSnapshot<T> vs IOptionsMonitor<T> ─────────────────
//
// IOptions<T>          — Singleton. Value set once at startup. Cannot refresh.
// IOptionsSnapshot<T>  — Scoped.    Refreshes once per request from config source.
// IOptionsMonitor<T>   — Singleton. Live updates via change notification.
//                        Use OnChange(callback) to react to config changes.
//
// Which to inject into which:
// Singleton service → IOptions<T> or IOptionsMonitor<T>   (both Singleton-compatible)
// Singleton service → IOptionsSnapshot<T>                 ❌ CAPTIVE DEPENDENCY!
// Scoped service    → any of the three is fine

// ── Injection into framework types ──────────────────────────────────────────
//
// ASP.NET Core types that support constructor injection automatically:
//   Controllers                    — services.AddControllers() registers them
//   Razor Pages (PageModel)        — services.AddRazorPages()
//   Minimal API handlers           — services.AddEndpointsApiExplorer()
//   Middleware (UseMiddleware<T>()) — first ctor param is RequestDelegate, rest are deps
//   Filters (IActionFilter, etc.)  — registered via [ServiceFilter(typeof(...))]
//   IHostedService                 — services.AddHostedService<T>()
//   IHealthCheck                   — services.AddHealthChecks().AddCheck<T>()
//
// These do NOT support constructor injection:
//   Entity Framework entity classes  — they are plain data holders, not services
//   Data annotations / validators    — static by design
//   Static helper classes            — convert to instance classes to use DI

// Stubs
public interface IOrderRepository { Task<List<Order>> GetAllAsync(); }
public interface IEmailService    { }
public interface ILogger<T>       { void Log(string msg); }
public interface ICache
{
    bool TryGet<T>(string key, out T? value);
    void Set(string key, object value, TimeSpan ttl);
}
public interface IExportFormatter { byte[] Format(byte[] data); }
public record Order(int Id);
