// ============================================================
// CASE 04: Background Jobs / Hosted Services with Scoped Dependencies
// THE trickiest DI scenario — background services are Singleton,
// but they often need Scoped services (DbContext, repositories).
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DI.Cases.BackgroundJobs;

// ── The core problem ──────────────────────────────────────────────────────────
// BackgroundService (IHostedService) is always a Singleton in .NET.
// You CANNOT inject Scoped services directly into it.
// You MUST create explicit scopes for each unit of work.

// ── Pattern 1: IServiceScopeFactory injection ─────────────────────────────────
// The safest and most common pattern.
public class OrderCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderCleanupBackgroundService> _logger;

    // IServiceScopeFactory is Singleton — safe to inject into BackgroundService
    public OrderCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order cleanup service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Create a NEW scope for EACH batch of work
        // This is equivalent to a fresh HTTP request context
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Now you can safely resolve Scoped services
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var emailService    = scope.ServiceProvider.GetRequiredService<IEmailService>();

        _logger.LogInformation("Processing expired orders...");
        var expiredOrders = await orderRepository.GetExpiredOrdersAsync(ct);

        foreach (var order in expiredOrders)
        {
            await orderRepository.CancelAsync(order.Id, ct);
            await emailService.SendCancellationNoticeAsync(order.CustomerEmail, order.Id, ct);
            _logger.LogInformation("Cancelled order {OrderId}", order.Id);
        }

        await orderRepository.SaveChangesAsync(ct);
        // Scope disposes here → DbContext is cleaned up properly
    }
}

// ── Pattern 2: Channel-based queue (producer/consumer) ────────────────────────
// Better for high-throughput: HTTP request puts work on a channel,
// background worker picks it up and processes with its own scope.

public interface IOrderProcessingQueue
{
    void Enqueue(int orderId);
    IAsyncEnumerable<int> DequeueAllAsync(CancellationToken ct);
}

public class InMemoryOrderProcessingQueue : IOrderProcessingQueue
{
    private readonly System.Threading.Channels.Channel<int> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<int>();

    public void Enqueue(int orderId) => _channel.Writer.TryWrite(orderId);

    public async IAsyncEnumerable<int> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var orderId in _channel.Reader.ReadAllAsync(ct))
            yield return orderId;
    }
}

public class OrderProcessingWorker : BackgroundService
{
    private readonly IOrderProcessingQueue  _queue;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly ILogger<OrderProcessingWorker> _logger;

    public OrderProcessingWorker(
        IOrderProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var orderId in _queue.DequeueAllAsync(stoppingToken))
        {
            // Each order gets its own scope — independent transaction
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

            try
            {
                await handler.HandleAsync(orderId, stoppingToken);
                _logger.LogInformation("Processed order {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order {OrderId}", orderId);
            }
        }
    }
}

// ── Pattern 3: Scoped handler invoked by background service ──────────────────
// Move all Scoped-dependent logic into a dedicated handler class.
// The background service only deals with scope creation.

public interface IProcessOrderHandler
{
    Task HandleAsync(int orderId, CancellationToken ct = default);
}

public class ProcessOrderHandler : IProcessOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEmailService    _emailService;
    private readonly IInventoryService _inventory;

    // All Scoped — safe because handler is also Scoped
    public ProcessOrderHandler(
        IOrderRepository orderRepository,
        IEmailService    emailService,
        IInventoryService inventory)
    {
        _orderRepository = orderRepository;
        _emailService    = emailService;
        _inventory       = inventory;
    }

    public async Task HandleAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order is null) return;

        await _inventory.ReserveAsync(order.ProductId, order.Quantity, ct);
        await _emailService.SendConfirmationAsync(order.CustomerEmail, orderId, ct);
        await _orderRepository.MarkProcessedAsync(orderId, ct);
        await _orderRepository.SaveChangesAsync(ct);
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class BackgroundJobRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Queue — Singleton (shared between controller and background worker)
        services.AddSingleton<IOrderProcessingQueue, InMemoryOrderProcessingQueue>();

        // Background services — Singleton (registered as hosted services)
        services.AddHostedService<OrderCleanupBackgroundService>();
        services.AddHostedService<OrderProcessingWorker>();

        // Handler — Scoped (created fresh for each job via scope factory)
        services.AddScoped<IProcessOrderHandler, ProcessOrderHandler>();

        // Scoped dependencies of the handler
        services.AddScoped<IOrderRepository,  FakeOrderRepository>();
        services.AddScoped<IEmailService,     FakeEmailService>();
        services.AddScoped<IInventoryService, FakeInventoryService>();

        return services;
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Order>> GetExpiredOrdersAsync(CancellationToken ct = default);
    Task CancelAsync(int id, CancellationToken ct = default);
    Task MarkProcessedAsync(int id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendCancellationNoticeAsync(string email, int orderId, CancellationToken ct = default);
    Task SendConfirmationAsync(string email, int orderId, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task ReserveAsync(int productId, int quantity, CancellationToken ct = default);
}

public record Order(int Id, int ProductId, int Quantity, string CustomerEmail);

public class FakeOrderRepository : IOrderRepository
{
    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Order?>(new Order(id, 1, 1, "test@test.com"));
    public Task<List<Order>> GetExpiredOrdersAsync(CancellationToken ct = default) => Task.FromResult(new List<Order>());
    public Task CancelAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    public Task MarkProcessedAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeEmailService : IEmailService
{
    public Task SendCancellationNoticeAsync(string email, int orderId, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendConfirmationAsync(string email, int orderId, CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeInventoryService : IInventoryService
{
    public Task ReserveAsync(int productId, int quantity, CancellationToken ct = default) => Task.CompletedTask;
}
