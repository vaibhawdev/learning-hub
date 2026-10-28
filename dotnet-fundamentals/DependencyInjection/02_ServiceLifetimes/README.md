# 02 — Service Lifetimes

## The Three Lifetimes

```
Request 1 ────────────────────────────────────────
  Singleton:  [S1]─────────────────────────────────  (one instance, lives forever)
  Scoped:     [Sc1]────────────────────────────────  (one per request)
  Transient:  [T1] [T2] [T3]                          (new each time resolved)

Request 2 ────────────────────────────────────────
  Singleton:  [S1]─────────────────────────────────  (SAME S1 from request 1!)
  Scoped:     [Sc2]────────────────────────────────  (NEW scope instance)
  Transient:  [T4] [T5]                               (still new each time)
```

---

## Singleton

| Property | Value |
|----------|-------|
| Instances | 1 for the entire application lifetime |
| Thread safety | YOU must make it thread-safe |
| Memory | Held for entire app lifetime |
| Ideal for | Config, caches, connection pools, stateless services |
| Dangerous for | Anything holding user/request state |

**Use cases:**
1. `IConfiguration` — app settings loaded once
2. `IMemoryCache` / `IDistributedCache` — shared cache across requests
3. `HttpClientFactory` — manages socket lifecycle
4. `ILogger<T>` factories — logging infrastructure
5. `ConnectionMultiplexer` (Redis) — expensive to create, meant to be shared

```csharp
services.AddSingleton<ICache, InMemoryCache>();
```

---

## Scoped

| Property | Value |
|----------|-------|
| Instances | 1 per HTTP request (or per explicitly created scope) |
| Thread safety | Safe within a request (single thread per scope by default) |
| Memory | Released when the request/scope ends |
| Ideal for | DbContext, unit-of-work, request-specific state |
| Dangerous for | Injecting into Singleton (captive dependency!) |

**Use cases:**
1. `DbContext` (EF Core) — one context per request = one transaction boundary
2. Repository classes backed by DbContext
3. `ICurrentUserService` — resolves the authenticated user for THIS request
4. Shopping cart / basket service tied to a user session
5. Audit trail collector — aggregates audit events for a single request

```csharp
services.AddScoped<AppDbContext>();
services.AddScoped<IOrderRepository, OrderRepository>();
```

---

## Transient

| Property | Value |
|----------|-------|
| Instances | New one every single time it is resolved |
| Thread safety | Inherently safe (never shared) |
| Memory | Watch for IDisposable transients — container disposes them at scope end |
| Ideal for | Lightweight, stateless operations; formatters, validators, mappers |
| Dangerous for | Heavy objects (DbContext, HttpClient) — creates performance problems |

**Use cases:**
1. Input validators (`IValidator<T>` — FluentValidation registers these as transient)
2. Data mappers / `AutoMapper` profiles
3. One-shot command handlers in CQRS
4. Email template builders — build once, send, discard
5. Guid / ID generators — stateless, cheap

```csharp
services.AddTransient<IOrderValidator, OrderValidator>();
services.AddTransient<IMapper, AutoMapperAdapter>();
```

---

## The Injection Rule Table

| Consumer \ Dependency | Singleton | Scoped | Transient |
|----------------------|-----------|--------|-----------|
| **Singleton**        | OK        | **FORBIDDEN** (captive) | OK* |
| **Scoped**           | OK        | OK     | OK |
| **Transient**        | OK        | OK     | OK |

\* Transient injected into Singleton: the transient instance is captured and lives as long as the Singleton — it becomes effectively a Singleton. This is usually a bug unless you intend it.

---

## Files

| File | Content |
|------|---------|
| `SingletonDemo.cs` | Thread-safe counter, cache — correct singleton patterns |
| `ScopedDemo.cs` | DbContext scope, per-request user context |
| `TransientDemo.cs` | Validators, mappers, lightweight ops |
| `LifetimeCollision.cs` | Shows what happens with captive dependencies + how to fix |
