# 03 — Polymorphism

> **Core idea:** Write code against a general type; let the runtime pick the right behaviour automatically based on the actual object.

---

## What is Polymorphism?

"Poly" = many, "morph" = form. One interface, many implementations.

Without polymorphism, every piece of code that handles "a payment" needs a `switch` or `if/else` tree: *if CreditCard do this, else if PayPal do that*. Add a new payment type and you hunt down every switch in the codebase.

With polymorphism, you call `payment.Process()` and the right behaviour fires automatically — the caller never knows or cares which concrete type it holds.

---

## Two Kinds of Polymorphism

| Kind | Also called | Resolved by | C# mechanism |
|---|---|---|---|
| **Compile-time** | Static dispatch | Compiler at build time | Method overloading |
| **Runtime** | Dynamic dispatch | CLR at execution time | `virtual` / `override` via vtable |

---

## 5 Real-World Use Cases

1. **Payment processing** — `CreditCard`, `PayPal`, `Crypto` all implement `IPaymentMethod.Process()`.
2. **Serialization** — `JsonSerializer`, `XmlSerializer`, `BinarySerializer` share `ISerializer<T>`.
3. **Logging** — `ConsoleLogger`, `FileLogger`, `SentryLogger` all implement `ILogger`.
4. **Notifications** — `EmailNotification`, `SmsNotification`, `PushNotification` share a `Notification` base.
5. **Shape rendering** — `Circle`, `Rectangle`, `Triangle` each implement `IShape.Draw()` / `Area()`.

---

## Compile-Time Polymorphism — Method Overloading

The compiler selects which version to call based on argument count and types at build time. No vtable, no runtime cost.

```csharp
public sealed class OrderCalculator
{
    // Three overloads — compiler picks the right one at build time
    public decimal Calculate(int quantity, decimal unitPrice)
        => quantity * unitPrice;

    public decimal Calculate(int quantity, decimal unitPrice, decimal discountPercent)
    {
        if (discountPercent is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(discountPercent));
        return quantity * unitPrice * (1 - discountPercent);
    }

    public decimal Calculate(int quantity, decimal unitPrice, string couponCode)
    {
        decimal discount = couponCode switch
        {
            "SAVE10" => 0.10m,
            "SAVE20" => 0.20m,
            "VIP"    => 0.30m,
            _        => 0m
        };
        return quantity * unitPrice * (1 - discount);
    }
}

var calc = new OrderCalculator();
calc.Calculate(5, 10m);             // first overload
calc.Calculate(5, 10m, 0.15m);      // second overload
calc.Calculate(5, 10m, "SAVE10");   // third overload
```

**Overloading trap:** optional parameters and explicit overloads with the same required parameters cause ambiguity and confuse readers. Prefer explicit overloads.

---

## ❌ Anti-Pattern: Switch-on-Type (Runtime Without Polymorphism)

```csharp
// Every new payment type → modify this method
// Every feature that processes payments has its own copy of this switch
public string Process(PaymentType type, decimal amount, string reference)
{
    return type switch
    {
        PaymentType.CreditCard   => ProcessCreditCard(amount, reference),
        PaymentType.PayPal       => ProcessPayPal(amount, reference),
        PaymentType.BankTransfer => ProcessBankTransfer(amount, reference),
        PaymentType.Crypto       => ProcessCrypto(amount, reference),
        _                        => throw new NotSupportedException($"Unknown: {type}")
    };
}

// And when refunds arrive... another identical switch appears.
// And when validation arrives... another.
// Open/Closed Principle is shredded.
```

---

## ✅ Pattern: Interface-Based Runtime Polymorphism

```csharp
public record PaymentRequest(decimal Amount, string Currency, string Reference);
public record PaymentResult(bool Success, string TransactionId, string Message);

public interface IPaymentMethod
{
    string ProviderName { get; }
    bool   Supports(string currency);
    PaymentResult Process(PaymentRequest request);
    PaymentResult Refund(string transactionId, decimal amount);
}

public sealed class CreditCardPayment : IPaymentMethod
{
    public string ProviderName => "Stripe";
    public bool   Supports(string currency) => currency is "USD" or "EUR" or "GBP";

    public PaymentResult Process(PaymentRequest request)
    {
        var txId = $"ch_{Guid.NewGuid():N}"[..15];
        return new PaymentResult(true, txId, $"Charged {request.Amount:C} via Stripe");
    }

    public PaymentResult Refund(string transactionId, decimal amount)
        => new(true, $"re_{transactionId}", $"Refunded {amount:C}");
}

public sealed class CryptoPayment : IPaymentMethod
{
    public string ProviderName => "Coinbase Commerce";
    public bool   Supports(string currency) => currency is "BTC" or "ETH" or "USDC";

    public PaymentResult Process(PaymentRequest request)
        => new(true, Guid.NewGuid().ToString("N"), $"Crypto charge {request.Amount} {request.Currency}");

    // ✅ Returns a failure result instead of throwing — callers don't break
    public PaymentResult Refund(string transactionId, decimal amount)
        => new(false, string.Empty, "Crypto transactions are irreversible. Contact support.");
}

// The processor never changes when new payment methods are added
public sealed class PaymentProcessor
{
    private readonly IReadOnlyList<IPaymentMethod> _methods;
    public PaymentProcessor(IEnumerable<IPaymentMethod> methods)
        => _methods = new List<IPaymentMethod>(methods);

    public PaymentResult Process(PaymentRequest request)
    {
        var method = _methods.FirstOrDefault(m => m.Supports(request.Currency))
            ?? throw new InvalidOperationException($"No provider supports '{request.Currency}'");
        return method.Process(request);  // Runtime dispatch — no switch needed
    }
}
```

---

## Interface vs Abstract Class — The Real Decision

```
Use INTERFACE when:                      Use ABSTRACT CLASS when:
  ┌─────────────────────────────────┐      ┌─────────────────────────────────────┐
  │ Unrelated types share a contract│      │ All types are genuinely related (IS-A)│
  │ A class needs multiple "roles"  │      │ Shared STATE across all subtypes     │
  │ No shared state or base logic   │      │ Algorithm skeleton (Template Method) │
  └─────────────────────────────────┘      └─────────────────────────────────────┘
```

```csharp
// ABSTRACT CLASS — all Notifications share sender + timestamp state
public abstract class Notification
{
    protected readonly string Sender;
    protected readonly DateTimeOffset CreatedAt;

    protected Notification(string sender) { Sender = sender; CreatedAt = DateTimeOffset.UtcNow; }

    // Sealed template — algorithm is fixed
    public sealed NotificationResult Send(string recipient, string subject, string body)
    {
        if (!Validate(recipient)) return NotificationResult.Failure($"Invalid: {recipient}");
        return Deliver(recipient, subject, FormatBody(body));
    }

    protected abstract bool             Validate(string recipient);
    protected abstract string           FormatBody(string raw);
    protected abstract NotificationResult Deliver(string to, string subject, string body);
}

public sealed class EmailNotification : Notification
{
    public EmailNotification(string sender) : base(sender) { }

    protected override bool Validate(string r)      => r.Contains('@');
    protected override string FormatBody(string raw) => $"<html><body>{raw}</body></html>";
    protected override NotificationResult Deliver(string to, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] From:{Sender} To:{to} Subject:{subject}");
        return NotificationResult.Ok($"Email sent to {to}");
    }
}

public sealed class SmsNotification : Notification
{
    public SmsNotification(string sender) : base(sender) { }

    protected override bool Validate(string r)       => r.StartsWith('+') && r.Length >= 10;
    protected override string FormatBody(string raw)  => raw.Length > 160 ? raw[..157] + "..." : raw;
    protected override NotificationResult Deliver(string to, string subject, string body)
    {
        Console.WriteLine($"[SMS] To:{to} '{body}'");
        return NotificationResult.Ok($"SMS sent to {to}");
    }
}
```

---

## Advanced: How vtables Work Under the Hood

Every class with virtual members has a **Virtual Method Table (vtable)** managed by the CLR.

```
Object on heap (64-bit):
┌────────────────────────┐
│  sync block index      │  8 bytes
├────────────────────────┤
│  method table pointer  │  8 bytes ← this IS the vtable pointer
├────────────────────────┤
│  field data...         │
└────────────────────────┘

vtable (method table):
┌─────────────────────┐
│  slot 0: Describe() │ → pointer to Circle.Describe()
│  slot 1: Area()     │ → pointer to Circle.Area()
│  ...                │
└─────────────────────┘
```

When you call `shape.Area()` on a `Shape` reference:
1. Load the `shape` reference (pointer to object on heap)
2. Read the method table pointer from the object header
3. Index into the method table at the slot for `Area()`
4. Call through the function pointer at that slot

This is **one extra indirection** vs a direct call — typically ~2–3 ns, negligible in most code.

**When it matters:** In tight inner loops (physics simulations, trading engines) with millions of virtual calls per second, the branch misprediction cost adds up. Solution: `sealed`.

---

## Advanced: `sealed` → JIT Devirtualization

When a class is `sealed`, the JIT knows there are no subclasses. It can devirtualize the virtual call, turning it into a direct call and enabling **inlining** — eliminating the vtable lookup entirely.

```csharp
// NOT sealed — every Area() call goes through vtable dispatch
public class Shape { public virtual double Area() => 0; }

// sealed — JIT inlines Area() at call sites that hold Circle references
public sealed class Circle : Shape
{
    private readonly double _r;
    public Circle(double r) => _r = r;
    public sealed override double Area() => Math.PI * _r * _r;
}

// In a loop computing areas of a million Circles:
// Virtual:       ~12 ns/call (vtable + branch miss)
// Devirtualized: ~2 ns/call  (inlined multiply)
// → 6× faster in that hot path
```

---

## Advanced: `new` Keyword — Hiding vs Overriding

`new` hides the base member at the static type level. It is **not polymorphic**.

```csharp
public class Shape
{
    public virtual string Describe() => "Shape";   // virtual
    public         string Label()    => "Shape";   // non-virtual
}

public class Circle : Shape
{
    public override string Describe() => "Circle"; // ✅ polymorphic
    public new     string Label()     => "Circle"; // ⚠️ hides base — NOT polymorphic
}

Shape s = new Circle();
s.Describe();  // "Circle"  — override dispatches to derived ✅
s.Label();     // "Shape"   — static type (Shape) wins for hidden member ⚠️
```

**Rule:** If you write `new` on a method that matches a base member name, you almost certainly meant `override`.

---

## Advanced: Pattern Matching — Alternative to Polymorphism for Closed Sets

When the set of types is **closed** (you own and control all of them) and the types are data-shaped, pattern matching on `record` types can be cleaner than a class hierarchy.

```csharp
// Closed set — you control all subtypes
public abstract record ShapeRecord;
public sealed record CircleRecord(double Radius)            : ShapeRecord;
public sealed record RectangleRecord(double Width, double Height) : ShapeRecord;
public sealed record TriangleRecord(double Base, double Height)   : ShapeRecord;

// No vtable — compile-time type checking via exhaustive switch
public static double Area(ShapeRecord s) => s switch
{
    CircleRecord c    => Math.PI * c.Radius * c.Radius,
    RectangleRecord r => r.Width * r.Height,
    TriangleRecord t  => 0.5 * t.Base * t.Height,
    _                 => throw new ArgumentException($"Unknown: {s.GetType().Name}")
};
```

**Use polymorphism (open):** when external callers can add new types.
**Use pattern matching (closed):** when you own all the types and they are primarily data.

---

## Advanced: C# 8 Default Interface Methods

Default interface methods let you add new members to an interface without breaking all existing implementors. Use them for **evolving public API libraries** — not as a substitute for abstract classes.

```csharp
public interface ILogger
{
    void Log(string level, string message);

    // Default implementations — existing loggers don't need to override these
    void LogInfo(string msg)    => Log("INFO",    msg);
    void LogWarning(string msg) => Log("WARNING", msg);
    void LogError(string msg)   => Log("ERROR",   msg);
}

public sealed class ConsoleLogger : ILogger
{
    public void Log(string level, string message)
        => Console.WriteLine($"[{level}] {message}");
    // Gets LogInfo, LogWarning, LogError for free from the interface default
}
```

**Danger:** if two interfaces both provide a default for the same method name and a class implements both, you get a compile error — you must override to disambiguate. Don't use defaults to replace abstract class shared logic; use them only for backward-compatible evolution.

---

## Decision Guide

| Situation | Tool |
|---|---|
| Unrelated types share a capability | Interface |
| Related types share state + algorithm skeleton | Abstract class + Template Method |
| Adding new types must not touch existing code | Interface polymorphism |
| Set of types is closed and data-shaped | `record` + pattern matching |
| Single class plays multiple roles | Multiple interface implementation |
| Performance-critical loop with polymorphic calls | `sealed` for devirtualization |
| Method with same name as base but different semantics | `new` (rare — be explicit and intentional) |
| Evolving a public interface without breaking consumers | C# 8 default interface methods |
