# 05 — Dependency Inversion Principle (DIP)

> **Core idea:** High-level modules should not depend on low-level modules. Both should depend on **abstractions**. Abstractions should not depend on details; details should depend on abstractions.

---

## What is DIP?

The "inversion" in DIP refers to flipping the traditional dependency direction.

**Traditional (bad) flow:**
```
OrderService  →  depends on  →  SqlOrderRepository (concrete)
```

**Inverted (good) flow:**
```
OrderService  →  depends on  →  IOrderRepository (abstraction)
                                       ↑
                              SqlOrderRepository (concrete)
```

Now `OrderService` doesn't know SQL exists. The concretion points *upward* to the abstraction — the dependency is inverted.

---

## The Three Terms — Many Developers Confuse These

| Term | What it IS | Level |
|---|---|---|
| **DIP** — Dependency Inversion *Principle* | The design rule: high-level depends on abstractions | Principle |
| **DI** — Dependency *Injection* | The technique: pass dependencies in (usually constructor) | Pattern |
| **IoC** — Inversion of *Control* | The concept: someone else creates and wires your dependencies | Concept |
| **IoC Container** | The tool that automates IoC: `Microsoft.Extensions.DI`, Autofac, etc. | Framework |

DI implements DIP. An IoC container automates DI. They are not the same thing, and using an IoC container does NOT guarantee you're following DIP if you're injecting concretions.

---

## 5 Real-World Use Cases

1. **OrderService** depends on `IOrderRepository`, not `SqlOrderRepository` — swap the DB without touching business logic.
2. **EmailService** depends on `ISmtpClient` — inject a fake SMTP in tests, SendGrid in production.
3. **PaymentService** depends on `IPaymentGateway` — Stripe, PayPal, or a test double.
4. **AuthService** depends on `ITokenStore` — JWT in memory, Redis in prod, in-memory in tests.
5. **ReportService** depends on `IReportExporter` — CSV, PDF, or a null exporter in tests.

---

## ❌ Anti-Pattern 1: Hardcoded Dependency (Bastard Injection)

```csharp
// ❌ High-level module directly instantiates a low-level module
public class OrderService_Hardcoded
{
    // ❌ Hardcoded dependency — SqlOrderRepository is created INSIDE the service
    private readonly SqlOrderRepository_Fake _repo = new SqlOrderRepository_Fake();
    private readonly SmtpEmailSender_Fake    _email = new SmtpEmailSender_Fake();

    public async Task<Guid> PlaceOrderAsync(string customerId, decimal total)
    {
        // Tests CANNOT run without a real database and real SMTP server.
        // Changing the database requires modifying THIS class.
        // Changing the email provider requires modifying THIS class.
        var orderId = Guid.NewGuid();
        await _repo.SaveAsync(orderId, customerId, total);
        await _email.SendConfirmationAsync(customerId);
        return orderId;
    }
}

// Fake concretions to make the above compile
public class SqlOrderRepository_Fake { public Task SaveAsync(Guid id, string cust, decimal total) => Task.CompletedTask; }
public class SmtpEmailSender_Fake    { public Task SendConfirmationAsync(string cust) => Task.CompletedTask; }
```

---

## ❌ Anti-Pattern 2: Service Locator

Service Locator is sometimes called "DI done wrong." Dependencies are hidden inside the class — they're pulled from a global registry instead of being declared in the constructor.

```csharp
// ❌ Global service registry — a hidden dependency bag
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    public static void Register<T>(T service) => _services[typeof(T)] = service!;
    public static T Get<T>()                  => (T)_services[typeof(T)];
}

public class OrderService_ServiceLocator
{
    public async Task<Guid> PlaceOrderAsync(string customerId, decimal total)
    {
        // ❌ Hidden dependencies — not visible in the constructor signature
        // You cannot tell what this class needs by looking at its API
        var repo  = ServiceLocator.Get<IOrderRepository_Good>();
        var email = ServiceLocator.Get<IEmailService_Good>();

        var orderId = Guid.NewGuid();
        await repo.SaveAsync(orderId, customerId, total);
        await email.SendAsync(new EmailMessage_Good(customerId, "Order Confirmed", "..."));
        return orderId;
    }
}
// Problems:
// 1. Unit testing requires registering the right types in the locator — global state!
// 2. Test isolation is hard — one test's registration leaks into another.
// 3. The class's actual dependencies are invisible from the outside.
// 4. NullReferenceException if someone forgets to register a service.
```

---

## ✅ Pattern: Constructor Injection (DIP + DI)

Dependencies are declared upfront. The class is explicit about what it needs. Testing is trivial — pass fakes in.

```csharp
// Abstractions — owned by the Application layer (high-level)
public interface IOrderRepository_Good
{
    Task SaveAsync(Guid orderId, string customerId, decimal total, CancellationToken ct = default);
    Task<Order_Good?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
}

public interface IEmailService_Good
{
    Task SendAsync(EmailMessage_Good message, CancellationToken ct = default);
}

public record EmailMessage_Good(string To, string Subject, string Body);
public record Order_Good(Guid Id, string CustomerId, decimal Total);

// ✅ High-level module — depends only on abstractions
public sealed class OrderService_Good
{
    private readonly IOrderRepository_Good _repo;
    private readonly IEmailService_Good    _email;

    // Dependencies declared explicitly — visible, testable, replaceable
    public OrderService_Good(IOrderRepository_Good repo, IEmailService_Good email)
    {
        _repo  = repo  ?? throw new ArgumentNullException(nameof(repo));
        _email = email ?? throw new ArgumentNullException(nameof(email));
    }

    public async Task<Guid> PlaceOrderAsync(
        string customerId, decimal total, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("CustomerId required");
        if (total <= 0) throw new ArgumentException("Total must be positive");

        var orderId = Guid.NewGuid();
        await _repo.SaveAsync(orderId, customerId, total, ct);
        await _email.SendAsync(new EmailMessage_Good(customerId, "Order Confirmed",
            $"Your order {orderId} for {total:C} is confirmed."), ct);
        return orderId;
    }
}

// ✅ Low-level details — implement the abstractions, point upward to them
public sealed class SqlOrderRepository : IOrderRepository_Good
{
    private readonly string _connectionString;
    public SqlOrderRepository(string connectionString) => _connectionString = connectionString;

    public Task SaveAsync(Guid id, string cust, decimal total, CancellationToken ct = default)
    {
        Console.WriteLine($"[SQL] INSERT order {id} for {cust}: {total:C}");
        return Task.CompletedTask;
    }

    public Task<Order_Good?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Order_Good?>(new Order_Good(id, "customer", 100m));
}

public sealed class SendGridEmailService : IEmailService_Good
{
    private readonly string _apiKey;
    public SendGridEmailService(string apiKey) => _apiKey = apiKey;

    public Task SendAsync(EmailMessage_Good msg, CancellationToken ct = default)
    {
        Console.WriteLine($"[SendGrid] To:{msg.To} Subject:{msg.Subject}");
        return Task.CompletedTask;
    }
}

// ✅ Test doubles — zero infrastructure needed in tests
public sealed class InMemoryOrderRepository : IOrderRepository_Good
{
    private readonly Dictionary<Guid, Order_Good> _store = new();

    public Task SaveAsync(Guid id, string cust, decimal total, CancellationToken ct = default)
    {
        _store[id] = new Order_Good(id, cust, total);
        return Task.CompletedTask;
    }

    public Task<Order_Good?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null);
}

public sealed class NoopEmailService : IEmailService_Good
{
    public Task SendAsync(EmailMessage_Good msg, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

---

## Intermediate: IoC Container Registration (Microsoft.Extensions.DependencyInjection)

The IoC container automates the wiring. You declare what satisfies what — the container builds the object graphs.

```csharp
// Requires: Microsoft.Extensions.DependencyInjection NuGet package
using Microsoft.Extensions.DependencyInjection;

public static class DependencyRegistration
{
    public static IServiceCollection RegisterOrderServices(
        this IServiceCollection services, string connectionString, string sendGridKey)
    {
        // Abstractions → Concretions
        services.AddScoped<IOrderRepository_Good>(
            _ => new SqlOrderRepository(connectionString));

        services.AddSingleton<IEmailService_Good>(
            _ => new SendGridEmailService(sendGridKey));

        services.AddScoped<OrderService_Good>();

        return services;
    }
}

// In Program.cs / Startup.cs:
// builder.Services.RegisterOrderServices(connectionString, sendGridKey);
// Container handles construction and lifetime automatically.
```

---

## Advanced: Lifetime Management — The Captive Dependency Trap

This is the #1 production gotcha with IoC containers. A **Singleton** capturing a **Scoped** dependency causes the Scoped service to live for the application's entire lifetime — defeating its purpose and causing state bugs.

```
Lifetimes (shortest to longest):
  Transient  → new instance every injection
  Scoped     → one instance per HTTP request (or defined scope)
  Singleton  → one instance for the entire application lifetime
```

```csharp
// ❌ CAPTIVE DEPENDENCY — Singleton captures Scoped
public sealed class ReportCache   // Registered as Singleton
{
    private readonly IOrderRepository_Good _repo;   // Registered as Scoped

    // When ReportCache is constructed (first request), it captures ONE instance
    // of SqlOrderRepository — tied to request #1's database connection/context.
    // All future requests use request #1's context → stale data, connection leaks,
    // EF Core ObjectDisposedException in production.
    public ReportCache(IOrderRepository_Good repo) => _repo = repo;
}
```

```csharp
// ✅ Fix: Use IServiceScopeFactory to create a scope on demand
public sealed class ReportCache_Fixed   // Singleton
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportCache_Fixed(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task<Order_Good?> GetOrderAsync(Guid id)
    {
        // Create a fresh scope for each operation — gets a fresh Scoped dependency
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository_Good>();
        return await repo.GetByIdAsync(id);
    }   // scope disposed here — Scoped dependency is released properly
}
```

---

## Advanced: Lifetime Rules

```
┌────────────────────────────────────────────────────────────────────┐
│  Safe dependency directions (longest → shortest is always safe)    │
│                                                                    │
│  Singleton   can depend on   Singleton                             │
│  Scoped      can depend on   Singleton  or  Scoped                 │
│  Transient   can depend on   Singleton  or  Scoped  or  Transient  │
│                                                                    │
│  ❌ NEVER:                                                         │
│  Singleton   depends on   Scoped   (captive dependency)            │
│  Singleton   depends on   Transient (same problem)                 │
└────────────────────────────────────────────────────────────────────┘
```

**Detection:** ASP.NET Core validates scope on startup by default in Development mode. If you register a Singleton that captures a Scoped service, you'll see:

```
InvalidOperationException: Cannot consume scoped service 'IOrderRepository_Good'
from singleton 'ReportCache'.
```

Enable it in production too:

```csharp
builder.Services.AddScoped<IOrderRepository_Good, SqlOrderRepository>();
// Validation happens at app startup — fails fast instead of silently at runtime
```

---

## Advanced: Func\<T\> Factory — Deferred and Repeated Resolution

When a Singleton needs a Scoped dependency repeatedly (not just at construction), inject a factory function.

```csharp
public sealed class BackgroundProcessor   // Singleton — runs forever
{
    private readonly Func<IOrderRepository_Good> _repoFactory;

    // Inject a factory, not the dependency itself
    public BackgroundProcessor(Func<IOrderRepository_Good> repoFactory)
        => _repoFactory = repoFactory;

    public async Task ProcessBatchAsync(IEnumerable<Guid> orderIds)
    {
        foreach (var id in orderIds)
        {
            // Fresh instance per operation — no captive lifetime issue
            var repo  = _repoFactory();
            var order = await repo.GetByIdAsync(id);
            Console.WriteLine($"Processing {order?.Id}");
        }
    }
}

// Registration:
// services.AddScoped<IOrderRepository_Good, SqlOrderRepository>();
// services.AddSingleton<BackgroundProcessor>(sp =>
//     new BackgroundProcessor(() => sp.CreateScope()
//                                     .ServiceProvider
//                                     .GetRequiredService<IOrderRepository_Good>()));
```

---

## Advanced: DIP vs DI — You Can Have One Without the Other

```csharp
// ✅ DIP without an IoC container — manual wiring (valid for small apps)
var repo    = new SqlOrderRepository("Server=prod;Database=orders");
var email   = new SendGridEmailService("sg_api_key_here");
var service = new OrderService_Good(repo, email);   // Constructor injection, no container

// ❌ DI without DIP — injecting a concretion (defeats the purpose)
public class OrderService_WrongDI
{
    private readonly SqlOrderRepository _repo;   // concretion injected — still coupled to SQL

    public OrderService_WrongDI(SqlOrderRepository repo) => _repo = repo;
    // You can swap this via DI container, but you CAN'T swap the type.
    // DI technique used; DIP principle NOT followed.
}
```

The principle (DIP) matters more than the tool (IoC container). An IoC container is a convenience — it doesn't enforce or guarantee DIP.

---

## More Real-World Examples

### Example: Abstracting Time — `DateTime.UtcNow` Is a Hidden Dependency

Static clock access is the most-missed DIP violation: you can't test "order placed after cutoff" logic without controlling time.

```csharp
// ❌ Untestable — the test's result depends on when you RUN it
public bool QualifiesForSameDayShipping(Order order)
    => DateTime.UtcNow.Hour < 14;   // hidden static dependency on the machine clock

// ✅ .NET 8+: inject the built-in abstract TimeProvider
public sealed class ShippingPolicy
{
    private readonly TimeProvider _clock;
    public ShippingPolicy(TimeProvider clock) => _clock = clock;

    public bool QualifiesForSameDayShipping(Order order)
        => _clock.GetUtcNow().Hour < 14;
}

// Production:  services.AddSingleton(TimeProvider.System);
// Test:        var fake = new FakeTimeProvider(new DateTimeOffset(2026, 7, 10, 13, 59, 0, TimeSpan.Zero));
//              Assert.True(new ShippingPolicy(fake).QualifiesForSameDayShipping(order));
//              (FakeTimeProvider ships in Microsoft.Extensions.TimeProvider.Testing)
// Pre-.NET 8: define your own IClock { DateTimeOffset UtcNow { get; } } — same idea.
```

Same reasoning applies to other statics you don't own: `Random.Shared`, `Environment.GetEnvironmentVariable`, `File.ReadAllText` (`System.IO.Abstractions` gives you `IFileSystem`), `Guid.NewGuid()` when IDs must be deterministic in tests.

### Example: `HttpClient` the Wrong Way and the Right Way

```csharp
// ❌ new HttpClient() per call — socket exhaustion under load, unmockable, hardcoded
public class ExchangeRateService_Bad
{
    public async Task<decimal> GetRateAsync(string from, string to)
    {
        using var http = new HttpClient();   // new socket pool every call!
        var json = await http.GetStringAsync($"https://api.rates.example/{from}/{to}");
        return decimal.Parse(json);
    }
}

// ✅ Typed client via IHttpClientFactory — the framework injects a managed HttpClient
public sealed class ExchangeRateService
{
    private readonly HttpClient _http;
    public ExchangeRateService(HttpClient http) => _http = http;   // factory-provided

    public async Task<decimal> GetRateAsync(string from, string to)
        => decimal.Parse(await _http.GetStringAsync($"rates/{from}/{to}"));
}

// Program.cs — configuration lives at the composition root, not inside the service:
// services.AddHttpClient<ExchangeRateService>(c => c.BaseAddress = new Uri("https://api.rates.example/"));
// Tests: new ExchangeRateService(new HttpClient(fakeHandler)) — intercept at HttpMessageHandler level.
```

### Example: Decorators — DIP's Payoff for Cross-Cutting Concerns

Because callers depend only on `IOrderRepository_Good`, you can layer behavior around it without touching the SQL implementation or any caller.

```csharp
public sealed class CachedOrderRepository : IOrderRepository_Good
{
    private readonly IOrderRepository_Good _inner;
    private readonly Dictionary<Guid, Order_Good> _cache = new();

    public CachedOrderRepository(IOrderRepository_Good inner) => _inner = inner;

    public async Task<Order_Good?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(id, out var hit)) return hit;
        var order = await _inner.GetByIdAsync(id, ct);
        if (order is not null) _cache[id] = order;
        return order;
    }

    public Task SaveAsync(Guid id, string cust, decimal total, CancellationToken ct = default)
    {
        _cache.Remove(id);                       // invalidate on write
        return _inner.SaveAsync(id, cust, total, ct);
    }
}

// Composition root stacks the onion — nobody else knows caching exists:
// services.AddScoped<SqlOrderRepository>();
// services.AddScoped<IOrderRepository_Good>(sp =>
//     new CachedOrderRepository(sp.GetRequiredService<SqlOrderRepository>()));
// (Or use Scrutor: services.Decorate<IOrderRepository_Good, CachedOrderRepository>();)
// Retry, logging, and metrics decorators stack the same way. This composability
// is the concrete payoff of "depend on abstractions."
```

---

## Gotchas

### Gotcha 1: Returning `IQueryable` Means You Never Actually Inverted Anything

The interface *looks* like an abstraction, but `IQueryable<T>` is a promise that "an EF-like LINQ provider translates whatever expression you throw at it" — a promise only EF can keep.

```csharp
// ❌ Abstraction in name only
public interface IOrderRepository { IQueryable<Order> Orders { get; } }

// A caller writes:
var recent = repo.Orders.Where(o => o.CreatedAt > cutoff)
                        .Include(o => o.Lines)        // EF-only extension!
                        .AsNoTracking()               // EF-only!
                        .ToList();
// Your in-memory test double can't honor Include/AsNoTracking; a Dapper swap can't
// honor ANY of it. Every caller is still programming against EF — through a keyhole.
```

**Fix:** the interface exposes *intent* (`GetRecentWithLinesAsync(cutoff)`) and materialized results (`IReadOnlyList<T>`, not `IQueryable<T>`). If callers need rich ad-hoc queries, that's a sign they should use the DbContext directly in a query layer — an honest EF dependency beats a dishonest abstraction.

### Gotcha 2: The Interface Must Live with the High-Level Module — Ownership IS the Inversion

Putting `IOrderRepository` in the `Infrastructure` project next to `SqlOrderRepository` keeps the dependency arrow pointing the wrong way, interfaces or not.

```text
❌ Application ──references──▶ Infrastructure (owns IOrderRepository + SqlOrderRepository)
   High-level still depends on the low-level package. Nothing was inverted —
   Infrastructure can now break Application by changing "its" interface.

✅ Application (owns IOrderRepository)  ◀──references── Infrastructure (owns SqlOrderRepository)
   The concretion points UP to the abstraction. Application compiles with zero
   knowledge that Infrastructure exists. THIS arrow flip is the "inversion" in DIP.
```

Litmus test: delete the Infrastructure project. Does Application still compile? If not, DIP is not in effect regardless of how many interfaces you have.

### Gotcha 3: Injecting `IServiceProvider` Is the Service Locator Wearing a DI Costume

```csharp
// ❌ Passed code review because "it's constructor injection"... of the entire container
public class OrderService
{
    private readonly IServiceProvider _sp;
    public OrderService(IServiceProvider sp) => _sp = sp;

    public async Task PlaceAsync(Order o)
    {
        var repo  = _sp.GetRequiredService<IOrderRepository_Good>();  // hidden dep
        var email = _sp.GetRequiredService<IEmailService_Good>();     // hidden dep
        // Same problems as ServiceLocator: invisible dependencies, runtime failures,
        // and now this class can resolve ANYTHING — its true coupling is unbounded.
    }
}
```

Legitimate `IServiceProvider`/`IServiceScopeFactory` use exists in exactly a few places: the composition root, factories, and Singletons that must create scopes (the captive-dependency fix above). In ordinary services, declare real dependencies.

### Gotcha 4: Constructor Over-Injection Is an SRP Alarm, Not a DI Problem

```csharp
// ❌ Seven dependencies — DI made the god class EASY to build, and painfully visible
public OrderService(IOrderRepository repo, IEmailService email, ISmsService sms,
                    IPaymentGateway pay, IInventoryService inv, ITaxCalculator tax,
                    ILoyaltyService loyalty) { /* ... */ }
```

The wrong fixes: parameter object that just bundles the seven, property injection, or `IServiceProvider` (Gotcha 3) — these hide the count without reducing the coupling. The right fix: the class has too many responsibilities; split it (notifications → one collaborator, pricing → another) or introduce genuine aggregate concepts (`INotificationDispatcher` that composes email+SMS behind one *meaningful* contract). Rule of thumb: past ~4–5 dependencies, question the class, not the container.

### Gotcha 5: Don't Dispose What You Didn't Create

```csharp
public sealed class ReportService : IDisposable
{
    private readonly IOrderRepository_Good _repo;   // injected — container owns it
    public ReportService(IOrderRepository_Good repo) => _repo = repo;

    public void Dispose() => (_repo as IDisposable)?.Dispose();
    // ❌ If _repo is a Singleton, you just killed it for every other consumer
    //    in the application. ObjectDisposedException — far from the crime scene.
}
```

Ownership rule: **whoever creates, disposes.** The container created the dependency, so the container disposes it when its lifetime ends. You only dispose what you `new`ed yourself (or obtained from a factory that transfers ownership, like `IHttpClientFactory.CreateClient` — which, note, you *also* shouldn't dispose eagerly, since the factory manages handler lifetimes).

### Gotcha 6: Not Everything Deserves an Interface

DIP says *high-level policy* shouldn't depend on *volatile details*. It does not say "every class gets an `I`."

```csharp
public record Money(decimal Amount, string Currency);       // value object — no interface
public record PlaceOrderRequest(string CustomerId, ...);    // DTO — no interface
public static class OrderNumberFormatter                    // pure, deterministic — no interface
{ public static string Format(long seq) => $"ORD-{seq:D8}"; }
```

Abstract at the boundaries where **volatility** lives: I/O (database, HTTP, filesystem, queues), time/randomness, third-party SDKs, and things you swap in tests. Stable, deterministic, dependency-free code (domain calculations, value objects, mappers) can be depended on concretely — wrapping it in interfaces adds indirection and mock-heavy tests that assert implementation choreography instead of behavior.

### Gotcha 7: Constructors Must Be Cheap — No I/O, No Async

Containers construct object graphs eagerly and constructors can't `await`, so the classic workarounds are all traps: `.Result` in a constructor (deadlock risk), fire-and-forget init (race conditions), lazy-blocking properties (hidden latency spikes).

```csharp
// ❌ Constructor does I/O — blocks graph construction, can't await, fails un-injectably
public CatalogService(IProductRepository repo)
    => _products = repo.GetAllAsync().Result;   // deadlock + startup stall

// ✅ Options: (a) async factory method, (b) IHostedService that warms state at startup,
//    (c) lazy-async — resolve on first use:
public sealed class CatalogService
{
    private readonly Lazy<Task<IReadOnlyList<Product>>> _products;
    public CatalogService(IProductRepository repo)
        => _products = new(() => repo.GetAllAsync());   // ctor stays trivial

    public async Task<Product?> FindAsync(string sku)
        => (await _products.Value).FirstOrDefault(p => p.Sku == sku);
}
```

---

## Decision Guide

| Situation | Action |
|---|---|
| Class `new`s its own dependencies inside | Constructor injection — pass them in |
| Can't test without a real database / real SMTP | You have hardcoded dependencies — inject abstractions |
| `ServiceLocator.Get<T>()` inside a class method | Replace with constructor injection — make deps explicit |
| Singleton depends on a Scoped service | Use `IServiceScopeFactory` or rethink the lifetime |
| Scoped service has no state between calls | Consider Transient — simpler, safer |
| Need a fresh dependency instance per operation | Inject `Func<T>` or `IServiceScopeFactory` |
| IoC container wiring feels complicated | Simplify the object graph — too many deps signals SRP violation |
| Two implementations of the same interface | Register by name or use a factory/strategy; IoC containers support keyed services (.NET 8+) |
