// ============================================================
// LIFETIME COLLISIONS — what goes wrong and how to detect/fix it
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.ServiceLifetimes.Collisions;

// ── SCENARIO 1: Captive Dependency (most common bug) ─────────────────────────
// A Singleton holds a Scoped service.
// The Scoped service becomes effectively Singleton — it is NEVER re-created.
// After the first HTTP request completes, the Scoped object is disposed,
// but the Singleton still holds a reference to the (now disposed) object.

// BROKEN setup:
public class CaptiveDependencyBroken
{
    // Singleton consuming a Scoped DbContext — THIS WILL THROW ObjectDisposedException
    // after the first request ends.
    //
    // public class OrderReportService  // <- Singleton
    // {
    //     private readonly AppDbContext _context;  // <- Scoped
    //
    //     public OrderReportService(AppDbContext context) { _context = context; }
    //     // After request 1 ends, _context is disposed.
    //     // Request 2 calls GetReport() → DbContext throws ObjectDisposedException
    //     public async Task<List<Order>> GetReport() => await _context.Orders.ToListAsync();
    // }
}

// FIX 1: Change OrderReportService to Scoped too (preferred if it has no shared state)
public class OrderReportService_Fixed_AsScoped
{
    private readonly AppDbContext _context;

    public OrderReportService_Fixed_AsScoped(AppDbContext context) => _context = context;

    public List<string> GetReport() => new() { "report data" };
}

// FIX 2: Inject IServiceScopeFactory if the service MUST stay Singleton
public class OrderReportService_Fixed_WithFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderReportService_Fixed_WithFactory(IServiceScopeFactory factory)
        => _scopeFactory = factory;

    public List<string> GetReport()
    {
        using var scope   = _scopeFactory.CreateScope();
        var context       = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new List<string> { "report data" };
    }
}

// ── SCENARIO 2: Transient captured by Singleton ───────────────────────────────
// The Transient's lifetime becomes the Singleton's lifetime.
// Not always a bug — depends on whether the transient is stateful.

public class StatefulTransient
{
    public int Counter { get; private set; } = 0;
    public void Increment() => Counter++;
}

// BAD: StatefulTransient is registered as Transient, but captured by Singleton.
// It will NEVER be recreated — all requests share the same Counter.
public class CapturedTransientSingleton
{
    private readonly StatefulTransient _transient;
    public CapturedTransientSingleton(StatefulTransient t) => _transient = t;

    // All requests increment the same Counter instance!
    public void Process() => _transient.Increment();
}

// ── SCENARIO 3: Scoped service resolved from root provider ────────────────────
// Calling GetRequiredService<IScopedService>() on the ROOT IServiceProvider
// (not a scope) creates a "root scope" instance that NEVER gets disposed.
// .NET 8+ validates this and throws in development.

public class RootProviderMisuse
{
    public static void Demo(IServiceProvider rootProvider)
    {
        // BAD: resolves Scoped from root — lives forever, never disposed
        // var scoped = rootProvider.GetRequiredService<IScopedService>();

        // GOOD: always create a scope first
        using var scope = rootProvider.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<IScopedService>();
    }
}

// ── SCENARIO 4: Multiple Scoped services not sharing state ────────────────────
// If two services both take AppDbContext but resolve in the same scope,
// they GET THE SAME DbContext instance — this is correct and desired.

public class Service_A
{
    private readonly AppDbContext _ctx;
    public Service_A(AppDbContext ctx) => _ctx = ctx;
    public void DoWorkA() => Console.WriteLine($"A using DbContext {_ctx.GetHashCode()}");
}

public class Service_B
{
    private readonly AppDbContext _ctx;
    public Service_B(AppDbContext ctx) => _ctx = ctx;
    public void DoWorkB() => Console.WriteLine($"B using DbContext {_ctx.GetHashCode()}");
}

// In the same request: Service_A._ctx and Service_B._ctx are THE SAME OBJECT.
// Good: they share the same transaction / change tracker.

// ── .NET 8 Scope Validation ──────────────────────────────────────────────────
// In Development, .NET validates the scope automatically:
//
// builder.Host.UseDefaultServiceProvider(options =>
// {
//     options.ValidateScopes  = true;  // detect captive dependencies
//     options.ValidateOnBuild = true;  // catch missing registrations at startup
// });
//
// With ValidateScopes = true, resolving a Scoped service from a Singleton
// throws immediately — you never hit prod with this bug.

public static class CollisionRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // WRONG: creates captive dependency
        // services.AddSingleton<OrderReportService_Fixed_AsScoped>();  // DON'T if it takes Scoped deps

        // CORRECT options:
        services.AddScoped<OrderReportService_Fixed_AsScoped>();
        services.AddSingleton<OrderReportService_Fixed_WithFactory>();

        return services;
    }
}

// Stubs
public class AppDbContext
{
    public IQueryable<string> Orders => new List<string>().AsQueryable();
}

public interface IScopedService { }
