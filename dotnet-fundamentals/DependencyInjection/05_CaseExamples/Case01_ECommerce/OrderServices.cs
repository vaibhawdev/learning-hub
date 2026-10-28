// ============================================================
// CASE 01: E-Commerce Order Placement
// Real-world DI with multiple service lifetimes working together
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Cases.ECommerce;

// ── Domain models ─────────────────────────────────────────────────────────────
public record OrderId(Guid Value);
public record Money(decimal Amount, string Currency);

public class Order
{
    public OrderId    Id          { get; init; } = new(Guid.NewGuid());
    public int        ProductId   { get; init; }
    public int        Quantity    { get; init; }
    public Money      Total       { get; init; } = new(0, "USD");
    public string     CustomerId  { get; init; } = "";
    public OrderStatus Status     { get; set; } = OrderStatus.Pending;
}

public enum OrderStatus { Pending, PaymentProcessed, Confirmed, Failed }

// ── Interfaces ────────────────────────────────────────────────────────────────
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id);
    Task SaveAsync(Order order);
    Task UpdateStatusAsync(OrderId id, OrderStatus status);
}

public interface IInventoryService
{
    Task<bool> CheckAvailabilityAsync(int productId, int quantity);
    Task ReserveAsync(int productId, int quantity);
    Task ReleaseAsync(int productId, int quantity);
}

public interface IPaymentGateway
{
    string GatewayName { get; }
    Task<PaymentResult> ChargeAsync(string customerId, Money amount);
}

public record PaymentResult(bool Success, string TransactionId, string? ErrorMessage = null);

public interface INotificationService
{
    Task SendOrderConfirmationAsync(string customerEmail, Order order);
    Task SendPaymentFailureAsync(string customerEmail, Order order, string reason);
}

public interface IAuditService
{
    void Log(string action, string entityId, object? details = null);
    Task FlushAsync();  // called at end of request
}

public interface ICurrentUserService
{
    string CustomerId { get; }
    string Email      { get; }
}

// ── Orchestrator — the main order placement service ───────────────────────────
public interface IOrderPlacementService
{
    Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderCommand command);
}

public record PlaceOrderCommand(int ProductId, int Quantity, string PaymentGateway);
public record PlaceOrderResult(bool Success, string? OrderId, string? ErrorMessage);

public class OrderPlacementService : IOrderPlacementService
{
    private readonly IOrderRepository    _orders;
    private readonly IInventoryService   _inventory;
    private readonly IKeyedServiceProvider _gateways;
    private readonly INotificationService _notifications;
    private readonly IAuditService       _audit;
    private readonly ICurrentUserService _currentUser;

    public OrderPlacementService(
        IOrderRepository     orders,
        IInventoryService    inventory,
        IKeyedServiceProvider gateways,
        INotificationService notifications,
        IAuditService        audit,
        ICurrentUserService  currentUser)
    {
        _orders        = orders;
        _inventory     = inventory;
        _gateways      = gateways;
        _notifications = notifications;
        _audit         = audit;
        _currentUser   = currentUser;
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderCommand command)
    {
        _audit.Log("PlaceOrder.Start", _currentUser.CustomerId, command);

        // 1. Check inventory
        var available = await _inventory.CheckAvailabilityAsync(command.ProductId, command.Quantity);
        if (!available)
        {
            _audit.Log("PlaceOrder.InventoryFailed", _currentUser.CustomerId);
            return new PlaceOrderResult(false, null, "Insufficient stock.");
        }

        // 2. Reserve stock
        await _inventory.ReserveAsync(command.ProductId, command.Quantity);

        // 3. Create order
        var order = new Order
        {
            ProductId  = command.ProductId,
            Quantity   = command.Quantity,
            CustomerId = _currentUser.CustomerId,
            Total      = new Money(command.Quantity * 9.99m, "USD"),
            Status     = OrderStatus.Pending
        };
        await _orders.SaveAsync(order);

        // 4. Process payment (keyed service — resolved at runtime)
        var gateway = _gateways.GetRequiredKeyedService<IPaymentGateway>(command.PaymentGateway);
        var payment = await gateway.ChargeAsync(_currentUser.CustomerId, order.Total);

        if (!payment.Success)
        {
            await _inventory.ReleaseAsync(command.ProductId, command.Quantity);
            await _orders.UpdateStatusAsync(order.Id, OrderStatus.Failed);
            await _notifications.SendPaymentFailureAsync(_currentUser.Email, order, payment.ErrorMessage!);
            _audit.Log("PlaceOrder.PaymentFailed", order.Id.Value.ToString(), payment);
            return new PlaceOrderResult(false, null, $"Payment failed: {payment.ErrorMessage}");
        }

        // 5. Confirm order
        await _orders.UpdateStatusAsync(order.Id, OrderStatus.Confirmed);
        await _notifications.SendOrderConfirmationAsync(_currentUser.Email, order);
        _audit.Log("PlaceOrder.Success", order.Id.Value.ToString(), new { payment.TransactionId });

        return new PlaceOrderResult(true, order.Id.Value.ToString(), null);
    }
}

// ── Implementations ───────────────────────────────────────────────────────────
public class SqlOrderRepository : IOrderRepository
{
    // In real code: inject AppDbContext
    public Task<Order?> GetByIdAsync(OrderId id)         => Task.FromResult<Order?>(null);
    public Task SaveAsync(Order order)                   { Console.WriteLine($"[SQL] Saving order {order.Id.Value}"); return Task.CompletedTask; }
    public Task UpdateStatusAsync(OrderId id, OrderStatus s) { Console.WriteLine($"[SQL] Order {id.Value} → {s}"); return Task.CompletedTask; }
}

public class InventoryService : IInventoryService
{
    public Task<bool> CheckAvailabilityAsync(int productId, int qty) { Console.WriteLine($"[INV] Checking {qty} of product {productId}"); return Task.FromResult(true); }
    public Task ReserveAsync(int productId, int qty) { Console.WriteLine($"[INV] Reserved {qty}"); return Task.CompletedTask; }
    public Task ReleaseAsync(int productId, int qty) { Console.WriteLine($"[INV] Released {qty}"); return Task.CompletedTask; }
}

public class StripePaymentGateway : IPaymentGateway
{
    public string GatewayName => "stripe";
    public async Task<PaymentResult> ChargeAsync(string customerId, Money amount)
    {
        Console.WriteLine($"[Stripe] Charging {amount.Amount} {amount.Currency}");
        await Task.Delay(50);
        return new PaymentResult(true, "stripe_" + Guid.NewGuid().ToString("N")[..8]);
    }
}

public class PayPalPaymentGateway : IPaymentGateway
{
    public string GatewayName => "paypal";
    public async Task<PaymentResult> ChargeAsync(string customerId, Money amount)
    {
        Console.WriteLine($"[PayPal] Charging {amount.Amount} {amount.Currency}");
        await Task.Delay(70);
        return new PaymentResult(true, "pp_" + Guid.NewGuid().ToString("N")[..8]);
    }
}

public class MultiChannelNotificationService : INotificationService
{
    public Task SendOrderConfirmationAsync(string email, Order order)
    { Console.WriteLine($"[EMAIL] Order {order.Id.Value} confirmed → {email}"); return Task.CompletedTask; }
    public Task SendPaymentFailureAsync(string email, Order order, string reason)
    { Console.WriteLine($"[EMAIL] Payment failed for order {order.Id.Value} → {email}: {reason}"); return Task.CompletedTask; }
}

public class RequestAuditService : IAuditService
{
    private readonly List<string> _events = new();
    public void Log(string action, string entityId, object? details = null)
        => _events.Add($"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}] {action} | {entityId}");
    public Task FlushAsync()
    { Console.WriteLine($"[AUDIT] Flushing {_events.Count} events"); return Task.CompletedTask; }
}

public class HttpContextCurrentUserService : ICurrentUserService
{
    // Real impl: inject IHttpContextAccessor
    public string CustomerId => "cust_001";
    public string Email      => "alice@example.com";
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class ECommerceRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Scoped — per-request state
        services.AddScoped<IOrderRepository,      SqlOrderRepository>();
        services.AddScoped<IInventoryService,     InventoryService>();
        services.AddScoped<IAuditService,         RequestAuditService>();
        services.AddScoped<ICurrentUserService,   HttpContextCurrentUserService>();
        services.AddScoped<IOrderPlacementService,OrderPlacementService>();

        // Transient — stateless, short-lived
        services.AddTransient<INotificationService, MultiChannelNotificationService>();

        // Singleton + Keyed — stateless HTTP clients
        services.AddKeyedSingleton<IPaymentGateway, StripePaymentGateway>("stripe");
        services.AddKeyedSingleton<IPaymentGateway, PayPalPaymentGateway>("paypal");

        return services;
    }
}
