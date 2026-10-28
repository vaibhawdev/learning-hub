// ============================================================
// MULTIPLE IMPLEMENTATIONS — IEnumerable<T> injection
// Strategy pattern, pipeline processing, notification fanout.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.AdvancedPatterns.MultipleImplementations;

// ── Pattern 1: Notification fanout ────────────────────────────────────────────
// Send an order confirmation via ALL registered channels simultaneously.

public interface INotificationChannel
{
    string ChannelName { get; }
    Task NotifyAsync(string recipient, string message);
}

public class EmailNotificationChannel : INotificationChannel
{
    public string ChannelName => "Email";
    public async Task NotifyAsync(string recipient, string message)
    {
        Console.WriteLine($"[EMAIL → {recipient}] {message}");
        await Task.Delay(20);
    }
}

public class SmsNotificationChannel : INotificationChannel
{
    public string ChannelName => "SMS";
    public async Task NotifyAsync(string recipient, string message)
    {
        Console.WriteLine($"[SMS → {recipient}] {message}");
        await Task.Delay(15);
    }
}

public class PushNotificationChannel : INotificationChannel
{
    public string ChannelName => "Push";
    public async Task NotifyAsync(string recipient, string message)
    {
        Console.WriteLine($"[PUSH → {recipient}] {message}");
        await Task.Delay(10);
    }
}

// Inject ALL channels — sends via every registered channel
public class NotificationService
{
    private readonly IEnumerable<INotificationChannel> _channels;

    public NotificationService(IEnumerable<INotificationChannel> channels)
        => _channels = channels;

    public async Task SendToAllAsync(string recipient, string message)
    {
        var tasks = _channels.Select(c => c.NotifyAsync(recipient, message));
        await Task.WhenAll(tasks);
    }
}

// ── Pattern 2: Order processing pipeline (chain of responsibility) ────────────
public interface IOrderProcessor
{
    string Name { get; }
    int Priority { get; }  // lower = runs first
    Task<ProcessingResult> ProcessAsync(OrderContext context);
}

public record OrderContext(int OrderId, int ProductId, int Quantity, string CustomerEmail)
{
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public record ProcessingResult(bool Success, string? ErrorMessage = null);

public class InventoryCheckProcessor : IOrderProcessor
{
    public string Name     => "InventoryCheck";
    public int    Priority => 10;

    public Task<ProcessingResult> ProcessAsync(OrderContext context)
    {
        if (context.Quantity > 50)
        {
            context.HasError     = true;
            context.ErrorMessage = "Insufficient stock.";
            return Task.FromResult(new ProcessingResult(false, "Insufficient stock."));
        }
        Console.WriteLine($"[{Name}] Stock OK for order {context.OrderId}");
        return Task.FromResult(new ProcessingResult(true));
    }
}

public class FraudCheckProcessor : IOrderProcessor
{
    public string Name     => "FraudCheck";
    public int    Priority => 20;

    public Task<ProcessingResult> ProcessAsync(OrderContext context)
    {
        Console.WriteLine($"[{Name}] No fraud detected for order {context.OrderId}");
        return Task.FromResult(new ProcessingResult(true));
    }
}

public class PaymentProcessor : IOrderProcessor
{
    public string Name     => "Payment";
    public int    Priority => 30;

    public Task<ProcessingResult> ProcessAsync(OrderContext context)
    {
        Console.WriteLine($"[{Name}] Payment captured for order {context.OrderId}");
        return Task.FromResult(new ProcessingResult(true));
    }
}

// Orchestrator: runs all processors in priority order, stops on failure
public class OrderOrchestrator
{
    private readonly IEnumerable<IOrderProcessor> _processors;

    public OrderOrchestrator(IEnumerable<IOrderProcessor> processors)
        => _processors = processors;

    public async Task<ProcessingResult> ProcessAsync(OrderContext context)
    {
        foreach (var processor in _processors.OrderBy(p => p.Priority))
        {
            var result = await processor.ProcessAsync(context);
            if (!result.Success)
            {
                Console.WriteLine($"Pipeline halted at '{processor.Name}': {result.ErrorMessage}");
                return result;
            }
        }
        return new ProcessingResult(true);
    }
}

// ── Pattern 3: Health checks pipeline ────────────────────────────────────────
public interface IHealthCheck
{
    string CheckName { get; }
    Task<HealthStatus> CheckAsync();
}

public record HealthStatus(string CheckName, bool IsHealthy, string? Details = null);

public class DatabaseHealthCheck : IHealthCheck
{
    public string CheckName => "Database";
    public async Task<HealthStatus> CheckAsync()
    {
        await Task.Delay(10);
        return new HealthStatus(CheckName, IsHealthy: true, "Responding in 10ms");
    }
}

public class RedisHealthCheck : IHealthCheck
{
    public string CheckName => "Redis";
    public async Task<HealthStatus> CheckAsync()
    {
        await Task.Delay(5);
        return new HealthStatus(CheckName, IsHealthy: true, "Connected");
    }
}

public class ExternalApiHealthCheck : IHealthCheck
{
    public string CheckName => "ExternalAPI";
    public async Task<HealthStatus> CheckAsync()
    {
        await Task.Delay(100);
        return new HealthStatus(CheckName, IsHealthy: false, "Timeout after 100ms");
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class MultipleImplementationsRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Each Add adds to the list — all three injected into IEnumerable<INotificationChannel>
        services.AddTransient<INotificationChannel, EmailNotificationChannel>();
        services.AddTransient<INotificationChannel, SmsNotificationChannel>();
        services.AddTransient<INotificationChannel, PushNotificationChannel>();
        services.AddTransient<NotificationService>();

        // Order processors
        services.AddTransient<IOrderProcessor, InventoryCheckProcessor>();
        services.AddTransient<IOrderProcessor, FraudCheckProcessor>();
        services.AddTransient<IOrderProcessor, PaymentProcessor>();
        services.AddTransient<OrderOrchestrator>();

        // Health checks
        services.AddTransient<IHealthCheck, DatabaseHealthCheck>();
        services.AddTransient<IHealthCheck, RedisHealthCheck>();
        services.AddTransient<IHealthCheck, ExternalApiHealthCheck>();

        return services;
    }
}

// ── GOTCHA: GetService<IFoo>() only returns the LAST registered implementation ─
// Always use GetServices<IFoo>() or IEnumerable<IFoo> to get ALL.
public class ResolutionDemo
{
    public static void Show(IServiceProvider sp)
    {
        // Returns ONLY the last registered INotificationChannel (PushNotificationChannel)
        var single = sp.GetService<INotificationChannel>();

        // Returns ALL three channels
        var all = sp.GetServices<INotificationChannel>();
        Console.WriteLine($"Single: {single?.GetType().Name}");
        Console.WriteLine($"All: {string.Join(", ", all.Select(c => c.GetType().Name))}");
    }
}
