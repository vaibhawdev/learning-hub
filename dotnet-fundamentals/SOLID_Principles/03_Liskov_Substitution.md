# 03 — Liskov Substitution Principle (LSP)

> **Core idea:** If `S` is a subtype of `T`, you must be able to replace any `T` with an `S` — and the program should still behave correctly. Not just compile — **behave**.

---

## What is LSP?

LSP was formalized by Barbara Liskov in 1987:

> *"If φ(x) is a property provable about objects x of type T, then φ(y) should be true for objects y of type S where S is a subtype of T."*

In plain English: **a derived class must honour the behavioural contract of its base class.** Callers who use a base type should never need to know — or check — which derived type they actually have.

When LSP is violated, you'll see caller code littered with `if (obj is DerivedType)` checks. That's the smell: the abstraction has failed. The client no longer trusts the base type.

---

## The Three Formal Rules

| Rule | Meaning | Violation example |
|---|---|---|
| **Precondition** cannot be strengthened | Derived class cannot demand MORE from callers | Base accepts any positive number; derived only accepts > 100 |
| **Postcondition** cannot be weakened | Derived class cannot deliver LESS to callers | Base guarantees non-null return; derived can return null |
| **Invariants** must be preserved | What was true in base stays true in derived | Rectangle: width and height are independent; Square violates this |

---

## 5 Real-World Use Cases

1. **Rectangle/Square** — the classic violation. Setting `Width` on a `Square` also changes `Height`, breaking any code that assumes they are independent.
2. **Bird/Ostrich** — `Bird.Fly()` is in the contract, but `Ostrich` can't fly. Callers break.
3. **`IReadOnlyList<T>` / `IList<T>`** — `IList<T>` adds mutation methods. A read-only implementation that throws on `Add()` violates LSP.
4. **`NotImplementedException` in overrides** — the most common enterprise LSP violation. "We inherit for the 3 methods we need, stub the other 7."
5. **Stronger validation in derived class** — base `Save(Order)` accepts any order; derived `StrictOrderRepository.Save()` rejects orders under $10. Callers break.

---

## ❌ Classic Violation 1: Square Extends Rectangle

```csharp
public class Rectangle
{
    public virtual int Width  { get; set; }
    public virtual int Height { get; set; }
    public int Area() => Width * Height;
}

public class Square : Rectangle
{
    // ❌ Squares enforce Width == Height by overriding both setters
    public override int Width
    {
        get => base.Width;
        set { base.Width = value; base.Height = value; }  // changes Height too!
    }
    public override int Height
    {
        get => base.Height;
        set { base.Height = value; base.Width = value; }  // changes Width too!
    }
}

// ❌ This method works correctly with Rectangle, breaks silently with Square
public static void ResizeToDoubleWidth(Rectangle r)
{
    var originalHeight = r.Height;
    r.Width *= 2;

    // For Rectangle: area = 2W × H ✅
    // For Square:    area = (2W) × (2W) — because setting Width also set Height!
    // The assertion FAILS for Square
    System.Diagnostics.Debug.Assert(
        r.Area() == r.Width * originalHeight,
        $"Expected {r.Width * originalHeight} but got {r.Area()}");
}

// Usage — caller has no idea it's broken
Rectangle shape = new Square();
shape.Width  = 4;
shape.Height = 5;
Console.WriteLine(shape.Width);   // 5 — not 4! Square silently changed it.
Console.WriteLine(shape.Height);  // 5 — expected 5, got 5 by accident
Console.WriteLine(shape.Area());  // 25 — expected 20
```

---

## ❌ Classic Violation 2: NotImplementedException

The most common LSP violation in real enterprise codebases.

```csharp
public abstract class ReportGenerator_Base
{
    public abstract byte[] GeneratePdf();
    public abstract byte[] GenerateCsv();
    public abstract byte[] GenerateExcel();
    public abstract void   EmailReport(string to);
    public abstract void   ArchiveReport(string path);
}

// Only needs PDF — forced to "implement" the other 4
public class SimplePdfReport : ReportGenerator_Base
{
    public override byte[] GeneratePdf()  => new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    public override byte[] GenerateCsv()  => throw new NotImplementedException();    // ❌
    public override byte[] GenerateExcel() => throw new NotImplementedException();   // ❌
    public override void   EmailReport(string to) => throw new NotImplementedException(); // ❌
    public override void   ArchiveReport(string path) => throw new NotImplementedException(); // ❌
}

// Caller code breaks at runtime — the abstraction was a lie
public static void ProcessReport(ReportGenerator_Base gen)
{
    var pdf  = gen.GeneratePdf();   // works
    var csv  = gen.GenerateCsv();   // ❌ throws for SimplePdfReport
    gen.EmailReport("boss@co.com"); // ❌ throws for SimplePdfReport
}
```

---

## ❌ Classic Violation 3: Strengthened Precondition

```csharp
public class OrderRepository
{
    // Base contract: saves any valid order
    public virtual void Save(Order order)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        // persist...
    }
}

public class AuditedOrderRepository : OrderRepository
{
    // ❌ Derived strengthens the precondition — now rejects orders under $10
    // Callers that pass small orders expect this to work (the base allowed it)
    public override void Save(Order order)
    {
        if (order.Total < 10)                           // ❌ stronger precondition
            throw new InvalidOperationException("Orders under $10 are not auditable");
        base.Save(order);
    }
}

// Caller breaks at runtime — only if it happens to send a small order
void ProcessOrders(OrderRepository repo, IEnumerable<Order> orders)
{
    foreach (var o in orders)
        repo.Save(o);  // ❌ breaks silently when AuditedOrderRepository is injected
}
```

---

## ✅ Fix 1: Flatten the Rectangle/Square Hierarchy

Don't model Square as a subtype of Rectangle. They are both Shapes with an Area — that's the correct abstraction level.

```csharp
public interface IShape
{
    double Area();
    double Perimeter();
}

// Immutable — no setters to violate invariants after construction
public sealed class Rectangle : IShape
{
    public double Width  { get; }
    public double Height { get; }

    public Rectangle(double width, double height)
    {
        if (width  <= 0) throw new ArgumentException("Width must be positive");
        if (height <= 0) throw new ArgumentException("Height must be positive");
        Width  = width;
        Height = height;
    }

    public double Area()      => Width * Height;
    public double Perimeter() => 2 * (Width + Height);

    // Want a different size? Return a new instance — immutability prevents invariant violations
    public Rectangle WithWidth(double width)   => new(width, Height);
    public Rectangle WithHeight(double height) => new(Width, height);
}

public sealed class Square : IShape
{
    public double Side { get; }

    public Square(double side)
    {
        if (side <= 0) throw new ArgumentException("Side must be positive");
        Side = side;
    }

    public double Area()      => Side * Side;
    public double Perimeter() => 4 * Side;
}

// ✅ This now works correctly for both types — no broken assertions
public static void PrintArea(IShape shape)
    => Console.WriteLine($"{shape.GetType().Name} area: {shape.Area():F2}");
```

---

## ✅ Fix 2: Interface Segregation Solves NotImplementedException

If a class can't implement all methods of an interface, the interface is too fat. Split it (see also ISP principle).

```csharp
// Focused interfaces — each implementor takes only what applies
public interface IPdfExportable  { byte[] ExportPdf(); }
public interface ICsvExportable  { byte[] ExportCsv(); }
public interface IEmailSendable  { Task SendEmailAsync(string to); }
public interface IArchiveable    { Task ArchiveAsync(string path); }

// ✅ SimplePdfReport only implements what it actually supports
public sealed class SimplePdfReport : IPdfExportable
{
    public byte[] ExportPdf() => new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    // No stubs. No NotImplementedException. LSP is honoured.
}

// ✅ FullReport implements everything it genuinely supports
public sealed class FullReport : IPdfExportable, ICsvExportable, IEmailSendable, IArchiveable
{
    public byte[] ExportPdf()               => new byte[] { 0x25, 0x50, 0x44, 0x46 };
    public byte[] ExportCsv()               => System.Text.Encoding.UTF8.GetBytes("Name,Amount\n");
    public Task   SendEmailAsync(string to) { Console.WriteLine($"Email to {to}"); return Task.CompletedTask; }
    public Task   ArchiveAsync(string path) { Console.WriteLine($"Archived to {path}"); return Task.CompletedTask; }
}

// Callers request EXACTLY the capabilities they need — no surprises
public static void ExportToCsv(ICsvExportable report)
{
    var data = report.ExportCsv();   // guaranteed to work — only ICsvExportable types are accepted
    Console.WriteLine($"Exported {data.Length} bytes");
}
```

---

## ✅ Fix 3: Honest Contracts for Preconditions

Derived classes must not strengthen preconditions. If special rules are needed, handle them at the call site, not inside the override.

```csharp
public interface IOrderRepository
{
    // Contract: persists any non-null, valid Order
    Task SaveAsync(Order order, CancellationToken ct = default);
}

public sealed class SqlOrderRepository : IOrderRepository
{
    public Task SaveAsync(Order order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        // Persists any order — same precondition as the interface
        Console.WriteLine($"Saved order {order.Id}");
        return Task.CompletedTask;
    }
}

// ✅ Audit requirement handled in a decorator — doesn't change the contract
public sealed class AuditingOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IAuditLog        _audit;

    public AuditingOrderRepository(IOrderRepository inner, IAuditLog audit)
        => (_inner, _audit) = (inner, audit);

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        await _audit.RecordAsync($"Saving order {order.Id}", ct);
        await _inner.SaveAsync(order, ct);  // same precondition — no strengthening
    }
}

public interface IAuditLog { Task RecordAsync(string message, CancellationToken ct = default); }

public sealed class Order { public Guid Id { get; } = Guid.NewGuid(); public decimal Total { get; init; } }
```

---

## Advanced: Postcondition Weakening

```csharp
// ❌ Base guarantees non-null. Derived weakens the guarantee.
public class UserRepository_Base
{
    // Postcondition: NEVER returns null — throws EntityNotFoundException instead
    public virtual User GetById(Guid id)
        => throw new NotImplementedException("Implemented in derived");
}

public class CachingUserRepository : UserRepository_Base
{
    // ❌ Returns null when not in cache instead of throwing — weakens the postcondition
    public override User GetById(Guid id)
    {
        return null!;   // Caller trusted the base contract: non-null. Now NRE at call site.
    }
}

// ✅ Return an Option/Maybe type when null is a legitimate outcome — be honest in the signature
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    // The '?' in User? is the honest contract — null IS possible, caller must handle it
}
```

---

## Advanced: LSP and Covariance/Contravariance in C#

C# generics support variance declarations that enforce LSP at the type system level.

```csharp
// Covariant out — IProducer<Derived> is substitutable for IProducer<Base>
public interface IProducer<out T>
{
    T Produce();
}

// Contravariant in — IConsumer<Base> is substitutable for IConsumer<Derived>
public interface IConsumer<in T>
{
    void Consume(T item);
}

// Example:
public class Animal  { }
public class Dog : Animal { }

IProducer<Dog> dogProducer = null!;
IProducer<Animal> animalProducer = dogProducer;    // ✅ covariance — Dog IS-A Animal

IConsumer<Animal> animalConsumer = null!;
IConsumer<Dog> dogConsumer = animalConsumer;        // ✅ contravariance — consumer of Animal can consume Dog

// This is the type-system enforcement of LSP:
// A producer of a more specific type (Dog) can be used where a producer of a less specific type (Animal) is expected.
```

---

## More Real-World Examples

### Example: The BCL's Own LSP Tension — `Stream`

.NET's `Stream` is a fat base class: not every stream can seek, read, AND write. The BCL's pragmatic compromise is **capability flags** — worth knowing because you'll meet this pattern, and its cost, in real code.

```csharp
public static void CopyToStart(Stream source, Stream destination)
{
    // Callers must interrogate capabilities before calling — the type alone isn't a contract
    if (!source.CanRead)   throw new ArgumentException("Source not readable");
    if (!destination.CanWrite) throw new ArgumentException("Destination not writable");
    if (source.CanSeek) source.Position = 0;   // NetworkStream: CanSeek == false → skip

    source.CopyTo(destination);
}
// This WORKS, but notice the price: every caller carries if-checks that a
// properly segregated design (IReadableStream / ISeekableStream) would have
// made compile-time guarantees. Capability flags are LSP damage control, not LSP.
// Lesson: when you design your OWN hierarchy, you get to choose segregated
// interfaces up front instead of inheriting this tension.
```

### Example: `ReadOnlyCollection<T>` — a Known LSP Violation You Use Daily

```csharp
ICollection<int> numbers = new List<int> { 1, 2, 3 }.AsReadOnly();
numbers.Add(4);   // 💥 NotSupportedException at runtime
// ICollection<T> promises Add/Remove/Clear. ReadOnlyCollection<T> implements
// the interface but throws on all mutations — flagged only by IsReadOnly.
```

The modern fix in your own code: accept the *honest* interface instead.

```csharp
// ❌ Asks for more capability than it uses — invites the runtime surprise above
public decimal Sum(ICollection<decimal> values) => values.Sum();

// ✅ Asks for exactly what it needs — read-only implementations are fully substitutable
public decimal Sum(IReadOnlyCollection<decimal> values) => values.Sum();
```

### Example: Payment Gateway Family — Substitutable by Contract Test

The professional way to *prove* LSP: one shared contract test suite that every implementation must pass.

```csharp
public interface IPaymentGateway
{
    // Contract: charges amount > 0; returns a non-null receipt with a non-empty TransactionId;
    // throws PaymentDeclinedException (never anything else) on decline.
    Task<PaymentReceipt> ChargeAsync(decimal amount, string cardToken, CancellationToken ct = default);
}

// One abstract test class; each gateway gets a concrete subclass. If StripeGateway
// passes and PayPalGateway fails "Charge_SmallAmount_Succeeds", you found the
// LSP violation BEFORE production did.
public abstract class PaymentGatewayContractTests
{
    protected abstract IPaymentGateway CreateGateway();

    [Fact] public async Task Charge_PositiveAmount_ReturnsTransactionId()
    {
        var receipt = await CreateGateway().ChargeAsync(10.00m, "tok_valid");
        Assert.False(string.IsNullOrEmpty(receipt.TransactionId));
    }

    [Fact] public async Task Charge_Declined_ThrowsPaymentDeclinedException_Only()
        => await Assert.ThrowsAsync<PaymentDeclinedException>(
               () => CreateGateway().ChargeAsync(10.00m, "tok_declined"));
}

public sealed class StripeGatewayTests : PaymentGatewayContractTests
{ protected override IPaymentGateway CreateGateway() => new StripeGateway(/* test config */); }

public sealed class PayPalGatewayTests : PaymentGatewayContractTests
{ protected override IPaymentGateway CreateGateway() => new PayPalGateway(/* test config */); }
```

---

## Gotchas

### Gotcha 1: `new` Method Hiding Compiles Fine and Silently Kills Polymorphism

The `new` keyword *hides* the base method instead of overriding it. Which method runs depends on the **compile-time type of the variable** — the exact opposite of substitutability.

```csharp
public class Logger        { public virtual void Log(string m) => Console.WriteLine($"[BASE] {m}"); }
public class FileLogger : Logger
{
    public new void Log(string m) => Console.WriteLine($"[FILE] {m}");   // ❌ hides, not overrides
}

FileLogger direct = new FileLogger();
Logger     asBase = direct;              // same object!

direct.Log("hello");   // [FILE] hello
asBase.Log("hello");   // [BASE] hello  ← same object, different behavior by reference type
```

If you see `new` on a method, treat it as a bug until proven otherwise. The compiler even warns (CS0108) when you hide without the keyword — don't silence it with `new`, fix the design with `override` or a rename.

### Gotcha 2: Overrides That Throw *Different* Exception Types Break Callers

Exception types are part of the behavioral contract. A caller that catches what the base documents will miss what the derived actually throws.

```csharp
public class Repository
{
    // Documented contract: throws EntityNotFoundException when missing
    public virtual Order GetRequired(Guid id) => throw new EntityNotFoundException(id);
}

public class CachingRepository : Repository
{
    public override Order GetRequired(Guid id)
        => _cache.TryGetValue(id, out var o)
            ? o
            : throw new KeyNotFoundException($"{id} not in cache");   // ❌ new exception type
}

// Caller wrote correct code against the base contract — now broken:
try { return repo.GetRequired(id); }
catch (EntityNotFoundException) { return null; }   // KeyNotFoundException flies past → 500
```

**Rule:** an override may throw the same exceptions or *subtypes* of them — never sibling or broader types.

### Gotcha 3: Virtual Calls in Base Constructors Run Before the Derived Class Exists

```csharp
public class ReportBase
{
    public ReportBase() => Initialize();          // ❌ virtual call in constructor
    protected virtual void Initialize() { }
}

public class SqlReport : ReportBase
{
    private readonly string _connectionString;

    public SqlReport(string cs) : base()          // base ctor runs FIRST...
        => _connectionString = cs;                // ...this line hasn't run yet

    protected override void Initialize()
        => Connect(_connectionString);            // 💥 _connectionString is null here
}
```

C# constructs base-first, and virtual dispatch already targets the derived override — which then reads uninitialized derived fields. (C++ "solves" this by not dispatching virtually in ctors; C# dispatches and lets you crash.) **Fix:** never call virtual members from a constructor; use a factory method or an explicit `Initialize()` step after construction.

### Gotcha 4: Extra Side Effects Are Contract Violations Too — Even with Identical Signatures

LSP isn't only about parameters, returns, and exceptions. A derived class that does *more* than the contract says can be just as breaking.

```csharp
public class OrderRepository
{
    public virtual Task SaveAsync(Order o) { /* persists. That's ALL the contract promises. */ }
}

public class NotifyingOrderRepository : OrderRepository
{
    public override async Task SaveAsync(Order o)
    {
        await base.SaveAsync(o);
        await _email.SendOrderConfirmationAsync(o);   // ❌ surprise side effect
    }
}

// Import job saves 50,000 historical orders through the base-typed reference...
// → 50,000 confirmation emails to customers. Signature identical; contract shattered.
```

If the extra behavior is genuinely wanted, make it visible in composition (a decorator registered deliberately for the checkout path only) — not hidden inside a subtype that *anyone* might receive.

### Gotcha 5: Sync-over-Async in One Implementation Poisons All Callers

```csharp
public interface IPriceFeed { Task<decimal> GetPriceAsync(string symbol); }

public sealed class CachedPriceFeed : IPriceFeed
{
    public Task<decimal> GetPriceAsync(string symbol)
        => Task.FromResult(_cache[symbol]);           // fine — genuinely async-compatible
}

public sealed class LegacyPriceFeed : IPriceFeed
{
    public Task<decimal> GetPriceAsync(string symbol)
        => Task.FromResult(_legacyClient.GetPrice(symbol));
        // ❌ _legacyClient.GetPrice blocks a thread for 2s under the hood.
        // Callers chose the async contract expecting non-blocking behavior;
        // under load this implementation exhausts the thread pool — but only
        // in the environment where LegacyPriceFeed happens to be registered.
}
```

Performance/blocking characteristics are the fuzziest part of a contract, but callers *do* depend on them. If an implementation must block, document it loudly or run it on a dedicated scheduler — don't let it masquerade as a drop-in substitute.

### Gotcha 6: Mutable Base-Class State + Derived Invariants Don't Mix

The Rectangle/Square disaster generalizes: **any** derived class that must keep two base properties in sync will break callers who set them independently. If a subtype needs to constrain base state, the hierarchy is wrong — the "extra constraint" is the invariant of a *different* type. Prefer immutable types (invariants fixed at construction) or composition (`Square` *has* a side, not *is* a rectangle).

---

## Decision Guide

| Situation | Action |
|---|---|
| Subclass changes how Width/Height interact | Don't subclass — use composition or flatten the hierarchy |
| Override throws `NotImplementedException` | Interface is too fat — apply ISP; implement only what you support |
| Client code has `if (obj is DerivedType)` checks | LSP violation — the base type doesn't honour its contract |
| Derived class strengthens a method's input validation | Move the special validation to the call site or a decorator |
| Derived class can return null when base guarantees non-null | Change the signature to `T?` and make the contract honest |
| Two types share properties but have conflicting behaviour | They are NOT in an IS-A relationship — use a shared interface instead |
| `abstract` method in base that some derived classes can't implement | Extract that method to a separate interface |
