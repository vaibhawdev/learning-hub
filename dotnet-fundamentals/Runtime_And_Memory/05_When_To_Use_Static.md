# 05 — When To Use Static

> **Core idea:** Use `static` for code that is **pure and stateless**. Use an instance (usually registered in DI) the moment code has **state, dependencies, a lifecycle, or needs to be replaced in a test**.

Demo: [05_When_To_Use_Static.cs](./05_When_To_Use_Static.cs)

---

## Decision table

| Your code... | Use | Example |
|---|---|---|
| Is a pure function: same input, same output, no side effects | **static** | `Math.Max`, `Slugify(string)`, `Clamp(x, lo, hi)` |
| Adds methods to a type you don't own | **static class** (extension methods) | `public static bool IsBlank(this string s)` |
| Is an immutable constant or lookup table | `const` / `static readonly` | `static readonly FrozenSet<string> Reserved = ...` |
| Is a factory that just calls `new` | **static method** | `Money.FromCents(250)` |
| Is the program entry point | **static** | `Main` |
| Holds mutable state | **instance** with synchronisation | counters, caches, queues |
| Talks to a database, network, file system or clock | **instance via interface** | `IOrderRepository`, `TimeProvider` |
| Needs configuration, per tenant or per environment | **instance via DI** | settings, feature flags |
| Owns a resource that must be disposed | **instance** (`IDisposable`) | connections, streams |
| Must be mockable in a unit test | **instance via interface** | anything with side effects |

**One-line test:** *if I had to replace this with a fake in a test, could I?* If you can't, and you would need to, it should not be static.

---

## Static class vs singleton instance

Both give "one of it for the whole app". They are not equivalent:

| | `static class` | Singleton instance (e.g. `AddSingleton<IFoo, Foo>()`) |
|---|---|---|
| Can implement an interface | **No** (CS0714) | Yes |
| Replaceable in tests | **No** | Yes: inject a fake |
| Constructor injection of dependencies | **No** | Yes |
| Disposed on shutdown | **No** | Yes, if it is `IDisposable` and the container owns it |
| Created | When the type is first initialised (timing rules in [04](./04_Static_Classes.md)) | When the container creates it (eagerly or lazily, your choice) |
| Thread safety | **Your job** | **Your job**: a singleton is shared too |
| Memory | Lives until the process ends | Lives until the container is disposed |
| Hides its dependencies | **Yes**: callers look pure but aren't | No: they are in the constructor |

A singleton does not remove the concurrency problem. [`SingletonDemo.cs`](../DependencyInjection/02_ServiceLifetimes/SingletonDemo.cs) shows how to write one safely. What you gain over static is **replaceability and lifecycle**.

---

## What the demos show

### 1. Shared mutable static state loses updates

8 CPUs, 1,000,000 increments from `Parallel.For`:

```
plain static ++  : 344910   (lost 655090)
Interlocked      : 1000000
```

In ASP.NET Core every request is a concurrent caller of your static fields. The exact number varies per run. That it is wrong does not.

### 2. A `static` lambda cannot allocate a closure

1000 calls, same work, different lambdas:

```
static lambda                :       0 bytes total,   0.0 per call
non-static, captures nothing :       0 bytes total,   0.0 per call
capturing lambda             :   64024 bytes total,  64.0 per call  (closure object + new delegate each time)
```

The compiler already caches a lambda that captures nothing, so the `static` keyword does not make that case faster (the second line proves it). What `static` adds is a **compile-time guarantee**: if someone later edits the lambda to capture a local, it fails to compile instead of silently allocating on a hot path.

### 3. Static hides a dependency; an abstraction gives you control

```csharp
static class GreeterStatic { public static string Greet() => DateTime.UtcNow.Hour < 12 ? ... }   // which branch runs depends on when you run the test
class Greeter(TimeProvider clock) { ... clock.GetUtcNow() ... }                                  // test passes any time it wants
```
```
static version : 'Good afternoon'  (depends on when you run it)
injected 09:00 : 'Good morning'
injected 15:00 : 'Good afternoon'
```

`TimeProvider` (.NET 8+) is the framework's own replacement for a static `DateTime.UtcNow` dependency.

### 4. Same consumer, different implementation

```
singleton via interface : order-1, order-2
swapped for a stub      : order-7   (same OrderService, different behaviour)
static class            : order-1   (callers cannot be given a different implementation)
```

`OrderService` takes an `IIdGenerator`, so tests can substitute a stub. A static `IdGeneratorStatic.Next()` cannot be substituted.

---

## Common anti-patterns

| ❌ Anti-pattern | Why it hurts | ✅ Instead |
|---|---|---|
| `static class Utils` with 400 unrelated methods | A dumping ground. Hides dependencies and grows without bounds | Small, cohesive types. Static only for truly pure helpers |
| `static` mutable config (`AppConfig.ConnectionString = ...`) | Global state: order-dependent, racy, untestable | `IOptions<T>` injected where needed |
| `static` cache with no eviction | GC root: grows until the process dies ([03](./03_Garbage_Collection.md)) | `IMemoryCache` with size limit and expiry |
| Static service locator (`ServiceLocator.Get<T>()`) | Hidden dependencies, runtime failures. See [`ServiceLocatorAntiPattern.cs`](../DependencyInjection/06_Gotchas/ServiceLocatorAntiPattern.cs) | Constructor injection |
| `static DbContext` | `DbContext` is not thread-safe and must be scoped per request. See the gotcha note in [`SingletonDemo.cs`](../DependencyInjection/02_ServiceLifetimes/SingletonDemo.cs) | Register `DbContext` as scoped |
| `static` event with forgotten subscribers | Publisher is a root; subscribers never freed | Unsubscribe, or use weak events |
| Static initialiser that does I/O or can throw | Failure is permanent for the process (`TypeInitializationException`, [04](./04_Static_Classes.md)) | Lazy initialisation inside an instance, with retry |

**`HttpClient` is the classic trap.** Creating one per request exhausts sockets, so the old advice was "make it static". The current guidance is `IHttpClientFactory` (or a long-lived client configured with a connection lifetime). A bare static `HttpClient` can hold on to stale DNS results. Check the current official guidance before choosing.

---

## Refactoring a static class into an injectable service

```csharp
// Before: callers are welded to the implementation
public static class PriceCalculator
{
    public static decimal WithTax(decimal net) => net * 1.2m;
}

// After: same logic, now replaceable and configurable
public interface IPriceCalculator { decimal WithTax(decimal net); }

public sealed class PriceCalculator(IOptions<TaxOptions> tax) : IPriceCalculator
{
    public decimal WithTax(decimal net) => net * (1 + tax.Value.Rate);
}

// Program.cs
builder.Services.AddSingleton<IPriceCalculator, PriceCalculator>();   // stateless and thread-safe => Singleton is right
```

Refactor when you hit one of these: you need to fake it in a test, it now needs configuration, it grew a dependency, or it grew mutable state.

If it is genuinely stateless with no dependencies and no need to fake it, leave it static. Not everything needs an interface.

---

## Self-check

1. Name two kinds of code that are *good* candidates for a static class.
2. Why can't a static class be a drop-in replacement for a DI singleton?
3. Is a DI singleton automatically thread-safe? What do you gain over a static class then?
4. You must unit-test a method that depends on `DateTime.UtcNow`. What do you do?
5. Why did the capturing lambda allocate 64 bytes per call while both non-capturing ones allocated 0? What does the `static` keyword add?
6. When should a static helper be refactored into an injected service?

## Related

- [Service lifetimes (Singleton / Scoped / Transient)](../DependencyInjection/02_ServiceLifetimes/README.md)
- [DI gotchas](../DependencyInjection/06_Gotchas/README.md)
- [Dependency Inversion Principle](../SOLID_Principles/05_Dependency_Inversion.md)
