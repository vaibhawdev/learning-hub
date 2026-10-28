# 01 — Encapsulation

> **Core idea:** An object owns its own state and is the *only* one allowed to change it.

---

## What is Encapsulation?

Encapsulation bundles **data** (fields) and the **behaviour** that operates on that data into a single unit, then restricts direct access to the internals. External code interacts only through a controlled public surface.

The object becomes a *trusted guardian* of its own invariants — the business rules that define what "valid state" means for that object.

---

## Key Concepts

| Concept | Plain English | C# Mechanic |
|---|---|---|
| **Data hiding** | Don't let outsiders touch raw fields | `private` / `readonly` fields |
| **Controlled access** | Expose state through properties, not fields | `{ get; private set; }` |
| **Immutability** | Some state should never change after construction | `readonly`, `init`, `record struct` |
| **Invariant enforcement** | Business rules live *inside* the object | Methods that validate before mutating |
| **Anemic Domain Model** | The #1 anti-pattern — objects as pure data bags | No behaviour, all logic in Services |

---

## 5 Real-World Use Cases

1. **BankAccount** — balance only changes via `Deposit()` / `Withdraw()`, never set directly.
2. **Order** — status transitions (`Pending → Confirmed → Shipped`) enforced by the `Order` class itself.
3. **User** — password stored as a hash; the raw value is never exposed or returned.
4. **Inventory** — stock count cannot go negative; the object enforces this constraint.
5. **Configuration** — immutable settings built once at startup via a Builder, read-only thereafter.

---

## ❌ Anti-Pattern: Anemic Domain Model

The object is a pure data container. All business logic lives in a separate `Service` class.

**Why it collapses:**
- Any code anywhere can write `account.Balance = -99999` — no protection.
- Rules are scattered across services; adding a new entry point (mobile app, batch job) risks missing one.
- You cannot tell what "valid state" means just by reading the class.

```csharp
// ❌ Pure data bag — NO behaviour, NO invariant enforcement
public class BankAccount_Anemic
{
    public Guid    Id           { get; set; }
    public string  Owner        { get; set; }   // Anyone can set null
    public decimal Balance      { get; set; }   // Anyone can set -∞
    public bool    IsLocked     { get; set; }   // Anyone can bypass fraud lock
    public List<string> Transactions { get; set; } = new();
}

// All "business logic" lives here — and in every other service that touches accounts
public class BankAccountService_Anemic
{
    public void Deposit(BankAccount_Anemic account, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (account.IsLocked) throw new InvalidOperationException("Account is locked");
        account.Balance += amount;
        account.Transactions.Add($"Deposited {amount:C}");
    }
    // A second developer writes another service that skips the lock check entirely.
    // Nothing in the class stops them.
}

// Nothing prevents this:
account.Balance  = -999_999m;   // Negative balance — impossible in reality
account.IsLocked = false;       // Bypasses fraud lock silently
account.Owner    = null;        // Corrupts identity
account.Transactions = null;    // Destroys audit trail
```

---

## ✅ Pattern: Rich Domain Model

The object defends its own state. External code can only interact through behaviour methods.

```csharp
// Immutable value type — prevents primitive obsession, stack-allocated
public readonly record struct Money(decimal Amount, string Currency)
{
    public Money  // Constructor validation — illegal values never exist
    {
        if (Amount < 0) throw new ArgumentException("Money cannot be negative");
        if (string.IsNullOrWhiteSpace(Currency)) throw new ArgumentException("Currency required");
    }

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new InvalidOperationException("Currency mismatch");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (b.Amount > a.Amount) throw new InvalidOperationException("Result would be negative");
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public bool IsGreaterThan(Money other) => Amount > other.Amount;
    public override string ToString() => $"{Currency} {Amount:F2}";
}

public sealed class BankAccount
{
    // Private backing fields — never exposed directly
    private readonly string _owner;
    private Money _balance;
    private LockReason _lockReason;
    private readonly List<TransactionEvent> _transactions = new();

    // Read-only public surface
    public string Owner   => _owner;
    public Money  Balance => _balance;
    public bool   IsLocked => _lockReason != LockReason.None;

    // External code gets a read-only view — cannot clear or add entries
    public IReadOnlyList<TransactionEvent> Transactions => _transactions.AsReadOnly();

    public BankAccount(string owner, string currency)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner required");
        _owner   = owner;
        _balance = new Money(0m, currency);
    }

    // ── The ONLY ways to change balance ────────────────────────────────────
    public void Deposit(decimal amount)
    {
        ThrowIfLocked();
        if (amount <= 0) throw new ArgumentException("Must be positive");
        _balance = _balance + new Money(amount, _balance.Currency);
        RecordTransaction("DEPOSIT", new Money(amount, _balance.Currency));
    }

    public void Withdraw(decimal amount)
    {
        ThrowIfLocked();
        var withdraw = new Money(amount, _balance.Currency);
        if (withdraw.IsGreaterThan(_balance))
            throw new InvalidOperationException($"Insufficient funds. Balance: {_balance}");
        _balance = _balance - withdraw;
        RecordTransaction("WITHDRAWAL", withdraw);
    }

    public void Lock(LockReason reason)
    {
        if (reason == LockReason.None) throw new ArgumentException("Must specify a reason");
        _lockReason = reason;
    }

    public void Unlock() => _lockReason = LockReason.None;

    private void ThrowIfLocked()
    {
        if (IsLocked) throw new InvalidOperationException($"Account locked: {_lockReason}");
    }

    private void RecordTransaction(string type, Money amount)
        => _transactions.Add(new TransactionEvent(type, amount, _balance, DateTimeOffset.UtcNow));
}
```

---

## Intermediate: Immutable Builder for Configuration

When an object has many optional fields and must be immutable once built, the Builder pattern is the right tool.

```csharp
public sealed class DatabaseConfig
{
    // init-only: settable only in object initializer, immutable after that
    public string   Host              { get; init; }
    public int      Port              { get; init; }
    public string   DatabaseName      { get; init; }
    public int      MaxPoolSize       { get; init; }
    public TimeSpan ConnectionTimeout { get; init; }

    private DatabaseConfig() { }   // Force use of Builder

    public sealed class Builder
    {
        private string   _host    = "localhost";
        private int      _port    = 5432;
        private string   _dbName  = string.Empty;
        private int      _maxPool = 20;
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public Builder WithHost(string host)        { _host    = host;    return this; }
        public Builder WithPort(int port)           { _port    = port;    return this; }
        public Builder WithDatabase(string name)    { _dbName  = name;    return this; }
        public Builder WithMaxPoolSize(int size)    { _maxPool = size;    return this; }
        public Builder WithConnectionTimeout(TimeSpan t) { _timeout = t; return this; }

        public DatabaseConfig Build()
        {
            if (string.IsNullOrWhiteSpace(_dbName))
                throw new InvalidOperationException("DatabaseName is required");

            return new DatabaseConfig
            {
                Host = _host, Port = _port, DatabaseName = _dbName,
                MaxPoolSize = _maxPool, ConnectionTimeout = _timeout
            };
        }
    }
}

// Usage — fluent, validated, immutable result
var config = new DatabaseConfig.Builder()
    .WithHost("prod-db.internal")
    .WithPort(5432)
    .WithDatabase("orders_db")
    .WithMaxPoolSize(50)
    .Build();
```

---

## Intermediate: Order State Machine

Encapsulation lets the object enforce legal state transitions. Callers cannot put it in an illegal state.

```csharp
public sealed class Order
{
    private OrderStatus _status;
    private string?     _trackingNumber;

    public OrderStatus Status         => _status;
    public string?     TrackingNumber => _trackingNumber;

    public Order(IEnumerable<string> items)
    {
        if (!items.Any()) throw new ArgumentException("Order must have at least one item");
        _status = OrderStatus.Pending;
    }

    public void ConfirmPayment()
    {
        EnsureStatus(OrderStatus.Pending, "confirm payment");
        _status = OrderStatus.PaymentConfirmed;
    }

    public void Ship(string trackingNumber)
    {
        EnsureStatus(OrderStatus.PaymentConfirmed, "ship");
        _status         = OrderStatus.Shipped;
        _trackingNumber = trackingNumber;
    }

    public void Cancel()
    {
        if (_status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new InvalidOperationException($"Cannot cancel a {_status} order");
        _status = OrderStatus.Cancelled;
    }

    private void EnsureStatus(OrderStatus required, string operation)
    {
        if (_status != required)
            throw new InvalidOperationException(
                $"Cannot {operation} when status is {_status}. Required: {required}");
    }
}
```

---

## Advanced: Thread Safety and Encapsulation

Encapsulation tells you *where* to put the fix (inside the class). Thread safety tells you *how*.

```csharp
public sealed class ThreadSafeBankAccount
{
    private readonly object _syncRoot = new();  // Dedicated lock object — not 'this'
    private decimal _balance;

    public decimal Balance
    {
        get { lock (_syncRoot) { return _balance; } }  // Read under lock — stale otherwise
    }

    public void Deposit(decimal amount)
    {
        lock (_syncRoot)
        {
            _balance += amount;  // Read + modify + write is atomic inside the lock
        }
    }

    public bool TryWithdraw(decimal amount)
    {
        lock (_syncRoot)
        {
            if (_balance < amount) return false;
            _balance -= amount;
            return true;
        }
    }

    // ⚠️ NEVER call external code (events, callbacks, other services) inside a lock.
    //    The external code may itself acquire a lock → deadlock.
    //    Keep lock scope as NARROW as possible.
}
```

---

## Advanced: Memory Implications

### `record struct` vs `record class` for value objects

```csharp
// Stack-allocated when local — zero GC pressure
public readonly record struct Money(decimal Amount, string Currency) { }

// Heap-allocated — every creation is a GC allocation
public record class Money(decimal Amount, string Currency) { }
```

In a high-throughput trading system creating millions of `Money` instances per second, the struct version measurably reduces Gen0 GC pauses.

**Rule:** Use `readonly record struct` for small immutable value objects (under ~16 bytes). Use `record class` when the object is genuinely reference-typed (shared, nullable, polymorphic).

### Defensive copy trap

```csharp
// ❌ Allocates a new ReadOnlyCollection wrapper on every property access
public IReadOnlyList<TransactionEvent> Transactions
    => new ReadOnlyCollection<TransactionEvent>(_transactions);

// ✅ List<T> already implements IReadOnlyList<T> — zero allocation
public IReadOnlyList<TransactionEvent> Transactions => _transactions;
```

---

## Advanced: Anemic vs Rich — The Real Trade-off

Anemic models are **not always wrong**. They are appropriate when:

- Building pure CRUD APIs with no meaningful domain logic.
- Writing read-side projections in a CQRS architecture.
- The team is small and the service layer IS the domain layer.

Rich Domain Models pay off when:

- Business rules are complex and change independently of the database schema.
- Multiple entry points (REST API, background jobs, event consumers) must enforce the **same** rules — you cannot trust every caller to check.
- You need an auditable change trail (the object logs its own domain events).

> **Decision rule:** Default to rich models for core business entities. Use anemic DTOs/ViewModels at the API boundary (request/response shapes).

---

## Decision Guide

| Situation | Approach |
|---|---|
| Field that must never be set externally | `private readonly` backing field |
| Field readable externally, writable only by class | `{ get; private set; }` |
| Field set once at object init, never after | `{ get; init; }` |
| Small value type with equality semantics | `readonly record struct` |
| Complex construction with many optional fields | Builder pattern |
| Multiple callers must share same business rules | Rich Domain Model |
| State has legal transitions | State machine inside the object |
| Thread-shared mutable state | `lock(_syncRoot)` with narrow scope |
