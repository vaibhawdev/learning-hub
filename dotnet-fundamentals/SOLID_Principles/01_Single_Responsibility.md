# 01 — Single Responsibility Principle (SRP)

> **Core idea:** A class should have only **one reason to change** — and that reason is tied to a specific *actor* whose needs drive it.

---

## What is SRP?

Robert C. Martin's original definition is often misquoted. It does NOT say "a class should do only one thing." It says:

> *"A module should be responsible to one, and only one, actor."*

An **actor** is a group of stakeholders whose requests drive changes to the code. Your `UserService` might be changed by:

- The **Security team** (change password hashing algorithm)
- The **Marketing team** (change welcome email copy)
- The **DBA** (change the SQL query for performance)
- The **Finance team** (change invoice generation format)

If all four of those actors own the same class, that class has four reasons to change. A change requested by the DBA (a query refactor) risks breaking the welcome email logic accidentally — because they live in the same file.

**SRP says: separate code that different actors own.**

---

## Key Concepts

| Concept | Meaning |
|---|---|
| **Actor** | A group of stakeholders who own a piece of functionality |
| **Reason to change** | A request from an actor that requires modifying code |
| **God Class** | A class that serves multiple actors — the #1 SRP violation |
| **Mixed concerns** | Business logic tangled with persistence, logging, or email |
| **Cohesion** | Related things together; unrelated things apart |

---

## 5 Real-World Use Cases

1. **UserService God Class** → Split into `UserRepository` (DBA owns), `EmailService` (Marketing owns), `AuthService` (Security owns).
2. **Invoice class** → Split into `Invoice` (domain), `InvoiceCalculator` (Finance), `InvoicePdfGenerator` (Operations), `InvoiceRepository` (DBA).
3. **OrderProcessor** → Split into `OrderValidator`, `OrderPricingService`, `OrderRepository`, `OrderNotificationService`.
4. **Employee class** → Split into `Employee` (HR domain), `PayrollCalculator` (Finance), `TimesheetLogger` (Ops), `PerformanceReporter` (Management).
5. **Product class** → Split into `Product` (catalog), `PricingEngine` (Finance), `StockService` (Warehouse), `SearchIndexer` (Engineering).

---

## ❌ Anti-Pattern: God Class / God Service

One class serves every actor. A change from any one of them touches everyone else's code.

```csharp
// ❌ Who owns this class?
// Security team:  → changes HashPassword, ValidateToken
// Marketing team: → changes SendWelcomeEmail, SendPasswordResetEmail
// DBA:            → changes GetUserById, SaveUser
// Finance team:   → changes GenerateInvoice

public class UserService_God
{
    private readonly string _connectionString;
    private readonly string _smtpServer;

    public UserService_God(string connectionString, string smtpServer)
    {
        _connectionString = connectionString;
        _smtpServer       = smtpServer;
    }

    // ── Security team's domain ──────────────────────────────────────────────
    public string HashPassword(string raw)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    public bool ValidatePassword(string raw, string hash) => HashPassword(raw) == hash;

    // ── DBA's domain ────────────────────────────────────────────────────────
    public User GetUserById(int id)
    {
        // raw ADO.NET — DBA wants to swap this for Dapper
        // But they have to understand the email code first. Risky.
        using var conn = new System.Data.SqlClient.SqlConnection(_connectionString);
        // ... SQL here ...
        return new User();
    }

    public void SaveUser(User user)
    {
        using var conn = new System.Data.SqlClient.SqlConnection(_connectionString);
        // ... INSERT/UPDATE SQL here ...
    }

    // ── Marketing team's domain ─────────────────────────────────────────────
    public void SendWelcomeEmail(User user)
    {
        // Marketing changes the email template quarterly.
        // Every time, they risk touching the SQL code by mistake.
        var client = new System.Net.Mail.SmtpClient(_smtpServer);
        var msg    = new System.Net.Mail.MailMessage(
            "noreply@co.com", user.Email,
            "Welcome!", $"Hi {user.Name}, welcome aboard!");
        client.Send(msg);
    }

    public void SendPasswordResetEmail(User user, string token)
    {
        var client = new System.Net.Mail.SmtpClient(_smtpServer);
        var msg    = new System.Net.Mail.MailMessage(
            "noreply@co.com", user.Email,
            "Reset your password", $"Token: {token}");
        client.Send(msg);
    }

    // ── Finance team's domain ────────────────────────────────────────────────
    public byte[] GenerateInvoicePdf(Order order)
    {
        // Finance team needs a new line item format.
        // This method is 200 lines long and lives next to HashPassword().
        throw new NotImplementedException("PDF generation here...");
    }

    // ── Fake models to make the above compile ─────────────────────────────
    public class User  { public string Name { get; set; } = ""; public string Email { get; set; } = ""; }
    public class Order { public int Id { get; set; } }
}
```

**The failure mode:** Marketing deploys a welcome email change. The DBA's query refactor (merged at the same time) causes a merge conflict in the middle of the email method. Both break. Nobody knows who broke what.

---

## ✅ Pattern: Focused, Actor-Aligned Classes

Each class has exactly one reason to change because exactly one actor owns it.

```csharp
// ── Security team owns this ─────────────────────────────────────────────────
public interface IPasswordHasher
{
    string Hash(string raw);
    bool   Verify(string raw, string hash);
}

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;

    public string Hash(string raw)
    {
        var salt  = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash  = System.Security.Cryptography.Rfc2898DeriveBytes
            .Pbkdf2(raw, salt, Iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string raw, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual   = System.Security.Cryptography.Rfc2898DeriveBytes
            .Pbkdf2(raw, salt, Iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return actual.SequenceEqual(expected);
    }
}

// ── DBA owns this ────────────────────────────────────────────────────────────
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task        SaveAsync(User user, CancellationToken ct = default);
}

// SqlUserRepository : IUserRepository — lives in the Infrastructure project
// The DBA can rewrite the SQL inside without touching anything else.

// ── Marketing team owns this ─────────────────────────────────────────────────
public interface IUserEmailService
{
    Task SendWelcomeAsync(User user, CancellationToken ct = default);
    Task SendPasswordResetAsync(User user, string token, CancellationToken ct = default);
}

// ── Finance team owns this ───────────────────────────────────────────────────
public interface IInvoiceGenerator
{
    Task<byte[]> GeneratePdfAsync(Order order, CancellationToken ct = default);
}

// ── Application orchestrator — thin, no business logic of its own ────────────
public sealed class UserRegistrationService
{
    private readonly IUserRepository  _users;
    private readonly IPasswordHasher  _hasher;
    private readonly IUserEmailService _email;

    public UserRegistrationService(
        IUserRepository users, IPasswordHasher hasher, IUserEmailService email)
    {
        _users  = users  ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _email  = email  ?? throw new ArgumentNullException(nameof(email));
    }

    public async Task<RegistrationResult> RegisterAsync(
        string name, string email, string password, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(email, ct);
        if (existing is not null)
            return RegistrationResult.Failure("Email already registered");

        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = name,
            Email        = email,
            PasswordHash = _hasher.Hash(password),
            CreatedAt    = DateTimeOffset.UtcNow
        };

        await _users.SaveAsync(user, ct);
        await _email.SendWelcomeAsync(user, ct);

        return RegistrationResult.Ok(user.Id);
    }
}

public record RegistrationResult(bool Success, Guid? UserId, string? Error)
{
    public static RegistrationResult Ok(Guid id)      => new(true, id, null);
    public static RegistrationResult Failure(string e) => new(false, null, e);
}

public sealed class User
{
    public Guid            Id           { get; set; }
    public string          Name         { get; set; } = string.Empty;
    public string          Email        { get; set; } = string.Empty;
    public string          PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset  CreatedAt    { get; set; }
}

public sealed class Order
{
    public Guid    Id         { get; set; }
    public string  CustomerId { get; set; } = string.Empty;
    public decimal Total      { get; set; }
}
```

---

## Intermediate: The Infrastructure Mixing Anti-Pattern

One of the most common SRP violations in .NET is mixing **EF Core concerns** directly into domain entities or service classes.

```csharp
// ❌ Domain entity polluted with EF Core tracking attributes
// The DBA/infrastructure concern bleeds into the Finance team's domain object
[System.ComponentModel.DataAnnotations.Schema.Table("Orders")]
public class Order_Polluted
{
    [System.ComponentModel.DataAnnotations.Key]
    public int Id { get; set; }

    // ❌ Navigation property — EF Core concern in a domain object
    public virtual ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

    // ❌ Business logic AND audit logging in the same class
    public void AddLine(OrderLine line)
    {
        Lines.Add(line);
        Console.WriteLine($"[AUDIT] Line added to order {Id} at {DateTime.UtcNow}");
        // Now Marketing needs to change the audit format.
        // Finance objects — they didn't ask for logging in their domain model.
    }
}

public class OrderLine { public int Id { get; set; } }
```

```csharp
// ✅ Clean domain entity — no EF Core attributes, no logging
public sealed class Order_Clean
{
    private readonly List<OrderLine_Clean> _lines = new();

    public Guid   Id       { get; } = Guid.NewGuid();
    public string CustomerId { get; }
    public IReadOnlyList<OrderLine_Clean> Lines => _lines;
    public decimal Total => _lines.Sum(l => l.Subtotal);

    public Order_Clean(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("CustomerId required");
        CustomerId = customerId;
    }

    public void AddLine(OrderLine_Clean line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
    }
}

public sealed class OrderLine_Clean
{
    public string  ProductName { get; }
    public int     Quantity    { get; }
    public decimal UnitPrice   { get; }
    public decimal Subtotal    => Quantity * UnitPrice;

    public OrderLine_Clean(string name, int qty, decimal price)
    {
        ProductName = name;
        Quantity    = qty > 0 ? qty : throw new ArgumentException("Qty must be positive");
        UnitPrice   = price >= 0 ? price : throw new ArgumentException("Price cannot be negative");
    }
}

// EF Core config lives in a separate EntityTypeConfiguration class — infrastructure concern only
// Audit logging lives in a separate audit interceptor / domain event handler
```

---

## Advanced: SRP at the Module / Project Level

SRP applies beyond classes — it applies to **projects and assemblies** too.

```
Solution layout that respects SRP at every level:

MyApp.Domain/           ← Finance + Security + HR teams' pure domain objects
  Entities/
  ValueObjects/
  Events/

MyApp.Application/      ← Orchestration layer — thin use cases
  Commands/
  Queries/
  Services/

MyApp.Infrastructure/   ← DBA owns this — SQL, EF, Redis, SMTP
  Persistence/
  Email/
  Caching/

MyApp.Api/              ← Engineering owns this — HTTP, auth middleware, JSON
  Controllers/
  Middleware/
```

When a DBA wants to tune a query, they work in `Infrastructure/Persistence`. They never open `Domain/`. When Marketing changes an email template, they work in `Infrastructure/Email`. They never open `Application/`. This is SRP at architectural scale.

---

## Advanced: Common Misapplication

**SRP does NOT mean one method per class.** Taken too far, you get:

```csharp
// ❌ Over-application of SRP — nano-classes with no cohesion
public class UserNameValidator     { public bool Validate(string name) => !string.IsNullOrWhiteSpace(name); }
public class UserEmailValidator    { public bool Validate(string email) => email.Contains('@'); }
public class UserPasswordValidator { public bool Validate(string pw)   => pw.Length >= 8; }
// Now you need a UserValidatorOrchestrator, a UserValidatorFactory, a UserValidatorRegistry...
// You've replaced one God Class with a God Graph.
```

The correct granularity:

```csharp
// ✅ Cohesive validator — all validation rules are one actor's concern (QA/Security)
public sealed class UserRegistrationValidator
{
    public ValidationResult Validate(string name, string email, string password)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))    errors.Add("Name is required");
        if (!email.Contains('@'))               errors.Add("Invalid email address");
        if (password.Length < 8)               errors.Add("Password must be at least 8 characters");
        if (!password.Any(char.IsDigit))       errors.Add("Password must contain a digit");
        if (!password.Any(char.IsUpper))       errors.Add("Password must contain an uppercase letter");
        return errors.Count == 0
            ? ValidationResult.Ok()
            : ValidationResult.Failure(errors);
    }
}

public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok()                       => new(true,  Array.Empty<string>());
    public static ValidationResult Failure(List<string> errs) => new(false, errs);
}
```

---

## More Real-World Examples

### Example: Fat Controller → Thin Controller

The most common SRP violation in ASP.NET Core codebases: the controller does validation, mapping, business logic, and persistence itself.

```csharp
// ❌ The controller is the whole application
[ApiController]
[Route("api/orders")]
public class OrdersController_Fat : ControllerBase
{
    private readonly AppDbContext _db;   // persistence INSIDE the controller

    public OrdersController_Fat(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderDto dto)
    {
        // validation logic
        if (string.IsNullOrWhiteSpace(dto.CustomerId)) return BadRequest("CustomerId required");
        if (dto.Items.Count == 0)                      return BadRequest("Order is empty");

        // pricing logic
        var total = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (total > 10_000) total *= 0.95m;   // bulk discount rule — buried in HTTP layer!

        // persistence logic
        var order = new OrderEntity { CustomerId = dto.CustomerId, Total = total };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // notification logic
        Console.WriteLine($"[EMAIL] Order {order.Id} confirmed");

        return Ok(order.Id);
    }
}
// Four actors own this one action method:
// QA (validation), Finance (discount rule), DBA (persistence), Marketing (email).
```

```csharp
// ✅ Controller = HTTP translation only. One actor: the API/Engineering team.
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderPlacementService _orders;
    public OrdersController(IOrderPlacementService orders) => _orders = orders;

    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderDto dto, CancellationToken ct)
    {
        var result = await _orders.PlaceAsync(dto.ToCommand(), ct);
        return result.Success ? Ok(result.OrderId) : BadRequest(result.Error);
    }
    // Validation → FluentValidation/validator class (QA owns)
    // Discount   → PricingService (Finance owns)
    // Persistence → IOrderRepository (DBA owns)
    // Email      → INotificationService (Marketing owns)
}
```

### Example: One Model Reused Across Three Layers

A single `Product` class used as EF entity **and** API response **and** domain model means three actors share one file — a subtle but very common SRP violation.

```csharp
// ❌ One class, three masters
public class Product
{
    [Key] public int Id { get; set; }                          // DBA's concern (EF)
    [JsonPropertyName("displayName")]                          // API team's concern (serialization)
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal ApplySeasonalDiscount() => Price * 0.9m;    // Finance's concern (domain rule)
}
// The API team renames a JSON field → risk of breaking EF mapping.
// The DBA adds a shadow FK property → it leaks into the public API response.
```

```csharp
// ✅ Three classes, each owned by exactly one actor
public sealed class ProductEntity   { public int Id { get; set; } public string Name { get; set; } = ""; public decimal Price { get; set; } }  // EF/DBA
public sealed record ProductDto(int Id, string DisplayName, decimal Price);                                                                    // API contract
public sealed class ProductPricing  { public decimal ApplySeasonalDiscount(decimal price) => price * 0.9m; }                                   // Finance domain
// Mapping between them is cheap. Coupling between actors is expensive.
```

---

## Gotchas

### Gotcha 1: `#region` Is a God-Class Symptom, Not a Fix

If a class needs `#region Persistence`, `#region Validation`, `#region Email` to stay readable, the regions are documenting the SRP violation — each region is a class trying to escape.

```csharp
public class CustomerService
{
    #region Database Operations      // ← this is a CustomerRepository
    #endregion
    #region Email Notifications      // ← this is a CustomerEmailService
    #endregion
    #region Validation               // ← this is a CustomerValidator
    #endregion
}
// Rule of thumb: every #region that groups by CONCERN (not by member kind)
// is a candidate class. Extract it.
```

### Gotcha 2: "Manager", "Helper", "Utils" Names Attract Garbage

Vague names create gravity. A class called `OrderManager` or `StringUtils` has no defined responsibility, so every developer dumps "miscellaneous" code into it. Six months later it's a god class.

```csharp
// ❌ Where does new code go? "Eh, put it in the helper."
public static class OrderHelper
{
    public static decimal CalculateTax(Order o) { /* Finance */ return 0; }
    public static string  FormatForEmail(Order o) { /* Marketing */ return ""; }
    public static bool    IsValidZipCode(string zip) { /* not even about orders! */ return true; }
}
```

**Fix:** name classes after a single responsibility (`TaxCalculator`, `OrderEmailFormatter`). If you can't name it without "Manager/Helper/Utils/Common", you haven't found the responsibility yet.

### Gotcha 3: An Orchestrator with Many Dependencies Is NOT a God Class

Teams over-correct and panic when a service has 4–5 constructor parameters. The test is not the dependency count — it's whether the class **implements** multiple concerns or merely **coordinates** them.

```csharp
// ✅ Fine — thin orchestration, zero business logic of its own
public sealed class CheckoutOrchestrator
{
    public CheckoutOrchestrator(IInventoryService inv, IPaymentGateway pay,
                                IOrderRepository repo, INotificationService notify) { /* ... */ }

    public async Task<CheckoutResult> CheckoutAsync(Cart cart)
    {
        await _inv.ReserveAsync(cart.Items);        // delegates
        var receipt = await _pay.ChargeAsync(cart); // delegates
        await _repo.SaveAsync(cart, receipt);       // delegates
        await _notify.SendConfirmationAsync(cart);  // delegates
        return CheckoutResult.Ok(receipt);
    }
}
// The orchestrator's single responsibility IS the sequencing.
// It becomes a god class only when it starts implementing the steps itself.
```

### Gotcha 4: Cross-Cutting Concerns Sprinkled Inline

Logging, caching, retry, and timing look innocent as one-liners — but they are separate responsibilities owned by Ops, and inline they multiply across every method.

```csharp
// ❌ Every method repeats the same three non-business concerns
public async Task<Order?> GetOrderAsync(Guid id)
{
    _logger.LogInformation("Getting order {Id}", id);            // logging concern
    if (_cache.TryGetValue(id, out Order? cached)) return cached; // caching concern
    var sw = Stopwatch.StartNew();                                // metrics concern
    var order = await _repo.GetByIdAsync(id);
    _metrics.Record("order.get", sw.ElapsedMilliseconds);
    _cache.Set(id, order, TimeSpan.FromMinutes(5));
    return order;
}
```

**Fix:** move cross-cutting concerns into decorators (see DIP file), pipeline behaviors (MediatR), or middleware. The business method should read as pure business.

### Gotcha 5: Splitting by Layer Is Not the Same as Splitting by Actor

`OrderService` + `OrderRepository` + `OrderController` looks like SRP, but if `OrderService` still contains pricing + tax + inventory + loyalty logic, the Finance/Warehouse/Marketing actors all still collide inside it. Layers separate *technical* concerns; SRP separates *change ownership*. You usually need both cuts.

### Gotcha 6: "It's Only 30 Lines" — Size Is Not the Metric

A 30-line class serving three actors violates SRP; an 800-line class serving one actor (e.g., a parser with one well-defined job) may not. Count **reasons to change**, not lines. The reverse trap: extracting nano-classes from a cohesive unit (see the misapplication section above) adds indirection without removing any reason to change.

---

## Decision Guide

| Situation | Action |
|---|---|
| Class is changed by more than one team/actor | Split by actor ownership |
| Class has unrelated fields used by different methods | Strong sign of SRP violation |
| Changing a business rule requires touching infrastructure code | SRP violation at module level |
| Unit test requires setting up 5+ unrelated dependencies | Class has too many responsibilities |
| Domain entity has EF Core / logging / email code in it | Extract to separate infrastructure classes |
| "One thing" feels like it should be 10 nano-classes | You've over-applied SRP — keep cohesive groups together |
