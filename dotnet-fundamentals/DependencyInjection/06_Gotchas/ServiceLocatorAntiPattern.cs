// ============================================================
// SERVICE LOCATOR ANTI-PATTERN
// Why it's harmful and how to refactor away from it.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Gotchas.ServiceLocator;

// ── What is the Service Locator pattern? ──────────────────────────────────────
// Business logic calls the DI container DIRECTLY to resolve its dependencies.
// The class hides its dependencies instead of declaring them.

// ── ANTI-PATTERN: Service Locator in business logic ──────────────────────────
public class OrderServiceWithServiceLocator  // DON'T DO THIS
{
    private readonly IServiceProvider _sp;

    // The constructor only takes IServiceProvider — a red flag
    public OrderServiceWithServiceLocator(IServiceProvider sp) => _sp = sp;

    public async Task PlaceOrderAsync(int productId, int qty)
    {
        // Dependencies are pulled from the container at call time, hidden from callers
        var repo    = _sp.GetRequiredService<IOrderRepository>();
        var emailer = _sp.GetRequiredService<IEmailService>();
        var logger  = _sp.GetRequiredService<ILogger>();

        // Business logic...
        logger.Log("Placing order");
        await repo.SaveAsync(new Order(productId, qty));
        await emailer.SendAsync("confirm@example.com", "Order placed");
    }

    // Problems:
    // 1. You cannot tell what this class needs by reading its constructor.
    // 2. Unit tests must configure a full IServiceProvider, not just mock specific deps.
    // 3. Missing registrations surface at runtime, not compile time.
    // 4. Every dep can be swapped at any call site — you can't reason about it statically.
}

// ── CORRECT: Constructor injection ────────────────────────────────────────────
public class OrderService  // DO THIS
{
    private readonly IOrderRepository _repo;
    private readonly IEmailService    _emailer;
    private readonly ILogger          _logger;

    // ALL dependencies declared upfront — the class is honest about what it needs
    public OrderService(IOrderRepository repo, IEmailService emailer, ILogger logger)
    {
        _repo    = repo;
        _emailer = emailer;
        _logger  = logger;
    }

    public async Task PlaceOrderAsync(int productId, int qty)
    {
        _logger.Log("Placing order");
        await _repo.SaveAsync(new Order(productId, qty));
        await _emailer.SendAsync("confirm@example.com", "Order placed");
    }

    // Benefits:
    // 1. Constructor signature IS the dependency contract — visible and enforced.
    // 2. Unit test: pass mocks to constructor. No container needed.
    // 3. Missing registration fails at startup (ValidateOnBuild=true), not at runtime.
    // 4. Static analysis tools can trace the dependency graph.
}

// ── LEGITIMATE uses of IServiceProvider ──────────────────────────────────────
// These are NOT anti-patterns:

// 1. In the COMPOSITION ROOT (Program.cs / Startup.cs) — that IS its purpose
//    services.AddScoped<IFoo>(sp => new FooImpl(sp.GetRequiredService<IBar>()));

// 2. In generic factories where you don't know the type at compile time
public class GenericHandlerFactory
{
    private readonly IServiceProvider _sp;
    public GenericHandlerFactory(IServiceProvider sp) => _sp = sp;

    // Type is not known until call time — factory is a valid reason to hold IServiceProvider
    public IHandler<T> Create<T>() => _sp.GetRequiredService<IHandler<T>>();
}

// 3. In IServiceScopeFactory for background services (covered in CaptiveDependency.cs)

// 4. In libraries writing framework-level code (middleware, filters, etc.)
public class FeatureFlagMiddleware
{
    private readonly IServiceProvider _sp;

    public FeatureFlagMiddleware(IServiceProvider sp) => _sp = sp;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Middleware can legitimately resolve services from the request scope
        using var scope   = _sp.CreateScope();
        var flagService   = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        if (!flagService.IsEnabled("maintenance"))
            await ctx.InvokeNextAsync();
    }
}

// ── How to detect Service Locator usage ──────────────────────────────────────
// Grep your codebase for:
//   _sp.GetService<
//   _sp.GetRequiredService<
//   serviceProvider.GetService
//   serviceProvider.GetRequiredService
// Any hits outside of Program.cs, middleware, or factory classes are suspects.

// ── Refactoring from Service Locator to Constructor Injection ─────────────────
// BEFORE (service locator):
//   private readonly IServiceProvider _sp;
//   var dep1 = _sp.GetRequiredService<IDep1>();
//   var dep2 = _sp.GetRequiredService<IDep2>();
//
// AFTER (constructor injection):
//   private readonly IDep1 _dep1;
//   private readonly IDep2 _dep2;
//   ctor(IDep1 dep1, IDep2 dep2) { _dep1 = dep1; _dep2 = dep2; }
//
// Step by step:
// 1. Find every GetRequiredService<T>() call in the class
// 2. Add those types as constructor parameters
// 3. Store in readonly fields
// 4. Remove IServiceProvider from the constructor (if no longer needed)
// 5. Update the DI registration (usually no change needed — container handles it)

// Stubs
public interface IOrderRepository { Task SaveAsync(Order o); }
public interface IEmailService    { Task SendAsync(string to, string msg); }
public interface ILogger          { void Log(string msg); }
public interface IHandler<T>      { }
public interface IFeatureFlagService { bool IsEnabled(string name); }
public record Order(int ProductId, int Qty);
public class HttpContext { public Task InvokeNextAsync() => Task.CompletedTask; }
