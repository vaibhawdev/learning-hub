# 04 — Interface Segregation Principle (ISP)

> **Core idea:** Clients should not be forced to depend on methods they don't use. Keep interfaces lean and focused on what a specific client actually needs.

---

## What is ISP?

Robert Martin's definition:

> *"Clients should not be forced to depend upon interfaces that they do not use."*

A **fat interface** (one with many methods) forces every implementor to provide all of them — even the ones that make no sense for that type. The result:

- Implementors stub irrelevant methods with `throw new NotImplementedException()` — an LSP violation.
- A change to any part of the fat interface forces recompilation of all implementors.
- Callers receive capabilities they never asked for, creating unexpected coupling.

ISP says: design interfaces from the **client's point of view**, not the implementor's. What does *this specific caller* need? Give it exactly that, nothing more.

---

## Key Concepts

| Concept | Meaning |
|---|---|
| **Fat interface** | One interface with many methods, serving many different clients |
| **Role interface** | A small, focused interface representing one capability (one "role" a type can play) |
| **Client perspective** | Interfaces are defined by what callers need, not by what implementors provide |
| **Forced stub** | Implementing a method with `throw new NotImplementedException()` — the ISP smell |
| **Recompilation coupling** | Any change to a fat interface forces all implementors to recompile |

---

## 5 Real-World Use Cases

1. **IWorker** → `IWorkable` + `IFeedable` + `ISleepable` — robots work but don't eat or sleep.
2. **Printer interface** → `IPrintable` + `IScannable` + `IFaxable` — a basic printer only prints.
3. **IRepository<T>** → `IReadRepository<T>` + `IWriteRepository<T>` — read replicas only read.
4. **IUserService** → `IAuthService` + `IProfileService` + `INotificationService` — each consumer depends on only what it uses.
5. **IOrderService** → `IOrderQuery` + `IOrderCommand` — CQRS alignment; query side doesn't need write methods.

---

## ❌ Anti-Pattern: Fat Interface

Every implementor must provide all 8 methods, even the ones that don't apply. `BasicPrinter` can't scan or fax — but the interface forces it to lie.

```csharp
// ❌ Fat interface — serves 4 different clients at once
public interface IMultiFunctionDevice
{
    void Print(byte[] document);
    byte[] Scan();
    void Fax(byte[] document, string phoneNumber);
    void Staple(int copies);
    bool CheckInkLevel();
    void SetPaperSize(string size);
    void ColourCalibrate();
    bool IsOnline();
}

// ❌ BasicPrinter: can only print. Forced to stub 7 methods.
public class BasicPrinter : IMultiFunctionDevice
{
    public void  Print(byte[] document) => Console.WriteLine($"Printing {document.Length} bytes");
    public bool  IsOnline()             => true;
    public bool  CheckInkLevel()        => true;

    // ❌ Forced stubs — these don't apply, but the interface demands them
    public byte[] Scan()                                       => throw new NotImplementedException();
    public void   Fax(byte[] doc, string phone)               => throw new NotImplementedException();
    public void   Staple(int copies)                          => throw new NotImplementedException();
    public void   SetPaperSize(string size)                   => throw new NotImplementedException();
    public void   ColourCalibrate()                           => throw new NotImplementedException();
}

// ❌ Caller that only needs printing is still coupled to the entire interface
public class DocumentService
{
    private readonly IMultiFunctionDevice _device;
    public DocumentService(IMultiFunctionDevice device) => _device = device;

    public void PrintDocument(byte[] doc)
    {
        if (!_device.IsOnline()) throw new InvalidOperationException("Device offline");
        _device.Print(doc);
        // DocumentService has no idea Scan/Fax/Staple exist — but it depends on them anyway.
        // If Scan()'s signature changes, DocumentService must recompile even though it never calls it.
    }
}
```

**Why this is dangerous at scale:** When `IMultiFunctionDevice` gains a new method (e.g., `DuplexPrint()`), every class that implements it — across the entire codebase — must be updated, even those that only print.

---

## ✅ Pattern: Role Interfaces

Each interface represents one capability. Implementors take only the roles they can genuinely fill. Callers depend only on what they use.

```csharp
// Lean, focused role interfaces
public interface IPrintable     { void  Print(byte[] document); }
public interface IScannable     { byte[] Scan(); }
public interface IFaxable       { void  Fax(byte[] document, string phoneNumber); }
public interface IStapleable    { void  Staple(int copies); }
public interface IDeviceStatus  { bool  IsOnline(); bool CheckInkLevel(); }

// ✅ BasicPrinter — only what it genuinely supports
public sealed class BasicPrinter : IPrintable, IDeviceStatus
{
    public void Print(byte[] document) => Console.WriteLine($"[BasicPrinter] Printing {document.Length} bytes");
    public bool IsOnline()             => true;
    public bool CheckInkLevel()        => true;
    // No stubs. No NotImplementedException. Clean.
}

// ✅ OfficePrinter — supports everything
public sealed class OfficePrinter : IPrintable, IScannable, IFaxable, IStapleable, IDeviceStatus
{
    public void   Print(byte[] document)              => Console.WriteLine($"[OfficePrinter] Printing");
    public byte[] Scan()                              => new byte[] { 0xFF, 0xD8 }; // JPEG header
    public void   Fax(byte[] doc, string phoneNumber) => Console.WriteLine($"Faxing to {phoneNumber}");
    public void   Staple(int copies)                  => Console.WriteLine($"Stapling {copies} copies");
    public bool   IsOnline()                          => true;
    public bool   CheckInkLevel()                     => true;
}

// ✅ NetworkScanner — only scans, doesn't print or fax
public sealed class NetworkScanner : IScannable, IDeviceStatus
{
    public byte[] Scan()          => new byte[] { 0xFF, 0xD8 };
    public bool   IsOnline()      => true;
    public bool   CheckInkLevel() => true;
}

// ✅ Callers depend ONLY on what they use
public sealed class DocumentPrintService
{
    private readonly IPrintable    _printer;
    private readonly IDeviceStatus _status;

    public DocumentPrintService(IPrintable printer, IDeviceStatus status)
        => (_printer, _status) = (printer, status);

    public void PrintDocument(byte[] doc)
    {
        if (!_status.IsOnline()) throw new InvalidOperationException("Device offline");
        _printer.Print(doc);
    }
}

public sealed class DocumentScanService
{
    private readonly IScannable _scanner;

    public DocumentScanService(IScannable scanner) => _scanner = scanner;

    public byte[] ScanDocument() => _scanner.Scan();
}
```

---

## Intermediate: ISP in Repository Design

Read replicas can only read. A fat `IRepository<T>` forces them to stub write methods.

```csharp
// ❌ Fat repository — read replicas can't write, but the interface demands it
public interface IRepository_Fat<T>
{
    T?             GetById(Guid id);
    IList<T>       GetAll();
    IList<T>       FindBySpec(Func<T, bool> spec);
    void           Save(T entity);
    void           Delete(Guid id);
    void           BulkInsert(IEnumerable<T> entities);
    void           BulkDelete(IEnumerable<Guid> ids);
    int            Count();
}

// ✅ Segregated into read and write contracts
public interface IReadRepository<T>
{
    Task<T?>               GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Func<T, bool> predicate, CancellationToken ct = default);
    Task<int>              CountAsync(CancellationToken ct = default);
}

public interface IWriteRepository<T>
{
    Task SaveAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken ct = default);
}

// Full read-write implementation
public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T> { }

// ✅ Read replica only needs to implement IReadRepository<T>
public sealed class ReadReplicaOrderRepository : IReadRepository<Order>
{
    public Task<Order?>               GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Order?>(null);  // real impl queries read replica

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Order>>(new List<Order>());

    public Task<IReadOnlyList<Order>> FindAsync(Func<Order, bool> pred, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Order>>(new List<Order>());

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(0);
    // No write stubs needed.
}

public sealed class Order { public Guid Id { get; init; } = Guid.NewGuid(); }
```

---

## Intermediate: ISP in CQRS — Separating Query from Command

CQRS naturally aligns with ISP: query callers don't need write methods; command handlers don't need query methods.

```csharp
public interface IOrderQueryService
{
    Task<OrderSummary?>        GetSummaryAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetPendingAsync(CancellationToken ct = default);
    Task<OrderStatistics>      GetStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public interface IOrderCommandService
{
    Task<Guid>  PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);
    Task        CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default);
    Task        ConfirmPaymentAsync(Guid orderId, string transactionId, CancellationToken ct = default);
}

// Dashboard controller — only queries, no commands
public sealed class DashboardController
{
    private readonly IOrderQueryService _queries;
    public DashboardController(IOrderQueryService queries) => _queries = queries;
    // Never sees CancelOrder or ConfirmPayment — no accidental coupling
}

// Order API controller — only commands
public sealed class OrderCommandController
{
    private readonly IOrderCommandService _commands;
    public OrderCommandController(IOrderCommandService commands) => _commands = commands;
    // Never sees GetStats or GetPending — no accidental coupling
}

public record OrderSummary(Guid Id, string Status, decimal Total);
public record OrderStatistics(int TotalOrders, decimal TotalRevenue);
public record PlaceOrderRequest(string CustomerId, IReadOnlyList<string> Items);
```

---

## Advanced: ISP + Default Interface Methods (C# 8)

Default interface methods let you add methods without breaking existing implementors — a middle ground between fat interfaces and interface proliferation.

```csharp
public interface ILogger
{
    void Log(string level, string message);

    // Defaults — existing loggers get these for free
    void Info(string msg)    => Log("INFO",    msg);
    void Warning(string msg) => Log("WARNING", msg);
    void Error(string msg)   => Log("ERROR",   msg);
}

// A minimal logger only implements Log() — gets Info/Warning/Error via defaults
// An advanced logger overrides them for performance (avoiding string allocation for filtered levels)
public sealed class HighPerformanceLogger : ILogger
{
    private readonly string _minLevel;
    public HighPerformanceLogger(string minLevel) => _minLevel = minLevel;

    public void Log(string level, string message)
        => Console.WriteLine($"[{level}] {message}");

    // Override Warning to check the level filter before allocating the string
    void ILogger.Warning(string msg)
    {
        if (_minLevel is "ERROR") return;  // skip allocation if filtered
        Log("WARNING", msg);
    }
}
```

**Caution:** Use default methods only for backward-compatible evolution of existing interfaces — not as a substitute for proper interface design. If you're adding defaults to avoid splitting a fat interface, you're treating the symptom, not the disease.

---

## Advanced: The ISP–LSP Relationship

ISP and LSP are tightly linked. Fat interfaces cause LSP violations: when an interface is too large, implementors cannot honour every method's contract — so they throw `NotImplementedException`, which breaks the substitutability guarantee.

```
Fat Interface
     │
     ▼
Implementor can't honour all methods
     │
     ▼
Implementor stubs with NotImplementedException
     │
     ▼
Caller receives surprise exception at runtime (LSP broken)

Fix:
     │
     ▼
Split interface (ISP)
     │
     ▼
Implementor only takes what it supports
     │
     ▼
No stubs — LSP honoured automatically
```

---

## More Real-World Examples

### Example: The BCL Collection Hierarchy — ISP Done Right

.NET's collection interfaces are a layered set of role interfaces. Each layer adds one capability, and methods ask for the *lowest* layer they can work with.

```
IEnumerable<T>          → "I can be iterated"           (foreach, LINQ)
IReadOnlyCollection<T>  → + "I know my Count"
IReadOnlyList<T>        → + "I support index access"
ICollection<T>          → + "I can be mutated"          (Add/Remove/Clear)
IList<T>                → + "I can be mutated by index"
```

```csharp
// ❌ Demands mutation capability it never uses — and List<T> only
public decimal Total(List<OrderLine> lines) => lines.Sum(l => l.Subtotal);

// ✅ Asks for exactly what it needs — accepts arrays, List<T>, ImmutableList<T>,
//    EF query results, anything iterable. Also SIGNALS "I won't mutate your list."
public decimal Total(IEnumerable<OrderLine> lines) => lines.Sum(l => l.Subtotal);

// ✅ Needs Count + indexing, still promises no mutation
public OrderLine Median(IReadOnlyList<OrderLine> lines) => lines[lines.Count / 2];
```

**Parameter rule of thumb:** accept the narrowest interface that supports what the method does; return the richest type callers reasonably need.

### Example: Notification Channels + Composite Dispatcher

```csharp
// One role per channel — a client that only sends email never sees SMS config concerns
public interface IEmailSender { Task SendEmailAsync(string to, string subject, string body); }
public interface ISmsSender   { Task SendSmsAsync(string phone, string text); }
public interface IPushSender  { Task SendPushAsync(string deviceId, string title, string body); }

// Password reset needs email ONLY. It cannot even accidentally send an SMS.
public sealed class PasswordResetNotifier
{
    private readonly IEmailSender _email;
    public PasswordResetNotifier(IEmailSender email) => _email = email;

    public Task NotifyAsync(string userEmail, string token)
        => _email.SendEmailAsync(userEmail, "Reset your password", $"Token: {token}");
}

// Order confirmation fans out to everything the user opted into
public sealed class OrderConfirmationNotifier
{
    private readonly IEmailSender _email;
    private readonly ISmsSender   _sms;
    public OrderConfirmationNotifier(IEmailSender email, ISmsSender sms)
        => (_email, _sms) = (email, sms);

    public async Task NotifyAsync(Customer c, Guid orderId)
    {
        await _email.SendEmailAsync(c.Email, "Order confirmed", $"Order {orderId}");
        if (c.SmsOptIn) await _sms.SendSmsAsync(c.Phone, $"Order {orderId} confirmed");
    }
}
// A TwilioGateway can implement ISmsSender AND IPushSender — one class, two roles.
// Roles describe what CALLERS need; classes take as many roles as they truly support.
```

### Example: `IDisposable` — the Smallest Role Interface You Already Know

`IDisposable` is one method, one capability, mixed into thousands of otherwise unrelated types. Nobody asks a `SqlConnection` to be "fully disposable-and-loggable-and-serializable" — capabilities compose per type. That's the end state ISP pushes you toward: types assemble small roles instead of inheriting one giant contract.

---

## Gotchas

### Gotcha 1: Interface Explosion — ISP Taken to Absurdity

Splitting by *client need* is the principle. Splitting every method into its own interface is a cargo cult.

```csharp
// ❌ Nano-interfaces nobody asked for
public interface IOrderSavable    { Task SaveAsync(Order o); }
public interface IOrderDeletable  { Task DeleteAsync(Guid id); }
public interface IOrderUpdatable  { Task UpdateAsync(Order o); }
// No client ever wants "delete but not update". These three change together,
// are implemented together, and are consumed together → they are ONE role:
public interface IOrderWriteRepository
{
    Task SaveAsync(Order o);
    Task UpdateAsync(Order o);
    Task DeleteAsync(Guid id);
}
```

**The test:** split when a *real client* uses one subset and another client uses a different subset. If every consumer uses all the methods, splitting adds files and `where T : IA, IB, IC` noise without removing any coupling.

### Gotcha 2: Header Interfaces — the 1:1 `IFoo`/`Foo` Mirror Ritual

An interface that exactly mirrors one class's public surface, extracted only "so we can mock it", is not ISP — it's still the implementor's perspective, just with an `I` in front.

```csharp
// ❌ IUserService is UserService's silhouette — 14 methods, one implementor,
//    every consumer still sees all 14 methods
public interface IUserService
{
    Task Register(...); Task Login(...); Task ResetPassword(...);
    Task UpdateProfile(...); Task UploadAvatar(...); Task GetPreferences(...);
    /* ...8 more... */
}
```

ISP asks: *what does each caller need?* The login page needs `Login` + `ResetPassword`. The profile page needs `UpdateProfile` + `UploadAvatar`. Carve interfaces from the **caller's** side (`IAuthenticator`, `IProfileEditor`) — the single `UserService` class can implement several of them. Mockability then falls out for free, per role.

### Gotcha 3: A "Segregated" Interface That Leaks Implementation Types Isn't Segregated

The method *count* can be perfect while the *signatures* re-couple every client to the infrastructure.

```csharp
// ❌ Two methods — but both signatures drag EF Core into every consumer
public interface IOrderReader
{
    IQueryable<Order> Query();                       // IQueryable = EF semantics leak
    Task<Order?> FindAsync(DbConnection connection); // caller must know about ADO.NET
}

// ✅ Contracts speak the domain language only
public interface IOrderReader
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetPendingAsync(CancellationToken ct = default);
}
```

Check every parameter and return type: if a role interface mentions `DbContext`, `IQueryable`, `HttpContext`, or a vendor SDK type, clients depend on that stack no matter how few methods there are.

### Gotcha 4: Explicit Interface Implementation as a Stub-Hiding Trick

Explicitly implementing a member to throw makes the class *look* clean while keeping the runtime bomb.

```csharp
public sealed class ReadOnlyOrderStore : IRepository<Order>   // full read-write interface
{
    public Task<Order?> GetByIdAsync(Guid id) { /* real */ }

    // ❌ Hidden from IntelliSense on the class — but any IRepository<Order>-typed
    //    reference (which is how DI hands it out!) still explodes at runtime
    Task IWriteRepository<Order>.SaveAsync(Order o)
        => throw new NotSupportedException("Read-only store");
}
```

Explicit implementation is fine for disambiguating two interfaces or hiding *redundant* members (e.g., non-generic `IEnumerable.GetEnumerator`). It is not fine as camouflage for "I can't actually fulfill this contract" — that's the ISP violation with a coat of paint. Implement `IReadRepository<Order>` only.

### Gotcha 5: Marker Interfaces Carry No Contract

```csharp
public interface IAuditable { }   // ❌ zero members — it's a type-level sticky note

if (entity is IAuditable) WriteAuditLog(entity);   // behavior driven by a tag, not a contract
```

An interface with no members can't be violated, mocked meaningfully, or partially implemented — it's metadata pretending to be a contract. Prefer an attribute (`[Auditable]`) for pure metadata, or give the interface the member it's implying (`IAuditable { string AuditKey { get; } }`) so the "marker" earns its place. (BCL exceptions like `IRequiresSessionState` exist for reflection-era reasons — don't copy them into new designs.)

### Gotcha 6: Default Interface Methods Can Quietly Re-Fatten Your Interfaces

C# 8 defaults (covered above) have a second failure mode: because adding a defaulted method breaks nobody at compile time, fat interfaces grow back *silently* — five releases later `ILogger` has 12 defaulted members and three implementors have subtly divergent behavior because they overrode different subsets. Treat every defaulted member addition with the same design scrutiny as a breaking one; the compiler has stopped guarding that door for you.

---

## Decision Guide

| Situation | Action |
|---|---|
| Implementor throws `NotImplementedException` on any method | Interface is too fat — split it |
| A method is added to an interface and 5+ classes break | Interface serves too many clients — segregate by role |
| Caller imports an interface but only calls 2 of its 8 methods | Client perspective is wrong — create a narrow interface for that caller |
| Read-only implementations of a read-write interface | Split into `IReadRepository` + `IWriteRepository` |
| Adding a new interface method forces unrelated classes to update | Existing interface has too wide a scope |
| Two callers use the same class but need different subsets of methods | Create two focused interfaces; the class implements both |
| Interface has 10+ methods | Almost certainly a fat interface — review each method's caller |
