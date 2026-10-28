// See 01_Encapsulation.md for the full guide, anti-patterns, and decision tables.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace OOP_Fundamentals.Encapsulation
{
    namespace BadCode
    {
        public class BankAccount_Anemic
        {
            public Guid    Id           { get; set; }
            public string  Owner        { get; set; }
            public decimal Balance      { get; set; }
            public bool    IsLocked     { get; set; }
            public List<string> Transactions { get; set; } = new();
        }

        public class BankAccountService_Anemic
        {
            public void Deposit(BankAccount_Anemic account, decimal amount)
            {
                if (amount <= 0) throw new ArgumentException("Amount must be positive");
                if (account.IsLocked) throw new InvalidOperationException("Account is locked");
                account.Balance += amount;
                account.Transactions.Add($"Deposited {amount:C}");
            }

            public void Withdraw(BankAccount_Anemic account, decimal amount)
            {
                if (amount <= 0) throw new ArgumentException("Amount must be positive");
                if (amount > account.Balance) throw new InvalidOperationException("Insufficient funds");
                account.Balance -= amount;
                account.Transactions.Add($"Withdrew {amount:C}");
            }
        }
    }

    namespace GoodCode
    {
        public readonly record struct Money(decimal Amount, string Currency)
        {
            public Money
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

        public readonly record struct AccountId(Guid Value)
        {
            public static AccountId NewId() => new(Guid.NewGuid());
        }

        public record TransactionEvent(
            string Type, Money Amount, Money BalanceAfter, DateTimeOffset OccurredAt);

        public enum LockReason { None, FraudSuspected, OwnerRequest, Regulatory }

        public sealed class BankAccount
        {
            private readonly AccountId _id;
            private readonly string _owner;
            private Money _balance;
            private LockReason _lockReason;
            private readonly List<TransactionEvent> _transactions = new();

            public AccountId Id      => _id;
            public string    Owner   => _owner;
            public Money     Balance => _balance;
            public bool      IsLocked => _lockReason != LockReason.None;
            public LockReason LockReason => _lockReason;

            // List<T> implements IReadOnlyList<T> — no wrapper allocation needed
            public IReadOnlyList<TransactionEvent> Transactions => _transactions;

            public BankAccount(string owner, string currency)
            {
                if (string.IsNullOrWhiteSpace(owner))
                    throw new ArgumentException("Owner required", nameof(owner));
                _id      = AccountId.NewId();
                _owner   = owner;
                _balance = new Money(0m, currency);
            }

            public void Deposit(decimal amount)
            {
                ThrowIfLocked();
                if (amount <= 0) throw new ArgumentException("Must be positive");
                var deposit = new Money(amount, _balance.Currency);
                _balance = _balance + deposit;
                _transactions.Add(new TransactionEvent("DEPOSIT", deposit, _balance, DateTimeOffset.UtcNow));
            }

            public void Withdraw(decimal amount)
            {
                ThrowIfLocked();
                var withdraw = new Money(amount, _balance.Currency);
                if (withdraw.IsGreaterThan(_balance))
                    throw new InvalidOperationException($"Insufficient funds. Balance: {_balance}");
                _balance = _balance - withdraw;
                _transactions.Add(new TransactionEvent("WITHDRAWAL", withdraw, _balance, DateTimeOffset.UtcNow));
            }

            public void Transfer(BankAccount destination, decimal amount)
            {
                ThrowIfLocked();
                destination.ThrowIfLocked();
                Withdraw(amount);
                destination.Deposit(amount);
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
        }

        public sealed class DatabaseConfig
        {
            public string   Host              { get; init; } = "localhost";
            public int      Port              { get; init; } = 5432;
            public string   DatabaseName      { get; init; } = string.Empty;
            public int      MaxPoolSize       { get; init; } = 20;
            public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

            private DatabaseConfig() { }

            public string ToConnectionString() =>
                $"Host={Host};Port={Port};Database={DatabaseName};" +
                $"MaxPoolSize={MaxPoolSize};Timeout={ConnectionTimeout.TotalSeconds}";

            public sealed class Builder
            {
                private string   _host    = "localhost";
                private int      _port    = 5432;
                private string   _dbName  = string.Empty;
                private int      _maxPool = 20;
                private TimeSpan _timeout = TimeSpan.FromSeconds(30);

                public Builder WithHost(string host)               { _host    = host;    return this; }
                public Builder WithPort(int port)                  { _port    = port;    return this; }
                public Builder WithDatabase(string name)           { _dbName  = name;    return this; }
                public Builder WithMaxPoolSize(int size)           { _maxPool = size;    return this; }
                public Builder WithConnectionTimeout(TimeSpan ttl) { _timeout = ttl;     return this; }

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
    }

    namespace AdvancedConcepts
    {
        public sealed class ThreadSafeBankAccount
        {
            // Dedicated lock object — never lock on 'this' or public fields
            private readonly object _syncRoot = new();
            private decimal _balance;

            public decimal Balance
            {
                get { lock (_syncRoot) { return _balance; } }
            }

            public void Deposit(decimal amount)
            {
                if (amount <= 0) throw new ArgumentException("Must be positive");
                lock (_syncRoot) { _balance += amount; }
            }

            public bool TryWithdraw(decimal amount)
            {
                if (amount <= 0) throw new ArgumentException("Must be positive");
                lock (_syncRoot)
                {
                    if (_balance < amount) return false;
                    _balance -= amount;
                    return true;
                }
            }
        }

        public enum OrderStatus { Pending, PaymentConfirmed, Shipped, Delivered, Cancelled }

        public sealed class Order
        {
            private readonly List<string> _items;
            private OrderStatus _status;
            private DateTimeOffset? _shippedAt;
            private string? _trackingNumber;

            public IReadOnlyList<string> Items          => _items.AsReadOnly();
            public OrderStatus           Status         => _status;
            public Guid                  Id             { get; } = Guid.NewGuid();
            public DateTimeOffset?       ShippedAt      => _shippedAt;
            public string?               TrackingNumber => _trackingNumber;

            public Order(IEnumerable<string> items)
            {
                var list = new List<string>(items);
                if (list.Count == 0) throw new ArgumentException("At least one item required");
                _items  = list;
                _status = OrderStatus.Pending;
            }

            public void ConfirmPayment() { EnsureStatus(OrderStatus.Pending, "confirm payment"); _status = OrderStatus.PaymentConfirmed; }

            public void Ship(string trackingNumber)
            {
                EnsureStatus(OrderStatus.PaymentConfirmed, "ship");
                if (string.IsNullOrWhiteSpace(trackingNumber)) throw new ArgumentException("Tracking number required");
                _status         = OrderStatus.Shipped;
                _shippedAt      = DateTimeOffset.UtcNow;
                _trackingNumber = trackingNumber;
            }

            public void MarkDelivered() { EnsureStatus(OrderStatus.Shipped, "mark delivered"); _status = OrderStatus.Delivered; }

            public void Cancel()
            {
                if (_status is OrderStatus.Shipped or OrderStatus.Delivered)
                    throw new InvalidOperationException($"Cannot cancel a {_status} order");
                _status = OrderStatus.Cancelled;
            }

            private void EnsureStatus(OrderStatus required, string op)
            {
                if (_status != required)
                    throw new InvalidOperationException($"Cannot {op} when status is {_status}. Required: {required}");
            }
        }

        public sealed class MemoryEfficientAccount
        {
            private readonly List<string> _log = new();
            public IReadOnlyList<string> Log => _log;   // zero allocation — List<T> is IReadOnlyList<T>

            public ImmutableList<string> ImmutableSnapshot()
                => ImmutableList.CreateRange(_log);     // explicit copy cost — only when you need immutability
        }
    }

    public static class EncapsulationDemo
    {
        public static void Run()
        {
            var alice = new GoodCode.BankAccount("Alice", "USD");
            var bob   = new GoodCode.BankAccount("Bob",   "USD");

            alice.Deposit(1000m);
            alice.Transfer(bob, 300m);
            Console.WriteLine($"Alice: {alice.Balance}  Bob: {bob.Balance}");

            alice.Lock(GoodCode.LockReason.FraudSuspected);
            try { alice.Deposit(50m); }
            catch (InvalidOperationException ex) { Console.WriteLine(ex.Message); }

            var config = new GoodCode.DatabaseConfig.Builder()
                .WithHost("prod-db.internal")
                .WithDatabase("orders")
                .WithMaxPoolSize(50)
                .Build();
            Console.WriteLine(config.ToConnectionString());

            var order = new AdvancedConcepts.Order(new[] { "Laptop", "Mouse" });
            order.ConfirmPayment();
            order.Ship("TRACK-001");
            Console.WriteLine($"Order {order.Id}: {order.Status}, tracking: {order.TrackingNumber}");
        }
    }
}
