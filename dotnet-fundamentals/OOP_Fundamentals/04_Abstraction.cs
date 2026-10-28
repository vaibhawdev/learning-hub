// See 04_Abstraction.md for the full guide, leaky abstraction patterns, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OOP_Fundamentals.AbstractionPrinciple
{
    namespace BadCode
    {
        // ❌ IQueryable leaks EF Core — callers can build arbitrary queries outside the repo
        public interface IOrderRepository_Leaky
        {
            IQueryable<object> GetAll();
        }

        // ❌ Method names leak implementation details (SQL tables, Redis, etc.)
        public interface IUserStorage_Leaky
        {
            void         ExecuteSqlInsert(string sql, object parameters);
            List<object> FetchFromUsersTable(int page, int size);
            void         CacheUser(object user, bool useRedisCluster = false);
        }

        // ❌ Seven responsibilities — seven reasons to change
        public class GodOrderService
        {
            public void    ChargeCustomer(decimal amount)      { }
            public void    SendConfirmationEmail(string email) { }
            public void    SaveOrder(object order)             { }
            public byte[]  GenerateInvoicePdf(object order)   => throw new NotImplementedException();
            public decimal CalculateTax(decimal amt, string c) => throw new NotImplementedException();
            public void    DecrementStock(int id, int qty)     { }
            public void    TrackConversionEvent(object order)  { }
        }
    }

    namespace GoodCode
    {
        // ── Domain types ───────────────────────────────────────────────────────
        public record OrderId(Guid Value)
        {
            public static OrderId NewId() => new(Guid.NewGuid());
        }

        public record ProductId(int Value);

        public enum OrderStatus { Pending, Confirmed, Shipped, Delivered, Cancelled }

        public sealed class OrderLine
        {
            public ProductId ProductId   { get; }
            public string    ProductName { get; }
            public int       Quantity    { get; }
            public decimal   UnitPrice   { get; }
            public decimal   Subtotal    => Quantity * UnitPrice;

            public OrderLine(ProductId productId, string productName, int quantity, decimal unitPrice)
            {
                if (quantity <= 0) throw new ArgumentException("Quantity must be positive");
                if (unitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative");
                ProductId   = productId;
                ProductName = productName;
                Quantity    = quantity;
                UnitPrice   = unitPrice;
            }
        }

        public sealed class Order
        {
            public OrderId             Id          { get; }
            public string              CustomerId  { get; }
            public OrderStatus         Status      { get; private set; }
            public IReadOnlyList<OrderLine> Lines  { get; }
            public decimal             TotalAmount { get; }
            public DateTimeOffset      CreatedAt   { get; }

            public Order(string customerId, IReadOnlyList<OrderLine> lines)
            {
                if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("CustomerId required");
                if (lines == null || lines.Count == 0) throw new ArgumentException("At least one line required");
                Id          = OrderId.NewId();
                CustomerId  = customerId;
                Status      = OrderStatus.Pending;
                Lines       = lines;
                TotalAmount = lines.Sum(l => l.Subtotal);
                CreatedAt   = DateTimeOffset.UtcNow;
            }

            public void Confirm()
            {
                if (Status != OrderStatus.Pending)
                    throw new InvalidOperationException($"Cannot confirm order in status {Status}");
                Status = OrderStatus.Confirmed;
            }
        }

        // ── Domain exceptions — callers never catch SqlException or MongoException ──
        public class RepositoryException : Exception
        {
            public RepositoryException(string message) : base(message) { }
            public RepositoryException(string message, Exception inner) : base(message, inner) { }
        }

        public sealed class EntityNotFoundException : RepositoryException
        {
            public EntityNotFoundException(string type, object id)
                : base($"{type} with id '{id}' was not found.") { }
        }

        // ── Clean repository contract ──────────────────────────────────────────
        public interface IOrderRepository
        {
            Task<Order?>               GetByIdAsync(OrderId id, CancellationToken ct = default);
            Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken ct = default);
            Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct = default);
            Task                       SaveAsync(Order order, CancellationToken ct = default);
            Task                       DeleteAsync(OrderId id, CancellationToken ct = default);
        }

        // ── Focused service abstractions ───────────────────────────────────────
        public record PaymentResult(bool Success, string TransactionId, string? ErrorMessage);
        public record EmailMessage(string To, string Subject, string HtmlBody);

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

        // ── Single-responsibility orchestrator ────────────────────────────────
        public record OrderPlacementResult(bool Success, OrderId? OrderId, string? TransactionId, string? Error)
        {
            public static OrderPlacementResult Ok(OrderId id, string tx) => new(true, id, tx, null);
            public static OrderPlacementResult Failure(string err)       => new(false, null, null, err);
        }

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
                foreach (var line in lines)
                    if (!await _inventory.IsAvailableAsync(line.ProductId, line.Quantity, ct))
                        return OrderPlacementResult.Failure($"No stock for '{line.ProductName}'");

                var order   = new Order(customerId, lines);
                var total   = order.TotalAmount + _tax.Calculate(order.TotalAmount, countryCode);
                var payment = await _payment.ChargeAsync(customerId, total, currency, ct);
                if (!payment.Success) return OrderPlacementResult.Failure($"Payment failed: {payment.ErrorMessage}");

                foreach (var line in lines) await _inventory.ReserveAsync(line.ProductId, line.Quantity, ct);
                order.Confirm();
                await _orders.SaveAsync(order, ct);
                await _email.SendAsync(new EmailMessage(customerId, $"Order #{order.Id} Confirmed", "..."), ct);

                return OrderPlacementResult.Ok(order.Id, payment.TransactionId);
            }
        }

        // ── Cache abstraction with NullObject + MemoryCache implementations ────
        public interface ICache
        {
            Task<T?>  GetAsync<T>(string key, CancellationToken ct = default) where T : class;
            Task      SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;
            Task      RemoveAsync(string key, CancellationToken ct = default);
            Task<bool> ExistsAsync(string key, CancellationToken ct = default);
        }

        public sealed class NullCache : ICache
        {
            public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
                => Task.FromResult<T?>(null);
            public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
                => Task.CompletedTask;
            public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
        }

        public sealed class MemoryCache : ICache
        {
            private readonly Dictionary<string, (object Value, DateTimeOffset Expiry)> _store = new();
            private readonly object _lock = new();

            public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            {
                lock (_lock)
                {
                    if (_store.TryGetValue(key, out var e) && e.Expiry > DateTimeOffset.UtcNow)
                        return Task.FromResult((T?)e.Value);
                    _store.Remove(key);
                    return Task.FromResult<T?>(null);
                }
            }

            public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
            {
                lock (_lock) _store[key] = (value, DateTimeOffset.UtcNow.Add(ttl));
                return Task.CompletedTask;
            }

            public Task RemoveAsync(string key, CancellationToken ct = default)
            {
                lock (_lock) _store.Remove(key);
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
            {
                lock (_lock) return Task.FromResult(
                    _store.TryGetValue(key, out var e) && e.Expiry > DateTimeOffset.UtcNow);
            }
        }
    }

    namespace AdvancedConcepts
    {
        using GoodCode;

        // ── Specification pattern — composable query objects ───────────────────
        public abstract class Specification<T>
        {
            public abstract bool IsSatisfiedBy(T entity);
            public Specification<T> And(Specification<T> other) => new AndSpec<T>(this, other);
            public Specification<T> Or(Specification<T> other)  => new OrSpec<T>(this, other);
            public Specification<T> Not()                        => new NotSpec<T>(this);
        }

        public sealed class AndSpec<T> : Specification<T>
        {
            private readonly Specification<T> _l, _r;
            public AndSpec(Specification<T> l, Specification<T> r) => (_l, _r) = (l, r);
            public override bool IsSatisfiedBy(T e) => _l.IsSatisfiedBy(e) && _r.IsSatisfiedBy(e);
        }

        public sealed class OrSpec<T> : Specification<T>
        {
            private readonly Specification<T> _l, _r;
            public OrSpec(Specification<T> l, Specification<T> r) => (_l, _r) = (l, r);
            public override bool IsSatisfiedBy(T e) => _l.IsSatisfiedBy(e) || _r.IsSatisfiedBy(e);
        }

        public sealed class NotSpec<T> : Specification<T>
        {
            private readonly Specification<T> _inner;
            public NotSpec(Specification<T> inner) => _inner = inner;
            public override bool IsSatisfiedBy(T e) => !_inner.IsSatisfiedBy(e);
        }

        public sealed class PendingOrderSpec : Specification<Order>
        {
            public override bool IsSatisfiedBy(Order o) => o.Status == OrderStatus.Pending;
        }

        public sealed class HighValueOrderSpec : Specification<Order>
        {
            private readonly decimal _threshold;
            public HighValueOrderSpec(decimal t) => _threshold = t;
            public override bool IsSatisfiedBy(Order o) => o.TotalAmount >= _threshold;
        }

        // ── Caching decorator — adds behaviour without modifying the wrapped class ──
        public sealed class CachingOrderRepository : IOrderRepository
        {
            private readonly IOrderRepository _inner;
            private readonly ICache           _cache;
            private readonly TimeSpan         _ttl;

            public CachingOrderRepository(IOrderRepository inner, ICache cache, TimeSpan ttl)
                => (_inner, _cache, _ttl) = (inner, cache, ttl);

            public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
            {
                var key    = $"order:{id.Value}";
                var cached = await _cache.GetAsync<Order>(key, ct);
                if (cached is not null) return cached;

                var order = await _inner.GetByIdAsync(id, ct);
                if (order is not null) await _cache.SetAsync(key, order, _ttl, ct);
                return order;
            }

            public async Task SaveAsync(Order order, CancellationToken ct = default)
            {
                await _inner.SaveAsync(order, ct);
                await _cache.RemoveAsync($"order:{order.Id.Value}", ct);
            }

            public Task<IReadOnlyList<Order>> GetByCustomerAsync(string id, CancellationToken ct)
                => _inner.GetByCustomerAsync(id, ct);
            public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct)
                => _inner.GetPendingOrdersAsync(ct);
            public Task DeleteAsync(OrderId id, CancellationToken ct)
                => _inner.DeleteAsync(id, ct);
        }

        // ── In-memory repository for tests — zero infrastructure required ──────
        public sealed class InMemoryOrderRepository : IOrderRepository
        {
            private readonly Dictionary<OrderId, Order> _store = new();

            public Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
                => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null);

            public Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = _store.Values.Where(o => o.CustomerId == customerId).ToList();
                return Task.FromResult(r);
            }

            public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = _store.Values.Where(o => o.Status == OrderStatus.Pending).ToList();
                return Task.FromResult(r);
            }

            public Task SaveAsync(Order order, CancellationToken ct = default)
            {
                _store[order.Id] = order; return Task.CompletedTask;
            }

            public Task DeleteAsync(OrderId id, CancellationToken ct = default)
            {
                _store.Remove(id); return Task.CompletedTask;
            }
        }
    }

    public static class AbstractionDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== ABSTRACTION DEMO ===\n");

            var repo  = new AdvancedConcepts.InMemoryOrderRepository();
            var cache = new GoodCode.MemoryCache();
            GoodCode.IOrderRepository cachedRepo =
                new AdvancedConcepts.CachingOrderRepository(repo, cache, TimeSpan.FromMinutes(5));

            var lines = new GoodCode.OrderLine[]
            {
                new(new GoodCode.ProductId(1), "Laptop",   1, 1299.99m),
                new(new GoodCode.ProductId(2), "Mouse",    2,   29.99m),
            };

            var order = new GoodCode.Order("cust-42", lines);
            await cachedRepo.SaveAsync(order);

            var retrieved = await cachedRepo.GetByIdAsync(order.Id);
            Console.WriteLine($"Order {retrieved?.Id.Value} — Total: {retrieved?.TotalAmount:C}");

            var spec = new AdvancedConcepts.PendingOrderSpec()
                .And(new AdvancedConcepts.HighValueOrderSpec(500m));
            Console.WriteLine($"Matches spec: {spec.IsSatisfiedBy(order)}");
        }
    }
}
