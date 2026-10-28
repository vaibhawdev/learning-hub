// See 05_Dependency_Inversion.md for DIP vs DI vs IoC, Service Locator analysis, and lifetime rules.
// Requires: Microsoft.Extensions.DependencyInjection (NuGet) for the IoC container section.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SOLID.DependencyInversion
{
    namespace BadCode
    {
        // ❌ Bastard injection: high-level module directly creates low-level modules
        public class OrderService_Hardcoded
        {
            // Both dependencies are created internally — cannot be swapped or mocked
            private readonly SqlOrderRepository_Fake    _repo  = new();
            private readonly SmtpEmailSender_Fake       _email = new();

            public async Task<Guid> PlaceOrderAsync(string customerId, decimal total)
            {
                // Tests require a real DB and real SMTP — impossible to unit test
                var orderId = Guid.NewGuid();
                await _repo.SaveAsync(orderId, customerId, total);
                await _email.SendConfirmationAsync(customerId, orderId);
                return orderId;
            }
        }

        public class SqlOrderRepository_Fake { public Task SaveAsync(Guid id, string c, decimal t) => Task.CompletedTask; }
        public class SmtpEmailSender_Fake    { public Task SendConfirmationAsync(string c, Guid id) => Task.CompletedTask; }

        // ❌ Service Locator — hidden dependencies, global state, test pollution
        public static class ServiceLocator
        {
            private static readonly Dictionary<Type, object> _services = new();
            public static void Register<T>(T service) where T : class => _services[typeof(T)] = service;
            public static T   Get<T>() => (T)_services[typeof(T)];
        }

        public class OrderService_ServiceLocator
        {
            public async Task<Guid> PlaceOrderAsync(string customerId, decimal total)
            {
                // Dependencies invisible from the outside — you must read the body to find them
                // Tests must populate the global ServiceLocator correctly (shared state between tests)
                var repo  = ServiceLocator.Get<GoodCode.IOrderRepository>();
                var email = ServiceLocator.Get<GoodCode.IEmailService>();

                var orderId = Guid.NewGuid();
                await repo.SaveAsync(orderId, customerId, total);
                await email.SendAsync(new GoodCode.EmailMessage(customerId, "Order Confirmed", $"Order {orderId}"));
                return orderId;
            }
        }

        // ❌ DI technique WITHOUT DIP principle — concretion injected
        // Can be wired by a container but the coupling to SQL is still concrete
        public class OrderService_WrongDI
        {
            private readonly SqlOrderRepository_Fake _repo;  // concretion — not an abstraction

            public OrderService_WrongDI(SqlOrderRepository_Fake repo) => _repo = repo;
            // Swapping to Dapper or MongoDB requires changing this class's constructor. Defeats DI.
        }
    }

    namespace GoodCode
    {
        // ── Abstractions owned by the high-level (Application) layer ──────────
        public interface IOrderRepository
        {
            Task SaveAsync(Guid orderId, string customerId, decimal total, CancellationToken ct = default);
            Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
        }

        public interface IEmailService
        {
            Task SendAsync(EmailMessage message, CancellationToken ct = default);
        }

        public interface IInventoryService
        {
            Task<bool> IsAvailableAsync(string productId, int qty, CancellationToken ct = default);
            Task       ReserveAsync(string productId, int qty, CancellationToken ct = default);
        }

        public interface IAuditLog
        {
            Task RecordAsync(string action, object context, CancellationToken ct = default);
        }

        public record EmailMessage(string To, string Subject, string Body);
        public record Order(Guid Id, string CustomerId, decimal Total);

        // ── High-level module: depends only on abstractions ────────────────────
        public sealed class OrderService
        {
            private readonly IOrderRepository  _repo;
            private readonly IEmailService     _email;
            private readonly IInventoryService _inventory;

            // All dependencies declared at construction — visible, testable, replaceable
            public OrderService(IOrderRepository repo, IEmailService email, IInventoryService inventory)
            {
                _repo      = repo      ?? throw new ArgumentNullException(nameof(repo));
                _email     = email     ?? throw new ArgumentNullException(nameof(email));
                _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            }

            public async Task<OrderResult> PlaceOrderAsync(
                string customerId, string productId, int quantity, decimal unitPrice,
                CancellationToken ct = default)
            {
                if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("CustomerId required");
                if (quantity <= 0) throw new ArgumentException("Quantity must be positive");

                var available = await _inventory.IsAvailableAsync(productId, quantity, ct);
                if (!available) return OrderResult.Failure($"'{productId}' is out of stock");

                var total   = quantity * unitPrice;
                var orderId = Guid.NewGuid();

                await _repo.SaveAsync(orderId, customerId, total, ct);
                await _inventory.ReserveAsync(productId, quantity, ct);
                await _email.SendAsync(new EmailMessage(
                    customerId,
                    $"Order {orderId} Confirmed",
                    $"Your order for {quantity}x {productId} ({total:C}) is confirmed."), ct);

                return OrderResult.Ok(orderId);
            }
        }

        public record OrderResult(bool Success, Guid? OrderId, string? Error)
        {
            public static OrderResult Ok(Guid id)       => new(true, id, null);
            public static OrderResult Failure(string e) => new(false, null, e);
        }

        // ── Low-level implementations: point upward to abstractions ────────────
        public sealed class SqlOrderRepository : IOrderRepository
        {
            private readonly string _connectionString;
            public SqlOrderRepository(string cs) => _connectionString = cs;

            public Task SaveAsync(Guid id, string cust, decimal total, CancellationToken ct = default)
            {
                Console.WriteLine($"[SQL] INSERT order {id} cust={cust} total={total:C}");
                return Task.CompletedTask;
            }

            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult<Order?>(new Order(id, "customer", 100m));
        }

        public sealed class SendGridEmailService : IEmailService
        {
            private readonly string _apiKey;
            public SendGridEmailService(string apiKey) => _apiKey = apiKey;

            public Task SendAsync(EmailMessage msg, CancellationToken ct = default)
            {
                Console.WriteLine($"[SendGrid] To:{msg.To} Subject:{msg.Subject}");
                return Task.CompletedTask;
            }
        }

        public sealed class InMemoryInventoryService : IInventoryService
        {
            private readonly Dictionary<string, int> _stock;
            public InMemoryInventoryService(Dictionary<string, int> initialStock)
                => _stock = new Dictionary<string, int>(initialStock);

            public Task<bool> IsAvailableAsync(string productId, int qty, CancellationToken ct = default)
                => Task.FromResult(_stock.TryGetValue(productId, out var s) && s >= qty);

            public Task ReserveAsync(string productId, int qty, CancellationToken ct = default)
            {
                if (_stock.ContainsKey(productId)) _stock[productId] -= qty;
                return Task.CompletedTask;
            }
        }

        // ── Test doubles — zero infrastructure required ─────────────────────────
        public sealed class InMemoryOrderRepository : IOrderRepository
        {
            public readonly List<(Guid Id, string Cust, decimal Total)> Saved = new();

            public Task SaveAsync(Guid id, string cust, decimal total, CancellationToken ct = default)
            { Saved.Add((id, cust, total)); return Task.CompletedTask; }

            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult(Saved.Find(o => o.Id == id) is var found && found != default
                    ? new Order(found.Id, found.Cust, found.Total)
                    : (Order?)null);
        }

        public sealed class CapturedEmailService : IEmailService
        {
            public readonly List<EmailMessage> Sent = new();
            public Task SendAsync(EmailMessage msg, CancellationToken ct = default)
            { Sent.Add(msg); return Task.CompletedTask; }
        }

        public sealed class NoopEmailService : IEmailService
        {
            public Task SendAsync(EmailMessage msg, CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    namespace AdvancedConcepts
    {
        using GoodCode;

        // ── Lifetime management gotchas ────────────────────────────────────────

        // ❌ Captive dependency: Singleton captures Scoped → Scoped lives too long
        // This is what ASP.NET Core's scope validation catches at startup.
        // Uncomment to see InvalidOperationException in Development mode:
        //
        // services.AddSingleton<ReportCache>();           // Singleton
        // services.AddScoped<IOrderRepository, ...>();    // Scoped
        // → InvalidOperationException: Cannot consume scoped service 'IOrderRepository'
        //   from singleton 'ReportCache'.

        public sealed class ReportCache_Captive
        {
            // ❌ Captured at construction time — tied to first request's scope forever
            private readonly IOrderRepository _repo;
            public ReportCache_Captive(IOrderRepository repo) => _repo = repo;
        }

        // ✅ Fix: IServiceScopeFactory — resolves a fresh Scoped instance per operation
        public sealed class ReportCache_Fixed
        {
            private readonly IServiceProvider _rootProvider;

            public ReportCache_Fixed(IServiceProvider rootProvider)
                => _rootProvider = rootProvider;

            public async Task<Order?> GetOrderAsync(Guid id, CancellationToken ct = default)
            {
                // Creates a fresh scope — gets a fresh Scoped IOrderRepository
                using var scope = _rootProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                return await repo.GetByIdAsync(id, ct);
            }   // scope disposed — Scoped dep is released properly
        }

        // IServiceProvider shim for demo (real apps use Microsoft.Extensions.DI)
        public interface IServiceProvider
        {
            IServiceScope CreateScope();
            T GetRequiredService<T>() where T : class;
        }

        public interface IServiceScope : IDisposable
        {
            IServiceProvider ServiceProvider { get; }
        }

        // ✅ Func<T> factory pattern — deferred and repeated resolution without IServiceProvider
        public sealed class BackgroundProcessor
        {
            // Factory injected, not the dependency itself
            private readonly Func<IOrderRepository> _repoFactory;

            public BackgroundProcessor(Func<IOrderRepository> repoFactory)
                => _repoFactory = repoFactory ?? throw new ArgumentNullException(nameof(repoFactory));

            public async Task ProcessBatchAsync(IEnumerable<Guid> orderIds, CancellationToken ct = default)
            {
                foreach (var id in orderIds)
                {
                    var repo  = _repoFactory();   // fresh instance per operation
                    var order = await repo.GetByIdAsync(id, ct);
                    Console.WriteLine($"[BG] Processing order {order?.Id}");
                }
            }
        }

        // ── IoC container registration pattern (pseudo-code, compilable) ────────
        public static class ServiceRegistration
        {
            // Shows the STRUCTURE of registration — in real apps use Microsoft.Extensions.DI
            public static void Register(ServiceContainer container, string connStr, string sgKey)
            {
                // Abstractions → Concretions
                container.AddScoped<IOrderRepository>(
                    () => new SqlOrderRepository(connStr));

                container.AddSingleton<IEmailService>(
                    () => new SendGridEmailService(sgKey));

                container.AddTransient<IInventoryService>(
                    () => new InMemoryInventoryService(new Dictionary<string, int>
                    {
                        ["product-1"] = 100,
                        ["product-2"] = 50
                    }));

                container.AddScoped<OrderService>();
            }
        }

        // Minimal container shim for the demo (real apps: Microsoft.Extensions.DI)
        public sealed class ServiceContainer
        {
            private readonly Dictionary<Type, Func<object>> _factories = new();
            private readonly Dictionary<Type, object>       _singletons = new();

            public void AddScoped<T>(Func<T> factory) where T : class
                => _factories[typeof(T)] = () => factory();

            public void AddSingleton<T>(Func<T> factory) where T : class
                => _factories[typeof(T)] = () =>
                {
                    if (!_singletons.TryGetValue(typeof(T), out var s))
                        _singletons[typeof(T)] = s = factory()!;
                    return s;
                };

            public void AddTransient<T>(Func<T> factory) where T : class
                => _factories[typeof(T)] = () => factory();

            public void AddScoped<T>() where T : class, new()
                => _factories[typeof(T)] = () => new T();

            public T Resolve<T>() where T : class
                => (T)_factories[typeof(T)]();
        }
    }

    public static class DipDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== DIP DEMO ===\n");

            // ── Bad: Service Locator wiring ────────────────────────────────────
            Console.WriteLine("── Service Locator (bad — for contrast only) ──");
            BadCode.ServiceLocator.Register<GoodCode.IOrderRepository>(new GoodCode.InMemoryOrderRepository());
            BadCode.ServiceLocator.Register<GoodCode.IEmailService>(new GoodCode.NoopEmailService());
            var slService = new BadCode.OrderService_ServiceLocator();
            var slId = await slService.PlaceOrderAsync("cust-99", 200m);
            Console.WriteLine($"Service Locator order: {slId} (hidden deps — you wouldn't know what it needed)");

            // ── Good: Constructor injection ────────────────────────────────────
            Console.WriteLine("\n── Constructor injection (good) ──");
            var repo      = new GoodCode.InMemoryOrderRepository();
            var emailSvc  = new GoodCode.CapturedEmailService();
            var inventory = new GoodCode.InMemoryInventoryService(
                new Dictionary<string, int> { ["laptop"] = 10, ["mouse"] = 50 });

            var service = new GoodCode.OrderService(repo, emailSvc, inventory);

            var result1 = await service.PlaceOrderAsync("alice", "laptop", 2, 1299.99m);
            Console.WriteLine($"Order placed: {(result1.Success ? result1.OrderId : result1.Error)}");
            Console.WriteLine($"Emails sent:  {emailSvc.Sent.Count} (subject: '{emailSvc.Sent[0].Subject}')");
            Console.WriteLine($"Orders saved: {repo.Saved.Count}");

            var result2 = await service.PlaceOrderAsync("bob", "keyboard", 1, 79.99m);
            Console.WriteLine($"Out of stock: {result2.Error}");

            // ── Func<T> factory ────────────────────────────────────────────────
            Console.WriteLine("\n── Func<T> factory for background processor ──");
            var processor = new AdvancedConcepts.BackgroundProcessor(
                () => new GoodCode.InMemoryOrderRepository());

            await processor.ProcessBatchAsync(new[] { Guid.NewGuid(), Guid.NewGuid() });

            // ── Manual wiring without container ────────────────────────────────
            Console.WriteLine("\n── Manual wiring (DIP without IoC container) ──");
            GoodCode.IOrderRepository  manualRepo  = new GoodCode.SqlOrderRepository("Server=prod;DB=orders");
            GoodCode.IEmailService     manualEmail = new GoodCode.SendGridEmailService("sg_key");
            GoodCode.IInventoryService manualInv   = new GoodCode.InMemoryInventoryService(
                new Dictionary<string, int> { ["laptop"] = 5 });

            var manualService = new GoodCode.OrderService(manualRepo, manualEmail, manualInv);
            var manualResult  = await manualService.PlaceOrderAsync("carol", "laptop", 1, 1299.99m);
            Console.WriteLine($"Manual wiring result: {(manualResult.Success ? $"OK {manualResult.OrderId}" : manualResult.Error)}");
        }
    }
}
