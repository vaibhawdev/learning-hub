# 04 — Abstraction

> **Core idea:** Show *what* something does. Hide *how* it does it. Callers depend only on the contract, never the implementation.

---

## What is Abstraction?

Abstraction is the practice of reducing a complex system to a simplified interface. A car's steering wheel is an abstraction: turn it, the car turns. You don't need to understand the rack-and-pinion gearbox or hydraulic pump to drive.

In code: a method, interface, or class exposes a **what** (the contract) and hides the **how** (the implementation). Change the how — swap SQL for MongoDB, Stripe for PayPal — without touching a single caller.

---

## The Two Tools of Abstraction in C#

| Tool | Best for | Tradeoff |
|---|---|---|
| `interface` | Pure contracts; unrelated types sharing a capability | No shared state or default behaviour |
| `abstract class` | Shared state + partial implementation | Single inheritance limit; IS-A required |

---

## 5 Real-World Use Cases

1. **`IRepository<T>`** — callers call `GetById()` without knowing if data comes from SQL Server, MongoDB, or Redis.
2. **`IEmailService`** — business logic calls `Send(to, subject, body)` without knowing SendGrid vs SMTP vs test queue.
3. **`ICache`** — callers call `Get(key)` / `Set(key, value, ttl)` without knowing Redis, MemoryCache, or NullCache.
4. **`IPaymentGateway`** — domain logic doesn't know Stripe vs PayPal API internals.
5. **`IFileStorage`** — upload files without knowing local disk vs Azure Blob vs S3.

---

## ❌ Anti-Pattern 1: The IQueryable Leak

The repository is supposed to hide persistence, but it returns `IQueryable<T>`. This is an EF Core–specific type. Callers can now attach `.Include()`, `.Where()`, `.OrderBy()` outside the repository — building queries the repository knows nothing about.

The abstraction provides **zero protection**. You can never swap to MongoDB or Dapper without rewriting every caller.

```csharp
// ❌ IQueryable<T> bleeds EF Core through the interface
public interface IOrderRepository_Leaky
{
    IQueryable<Order> GetAll();    // callers build arbitrary queries outside the repo
}

// Typical caller after this leaks:
var vipOrders = _repo.GetAll()
    .Include(o => o.Items)           // EF Core specific
    .Where(o => o.Amount > 1000)
    .OrderByDescending(o => o.CreatedAt)
    .ToListAsync();
// Now EVERY caller is coupled to EF Core. Swapping the database is impossible.
```

---

## ❌ Anti-Pattern 2: Infrastructure Names in the Interface

The method names reveal the implementation:

```csharp
// ❌ These names leak SQL, table structure, and Redis-specific concepts
public interface IUserStorage_Leaky
{
    void ExecuteSqlInsert(string sql, object parameters);         // SQL leaks
    List<User> FetchFromUsersTable(int page, int size);          // table name leaks
    void CacheUser(User user, bool useRedisCluster = false);     // Redis concept leaks
}
```

If these names are in the contract, swapping the underlying technology requires renaming the interface — which means changing every caller.

---

## ❌ Anti-Pattern 3: The God Service

Not an abstraction — a dumping ground with seven responsibilities and seven reasons to change.

```csharp
public class GodOrderService
{
    public void    ChargeCustomer(decimal amount)      { /* payment */ }
    public void    SendConfirmationEmail(string email) { /* email */ }
    public void    SaveOrder(Order order)              { /* database */ }
    public byte[]  GenerateInvoicePdf(Order order)    { /* PDF */ }
    public decimal CalculateTax(decimal amt, string c) { /* tax */ }
    public void    DecrementStock(int productId, int q) { /* inventory */ }
    public void    TrackConversionEvent(Order order)   { /* analytics */ }
}
// Changing tax rules forces a retest of payment logic.
// A new developer cannot find "where email is sent" without reading the whole class.
```

---

## ✅ Pattern: Clean Repository Abstraction

```csharp
// Domain exceptions — callers catch these, not SqlException or MongoException
public class RepositoryException : Exception
{
    public RepositoryException(string message) : base(message) { }
    public RepositoryException(string message, Exception inner) : base(message, inner) { }
}

public sealed class EntityNotFoundException : RepositoryException
{
    public EntityNotFoundException(string entityType, object id)
        : base($"{entityType} with id '{id}' was not found.") { }
}

// Clean contract — returns domain types, throws domain exceptions, no ORM concepts
public interface IOrderRepository
{
    Task<Order?>                   GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>>     GetByCustomerAsync(string customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>>     GetPendingOrdersAsync(CancellationToken ct = default);
    Task                           SaveAsync(Order order, CancellationToken ct = default);
    Task                           DeleteAsync(OrderId id, CancellationToken ct = default);
}
```

---

## ✅ Pattern: Focused Service Abstractions — One Purpose Each

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string customerId, decimal amount, string currency, CancellationToken ct = default);
    Task<PaymentResult> RefundAsync(string transactionId, decimal amount, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<bool> IsAvailableAsync(ProductId productId, int quantity, CancellationToken ct = default);
    Task       ReserveAsync(ProductId productId, int quantity, CancellationToken ct = default);
    Task       ReleaseAsync(ProductId productId, int quantity, CancellationToken ct = default);
}

public interface ITaxCalculator
{
    decimal Calculate(decimal amount, string countryCode);
}
```

---

## ✅ Pattern: Properly Decomposed Orchestration

The orchestration class has ONE job: coordinate the workflow. It depends only on abstractions — not on SQL, Stripe, or SendGrid. Every dependency is swappable; the class is fully unit-testable.

```csharp
public sealed class OrderPlacementService
{
    private readonly IOrderRepository  _orders;
    private readonly IPaymentGateway   _payment;
    private readonly IInventoryService _inventory;
    private readonly ITaxCalculator    _tax;
    private readonly IEmailService     _email;

    public OrderPlacementService(
        IOrderRepository orders, IPaymentGateway payment,
        IInventoryService inventory, ITaxCalculator tax, IEmailService email)
    {
        _orders    = orders    ?? throw new ArgumentNullException(nameof(orders));
        _payment   = payment   ?? throw new ArgumentNullException(nameof(payment));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _tax       = tax       ?? throw new ArgumentNullException(nameof(tax));
        _email     = email     ?? throw new ArgumentNullException(nameof(email));
    }

    public async Task<OrderPlacementResult> PlaceOrderAsync(
        string customerId, IReadOnlyList<OrderLine> lines,
        string currency, string countryCode, CancellationToken ct = default)
    {
        // 1. Stock check
        foreach (var line in lines)
            if (!await _inventory.IsAvailableAsync(line.ProductId, line.Quantity, ct))
                return OrderPlacementResult.Failure($"No stock for '{line.ProductName}'");

        // 2. Build domain object (validates its own invariants)
        var order = new Order(customerId, lines);

        // 3. Tax + charge
        var total   = order.TotalAmount + _tax.Calculate(order.TotalAmount, countryCode);
        var payment = await _payment.ChargeAsync(customerId, total, currency, ct);
        if (!payment.Success) return OrderPlacementResult.Failure($"Payment failed: {payment.ErrorMessage}");

        // 4. Reserve inventory, persist, notify
        foreach (var line in lines) await _inventory.ReserveAsync(line.ProductId, line.Quantity, ct);
        order.Confirm();
        await _orders.SaveAsync(order, ct);
        await _email.SendAsync(new EmailMessage(customerId, $"Order #{order.Id} Confirmed", "..."), ct);

        return OrderPlacementResult.Ok(order.Id, payment.TransactionId);
    }
}
```

---

## ✅ Pattern: Cache Abstraction — Swappable Implementations

```csharp
public interface ICache
{
    Task<T?>  GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task      SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;
    Task      RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

// NullCache: tests and dev environments — no Redis required
public sealed class NullCache : ICache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
        => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
}
```

---

## Advanced: Specification Pattern — Flexible Querying Without IQueryable

The Specification Pattern lets callers express *what* they want without knowing *how* to fetch it. The repository receives a specification and translates it to SQL/IQueryable internally.

```csharp
public abstract class Specification<T>
{
    public abstract bool IsSatisfiedBy(T entity);

    public Specification<T> And(Specification<T> other) => new AndSpec<T>(this, other);
    public Specification<T> Or(Specification<T> other)  => new OrSpec<T>(this, other);
    public Specification<T> Not()                        => new NotSpec<T>(this);
}

// Composable domain-language specs
public sealed class PendingOrderSpec   : Specification<Order>
{
    public override bool IsSatisfiedBy(Order o) => o.Status == OrderStatus.Pending;
}

public sealed class HighValueOrderSpec : Specification<Order>
{
    private readonly decimal _threshold;
    public HighValueOrderSpec(decimal t) => _threshold = t;
    public override bool IsSatisfiedBy(Order o) => o.TotalAmount >= _threshold;
}

// Compose at call site — no SQL in the caller
var spec = new PendingOrderSpec()
    .And(new HighValueOrderSpec(500m));

var results = await repo.FindAsync(spec);   // repo translates to SQL internally
```

---

## Advanced: Decorator Pattern — Layering Concerns Without Modifying Classes

The Decorator wraps an abstraction with additional behaviour (caching, logging, retry) without modifying the underlying class. The caller never knows which layer it holds.

```csharp
public sealed class CachingOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly ICache           _cache;
    private readonly TimeSpan         _ttl;

    public CachingOrderRepository(IOrderRepository inner, ICache cache, TimeSpan ttl)
        => (_inner, _cache, _ttl) = (inner, cache, ttl);

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
    {
        var key    = $"order:{id}";
        var cached = await _cache.GetAsync<Order>(key, ct);
        if (cached is not null) return cached;

        var order = await _inner.GetByIdAsync(id, ct);
        if (order is not null) await _cache.SetAsync(key, order, _ttl, ct);
        return order;
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        await _inner.SaveAsync(order, ct);
        await _cache.RemoveAsync($"order:{order.Id}", ct);  // invalidate on write
    }

    // Remaining methods delegate to _inner
    public Task<IReadOnlyList<Order>> GetByCustomerAsync(string id, CancellationToken ct)
        => _inner.GetByCustomerAsync(id, ct);
    public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct)
        => _inner.GetPendingOrdersAsync(ct);
    public Task DeleteAsync(OrderId id, CancellationToken ct)
        => _inner.DeleteAsync(id, ct);
}

// Wiring — the caller receives IOrderRepository and never knows about caching
IOrderRepository repo = new CachingOrderRepository(
    new SqlOrderRepository(connectionString),
    new RedisCache(redisConfig),
    ttl: TimeSpan.FromMinutes(5));
```

---

## Advanced: Abstraction Depth — Deep vs Shallow

John Ousterhout's rule from *A Philosophy of Software Design*:

> **"Deep modules: small interface, large functionality."**

| Module | Interface size | Hidden complexity | Verdict |
|---|---|---|---|
| `ICache` (4 methods) | Small | Redis cluster management, serialization, connection pooling | ✅ Deep |
| `IOrderRepository` (5 methods) | Small | SQL queries, connection handling, object mapping | ✅ Deep |
| A wrapper class that just delegates every call | Same as wrapped | Zero reduction | ❌ Shallow — no real abstraction |

A **shallow abstraction** (a pass-through wrapper) adds cognitive overhead without reducing complexity. The hallmark of a shallow abstraction: you can't understand it without reading the implementation anyway.

---

## Advanced: Abstraction Inversion

An abstraction inversion happens when the abstraction is at the wrong level, forcing callers to reconstruct complexity it should have hidden.

```csharp
// ❌ Abstraction at wrong level — too primitive
public interface IOrderRepository_TooLow
{
    Task<IReadOnlyList<Order>> GetAllAsync();  // returns EVERYTHING
}

// Caller forced to filter in memory — O(n) scan of all orders
var vip = (await _repo.GetAllAsync())
    .Where(o => o.TotalAmount > 1000 && o.Status == OrderStatus.Pending)
    .OrderByDescending(o => o.CreatedAt)
    .Take(20)
    .ToList();

// ✅ Abstraction at the right level — caller's domain language
public interface IOrderRepository_Right
{
    Task<IReadOnlyList<Order>> GetPendingHighValueAsync(decimal threshold, int limit, CancellationToken ct);
}
```

**Rule:** Abstractions should be at the level of the *caller's* domain vocabulary, not the implementor's mechanism.

---

## Decision Guide

| Situation | Recommendation |
|---|---|
| Hide SQL / ORM from business logic | `IRepository` returning domain types — no `IQueryable` |
| Flexible querying without ORM exposure | Specification Pattern |
| Add cross-cutting concern (cache, log, retry) | Decorator Pattern |
| Multiple implementations of same behaviour | Interface |
| Shared state + algorithm skeleton | Abstract class |
| Tests need to bypass infrastructure | `NullObject` implementation (NullCache, InMemoryRepo) |
| Caller reconstructing logic the abstraction should hide | Abstraction is at the wrong level — push logic down |
| Wrapper that just delegates every call | Delete the wrapper — it adds no value |
