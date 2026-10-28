# 02 — Open/Closed Principle (OCP)

> **Core idea:** Software entities should be **open for extension** but **closed for modification**. Add new behaviour by adding new code — not by editing working code.

---

## What is OCP?

When requirements change (and they always do), the ideal response is to add new code rather than change old, tested, working code. Changing existing code risks regressions and breaks contracts that other code depends on.

OCP is achieved by identifying the **axes of variation** — the things that genuinely change — and creating extension points for them.

**Key insight:** OCP does NOT mean "abstract everything from day one." That leads to premature abstraction — one of the most common architecture mistakes. The right move is to *let code become stable first*, then abstract the variation when the second or third case appears.

---

## Key Concepts

| Concept | Meaning |
|---|---|
| **Open for extension** | You can add new behaviour (new types, new strategies) |
| **Closed for modification** | Adding new behaviour doesn't require editing existing, tested code |
| **Axis of variation** | The dimension along which the system genuinely changes (payment types, export formats, discount rules) |
| **Strategy Pattern** | The primary OCP tool — extract the varying behaviour into a pluggable component |
| **Premature abstraction** | Abstracting before the second case exists — creates complexity before it's needed |

---

## 5 Real-World Use Cases

1. **Payment methods** — adding `CryptoPayment` doesn't touch `PaymentProcessor` or `CreditCardPayment`.
2. **Discount calculation** — adding a `LoyaltyDiscount` strategy doesn't modify `CheckoutService`.
3. **Export formats** — adding `ExcelExporter` doesn't touch `CsvExporter` or `ReportService`.
4. **Validation rules** — adding a `ProfanityFilter` validator doesn't modify `UserRegistrationService`.
5. **Notification channels** — adding `SlackNotifier` doesn't touch `EmailNotifier` or `NotificationDispatcher`.

---

## ❌ Anti-Pattern: Switch-on-Type / If-Chain

Every new discount type requires modifying `DiscountCalculator`. There are five such methods across the codebase, and each must be updated in sync.

```csharp
public enum CustomerType { Regular, Premium, Employee, VIP, Wholesale }

public class DiscountCalculator_Closed
{
    // ❌ Every new CustomerType requires modifying this method
    // ❌ And the Refund() method below, and the Invoice() method, and...
    public decimal Calculate(CustomerType type, decimal orderTotal)
    {
        return type switch
        {
            CustomerType.Regular   => orderTotal * 0.00m,
            CustomerType.Premium   => orderTotal * 0.10m,
            CustomerType.Employee  => orderTotal * 0.20m,
            CustomerType.VIP       => orderTotal * 0.15m,
            CustomerType.Wholesale => orderTotal > 1000 ? orderTotal * 0.25m : orderTotal * 0.15m,
            _                      => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    // ❌ Bonus calculation — another switch that must be kept in sync
    public decimal CalculateBonus(CustomerType type, decimal orderTotal)
    {
        return type switch
        {
            CustomerType.Regular   => 0,
            CustomerType.Premium   => orderTotal * 0.02m,
            CustomerType.Employee  => 0,
            CustomerType.VIP       => orderTotal * 0.05m,
            CustomerType.Wholesale => orderTotal * 0.01m,
            _                      => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
// Adding "Corporate" customer: find every switch in every file.
// Miss one → silent wrong calculation in production.
```

---

## ✅ Pattern: Strategy + Open Extension Points

Adding a new discount type means adding one new class. Zero existing files change.

```csharp
public record DiscountResult(decimal DiscountAmount, decimal FinalPrice, string AppliedRule);

public interface IDiscountStrategy
{
    string Name { get; }
    bool   AppliesTo(CustomerContext context);
    DiscountResult Apply(decimal orderTotal);
}

public sealed class CustomerContext
{
    public string  CustomerId   { get; init; } = string.Empty;
    public string  CustomerType { get; init; } = string.Empty;
    public decimal OrderTotal   { get; init; }
    public int     LoyaltyYears { get; init; }
    public bool    IsEmployee   { get; init; }
}

// Each strategy is a self-contained, independently testable unit

public sealed class NoDiscount : IDiscountStrategy
{
    public string Name => "No Discount";
    public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Regular";
    public DiscountResult Apply(decimal total) => new(0m, total, Name);
}

public sealed class PremiumDiscount : IDiscountStrategy
{
    private const decimal Rate = 0.10m;
    public string Name => "Premium 10%";
    public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Premium";
    public DiscountResult Apply(decimal total)
    {
        var discount = total * Rate;
        return new(discount, total - discount, Name);
    }
}

public sealed class EmployeeDiscount : IDiscountStrategy
{
    private const decimal Rate = 0.20m;
    public string Name => "Employee 20%";
    public bool AppliesTo(CustomerContext ctx) => ctx.IsEmployee;
    public DiscountResult Apply(decimal total)
    {
        var discount = total * Rate;
        return new(discount, total - discount, Name);
    }
}

public sealed class WholesaleDiscount : IDiscountStrategy
{
    public string Name => "Wholesale";
    public bool AppliesTo(CustomerContext ctx)
        => ctx.CustomerType == "Wholesale";

    public DiscountResult Apply(decimal total)
    {
        var rate     = total > 1000 ? 0.25m : 0.15m;
        var discount = total * rate;
        return new(discount, total - discount, $"{Name} ({rate:P0})");
    }
}

// ✅ Adding CorporateDiscount: new class, zero changes to anything above
public sealed class CorporateDiscount : IDiscountStrategy
{
    private readonly decimal _negotiatedRate;
    public CorporateDiscount(decimal negotiatedRate) => _negotiatedRate = negotiatedRate;
    public string Name => $"Corporate {_negotiatedRate:P0}";
    public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Corporate";
    public DiscountResult Apply(decimal total)
    {
        var discount = total * _negotiatedRate;
        return new(discount, total - discount, Name);
    }
}

// DiscountCalculator is closed for modification — it never changes
public sealed class DiscountCalculator
{
    private readonly IReadOnlyList<IDiscountStrategy> _strategies;

    public DiscountCalculator(IEnumerable<IDiscountStrategy> strategies)
        => _strategies = new List<IDiscountStrategy>(strategies);

    public DiscountResult Calculate(CustomerContext context)
    {
        var strategy = _strategies.FirstOrDefault(s => s.AppliesTo(context))
            ?? new NoDiscount();
        return strategy.Apply(context.OrderTotal);
    }
}
```

---

## Intermediate: Validation Pipeline — OCP via Chain of Responsibility

Each validation rule is a separate class. New rules are added to the pipeline registration, not to an existing validator.

```csharp
public record ValidationContext(string Email, string Password, string Name);
public record RuleResult(bool Passed, string? FailureMessage);

public interface IValidationRule
{
    RuleResult Evaluate(ValidationContext ctx);
}

public sealed class EmailFormatRule : IValidationRule
{
    public RuleResult Evaluate(ValidationContext ctx)
        => ctx.Email.Contains('@') && ctx.Email.Contains('.')
            ? new(true, null)
            : new(false, "Invalid email format");
}

public sealed class PasswordLengthRule : IValidationRule
{
    private readonly int _min;
    public PasswordLengthRule(int min = 8) => _min = min;
    public RuleResult Evaluate(ValidationContext ctx)
        => ctx.Password.Length >= _min
            ? new(true, null)
            : new(false, $"Password must be at least {_min} characters");
}

public sealed class PasswordComplexityRule : IValidationRule
{
    public RuleResult Evaluate(ValidationContext ctx)
    {
        if (!ctx.Password.Any(char.IsDigit))  return new(false, "Password must contain a digit");
        if (!ctx.Password.Any(char.IsUpper))  return new(false, "Password must contain an uppercase letter");
        return new(true, null);
    }
}

// ✅ Adding ProfanityFilter: new class, new registration — nothing existing changes
public sealed class NameProfanityRule : IValidationRule
{
    private static readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "root", "system" };

    public RuleResult Evaluate(ValidationContext ctx)
        => _blocked.Contains(ctx.Name)
            ? new(false, $"Name '{ctx.Name}' is not allowed")
            : new(true, null);
}

// ValidationPipeline is closed — never modified when rules change
public sealed class ValidationPipeline
{
    private readonly IReadOnlyList<IValidationRule> _rules;
    public ValidationPipeline(IEnumerable<IValidationRule> rules)
        => _rules = new List<IValidationRule>(rules);

    public IReadOnlyList<string> Validate(ValidationContext ctx)
        => _rules
            .Select(r => r.Evaluate(ctx))
            .Where(r => !r.Passed)
            .Select(r => r.FailureMessage!)
            .ToList();
}
```

---

## Intermediate: Export Formats — OCP with Factory

```csharp
public interface IReportExporter
{
    string Format { get; }   // "csv", "json", "pdf"
    byte[] Export(IEnumerable<ReportRow> rows);
}

public sealed record ReportRow(string Name, decimal Amount, DateTimeOffset Date);

public sealed class CsvExporter : IReportExporter
{
    public string Format => "csv";
    public byte[] Export(IEnumerable<ReportRow> rows)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("Name,Amount,Date");
        foreach (var r in rows)
            lines.AppendLine($"{r.Name},{r.Amount:F2},{r.Date:yyyy-MM-dd}");
        return System.Text.Encoding.UTF8.GetBytes(lines.ToString());
    }
}

public sealed class JsonExporter : IReportExporter
{
    public string Format => "json";
    public byte[] Export(IEnumerable<ReportRow> rows)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(rows);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
}

// Factory that is open for extension via registration — closed for modification
public sealed class ExporterFactory
{
    private readonly Dictionary<string, IReportExporter> _exporters;

    public ExporterFactory(IEnumerable<IReportExporter> exporters)
        => _exporters = exporters.ToDictionary(e => e.Format, StringComparer.OrdinalIgnoreCase);

    public IReportExporter GetExporter(string format)
        => _exporters.TryGetValue(format, out var exporter)
            ? exporter
            : throw new NotSupportedException($"Format '{format}' is not supported");
}
// ✅ Register ExcelExporter: zero changes to CsvExporter, JsonExporter, or ExporterFactory
```

---

## Advanced: The Premature Abstraction Trap

OCP applied too early is as harmful as not applying it at all.

```csharp
// Requirement: "We generate PDF invoices."
// ❌ Over-engineered from day one — there is NO second format yet
public interface IInvoiceFormatter    { byte[] Format(Invoice invoice); }
public interface IInvoiceRenderer     { void Render(byte[] data); }
public interface IInvoicePipeline     { void Execute(Invoice invoice); }
public abstract class InvoiceProcessorBase { /* ... */ }
// 4 abstractions for 1 concrete requirement = 4× the code to understand and maintain

// ✅ Start with the simplest thing that works:
public sealed class PdfInvoiceGenerator
{
    public byte[] Generate(Invoice invoice) { /* PDF logic */ return Array.Empty<byte>(); }
}

// When the SECOND format (Excel) appears → THEN extract IInvoiceGenerator.
// The "Rule of Three": abstract when you see the third case, not the first.
```

**The cost of premature abstraction:**
- Every new developer must understand the abstraction before writing a line.
- Abstractions constrain future designs — the wrong abstraction is worse than none.
- You pay the complexity cost upfront, before you know if the variation is real.

**The "Rule of Three":** Once you've implemented the same pattern three times in slightly different ways, you have enough information to know what the abstraction actually is. Abstracting from one example is guessing.

---

## Advanced: Plugin Pattern via Reflection

For true runtime extensibility (plugin architectures), OCP can be implemented via assembly scanning. The core system never needs to know about new plugins.

```csharp
// Core system — never changes
public interface IPlugin
{
    string Name { get; }
    void Execute(PluginContext context);
}

public sealed class PluginContext
{
    public string  Input  { get; init; } = string.Empty;
    public Dictionary<string, object> Data { get; } = new();
}

// Plugin loader — discovers implementations via reflection (assembly scanning)
public sealed class PluginLoader
{
    public IReadOnlyList<IPlugin> LoadFromAssembly(System.Reflection.Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t)
                     && !t.IsInterface
                     && !t.IsAbstract
                     && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IPlugin)Activator.CreateInstance(t)!)
            .ToList();
    }
}

// In production: scan plugin directories for .dll files at startup.
// Adding a new plugin = drop a .dll in the plugins/ folder.
// Zero changes to the host application.
```

---

## More Real-World Examples

### Example: Shipping Cost per Carrier

A new carrier is a new class + one DI registration — the checkout flow never changes.

```csharp
public record Shipment(decimal WeightKg, string DestinationCountry, bool Express);

public interface IShippingRateProvider
{
    string Carrier { get; }
    bool   CanShip(Shipment shipment);
    decimal Quote(Shipment shipment);
}

public sealed class FedExRates : IShippingRateProvider
{
    public string Carrier => "FedEx";
    public bool CanShip(Shipment s)  => s.WeightKg <= 68;
    public decimal Quote(Shipment s) => 12m + s.WeightKg * 1.8m + (s.Express ? 25m : 0m);
}

public sealed class DhlRates : IShippingRateProvider
{
    public string Carrier => "DHL";
    public bool CanShip(Shipment s)  => true;   // freight-capable
    public decimal Quote(Shipment s) => 15m + s.WeightKg * 1.5m + (s.Express ? 30m : 0m);
}

// Closed for modification — compares whatever providers are registered
public sealed class ShippingQuoteService
{
    private readonly IEnumerable<IShippingRateProvider> _providers;
    public ShippingQuoteService(IEnumerable<IShippingRateProvider> providers) => _providers = providers;

    public (string Carrier, decimal Price)? GetCheapest(Shipment shipment)
        => _providers
            .Where(p => p.CanShip(shipment))
            .Select(p => (p.Carrier, Price: p.Quote(shipment)))
            .OrderBy(q => q.Price)
            .Cast<(string, decimal)?>()
            .FirstOrDefault();
}
// services.AddSingleton<IShippingRateProvider, FedExRates>();
// services.AddSingleton<IShippingRateProvider, DhlRates>();
// Adding UPS next sprint: one class, one registration line. ShippingQuoteService untouched.
```

### Example: ASP.NET Core Middleware — OCP Built into the Framework

The framework's request pipeline is the OCP pattern you already use daily: the host is closed, the pipeline is open.

```csharp
// The hosting engine never changes. You EXTEND the pipeline by adding components:
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<TenantResolutionMiddleware>();   // your custom extension

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Items["TenantId"] = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
        await _next(ctx);   // pass control down the chain
    }
}
// Same shape appears in: EF Core interceptors, HttpClient DelegatingHandlers,
// MediatR pipeline behaviors. Recognize the pattern — you extend, never modify the host.
```

### Example: Country-Specific Tax Rules

```csharp
public interface ITaxRule
{
    bool Matches(string countryCode);
    decimal Calculate(decimal net);
}

public sealed class UkVat  : ITaxRule { public bool Matches(string c) => c == "GB"; public decimal Calculate(decimal n) => n * 0.20m; }
public sealed class DeVat  : ITaxRule { public bool Matches(string c) => c == "DE"; public decimal Calculate(decimal n) => n * 0.19m; }
public sealed class UsTax  : ITaxRule { public bool Matches(string c) => c == "US"; public decimal Calculate(decimal n) => n * 0.07m; }

public sealed class TaxCalculator
{
    private readonly IReadOnlyList<ITaxRule> _rules;
    public TaxCalculator(IEnumerable<ITaxRule> rules) => _rules = rules.ToList();

    public decimal Calculate(string countryCode, decimal net)
    {
        var rule = _rules.FirstOrDefault(r => r.Matches(countryCode))
            ?? throw new NotSupportedException($"No tax rule for '{countryCode}'");
        return rule.Calculate(net);
    }
}
// New market launch (France)? Add FrVat. Legal changes UK rate? Modify ONLY UkVat.
// The blast radius of any change is exactly one class.
```

---

## Gotchas

### Gotcha 1: Not Every `switch` Violates OCP — Closed Sets Are Fine

OCP applies to **axes of genuine variation**. Some sets are closed by nature and will never grow — for those, a switch is simpler AND safer than a strategy graph.

```csharp
// ✅ DayOfWeek has had 7 values for a few thousand years. A switch is correct here.
public static bool IsWeekend(DayOfWeek day) => day is DayOfWeek.Saturday or DayOfWeek.Sunday;

// ✅ Exhaustive switch on your own enum + no default case
//    → the compiler warns when a value is added. That's a FEATURE:
//    you WANT a compile error at every touchpoint when the set changes rarely.
public static string ToBadgeColor(OrderStatus status) => status switch
{
    OrderStatus.Pending   => "yellow",
    OrderStatus.Shipped   => "blue",
    OrderStatus.Delivered => "green",
    OrderStatus.Cancelled => "red",
    // no default — CS8509 warning fires if OrderStatus grows. Deliberate.
};
```

**The test:** who adds new cases, and how often? Business adds payment providers monthly → strategy. The domain has exactly 4 statuses that changed once in 5 years → switch. OCP is about managing *probable* change, not *conceivable* change.

### Gotcha 2: Strategy Selection Order Is a Silent Bug Factory

`FirstOrDefault(s => s.AppliesTo(ctx))` means: if two strategies match, **registration order decides** — silently.

```csharp
// EmployeeDiscount.AppliesTo → ctx.IsEmployee
// PremiumDiscount.AppliesTo  → ctx.CustomerType == "Premium"
// An employee who is ALSO a premium customer matches BOTH.
// Whoever registered first wins. Nobody decided that. Nobody tested it.
```

**Fixes:**

```csharp
// Option A: explicit priority on the contract
public interface IDiscountStrategy { int Priority { get; } /* ... */ }
var strategy = _strategies.Where(s => s.AppliesTo(ctx)).OrderByDescending(s => s.Priority).First();

// Option B: fail loudly on ambiguity in Development
var matches = _strategies.Where(s => s.AppliesTo(ctx)).ToList();
if (matches.Count > 1)
    throw new InvalidOperationException(
        $"Ambiguous strategies: {string.Join(", ", matches.Select(m => m.Name))}");

// Option C (best when possible): pick the highest-VALUE discount, not the first match
var best = _strategies.Where(s => s.AppliesTo(ctx))
                      .Select(s => s.Apply(ctx.OrderTotal))
                      .OrderByDescending(r => r.DiscountAmount)
                      .FirstOrDefault();
```

### Gotcha 3: OCP via Inheritance Is the Fragile Version

`protected virtual` hook methods technically give extension points, but they couple every subclass to the base class's internals — the Fragile Base Class problem.

```csharp
// ❌ Extension via inheritance — subclasses depend on base implementation order
public class OrderProcessor
{
    public void Process(Order o)
    {
        Validate(o);
        ApplyDiscounts(o);   // subclass overrides this...
        Persist(o);
    }
    protected virtual void ApplyDiscounts(Order o) { /* base logic */ }
    protected virtual void Persist(Order o) { /* ... */ }
}
// Base team reorders Process() or makes Persist() call ApplyDiscounts() internally
// → every subclass breaks, with no compile error. Behavior change at a distance.
```

**Fix:** prefer composition — inject the varying step (`IDiscountStrategy`) instead of overriding it. Inheritance shares the whole implementation; composition shares only the contract.

### Gotcha 4: "Closed for Modification" Does NOT Mean "Never Edit the File"

OCP is about *adding behavior*, not immutability. You still modify existing code to:

- **fix bugs** (a wrong discount rate is edited in place, not "extended"),
- **refactor** (renaming, restructuring with tests green),
- **change a requirement inside one strategy** (UK VAT rate change edits `UkVat` — that's the design working as intended: blast radius of one class).

The violation is when adding *new* behavior (new carrier, new format) forces edits to *existing, unrelated* code. Don't let anyone weaponize OCP to block a bug fix "because the class is closed."

### Gotcha 5: Config-Driven "Extensibility" Just Moves the Problem

Tempting over-correction: "let's make the rules data, then we never deploy!" — a JSON/DB rules engine.

```json
{ "rule": "discount", "when": "customer.type == 'Premium' && order.total > 100", "then": "total * 0.9" }
```

What you actually built: an untyped, untestable, un-debuggable programming language inside your config. No compile checks, no unit tests, no IDE support, no code review diff semantics — and the *interpreter* for it becomes the new god class you must modify for every new operator. Reach for rules-as-data only when non-developers genuinely must author rules at runtime, and budget for the tooling (validation, dry-run, versioning) that code would have given you for free.

### Gotcha 6: The Wrong Abstraction Is More Expensive Than Duplication

If you abstract after one example (violating the Rule of Three), the extension point often models the wrong axis. Then every new case needs a parameter added to the interface — and **changing a published interface modifies every implementor**, the exact thing OCP promised to avoid.

```csharp
// v1: abstracted too early, from one case
public interface IExporter { byte[] Export(ReportRow[] rows); }
// v2: PDF needs page size...        Export(rows, PageSize size)          → all implementors edited
// v3: Excel needs sheet names...    Export(rows, PageSize, string sheet) → all implementors edited again
// The "extension point" causes MORE modification than the switch it replaced.
```

**Fix when it happens:** pass a parameter object (`ExportOptions`) so the contract can grow without signature changes — or admit the abstraction was premature, inline it, and wait for the third case. (Sandi Metz: "prefer duplication over the wrong abstraction.")

---

## Decision Guide

| Situation | Action |
|---|---|
| Same switch/if-chain appears 2+ times across codebase | Extract to Strategy pattern |
| Adding a feature requires modifying tested code | Identify the axis of variation, create an extension point |
| Only one implementation exists | Don't abstract yet — YAGNI |
| New type needs to be added by a third party | Use plugin / interface registration |
| A colleague says "but what if we need X later?" | Ask "when?" — abstract when the need is real |
| Chain of independent processing steps | Chain of Responsibility |
| Creating objects based on a type string/enum | Factory pattern — open for registration |
