// ============================================================
// CONTAINER-BASED DI using Microsoft.Extensions.DependencyInjection
// This is what you use in ASP.NET Core / .NET Worker apps.
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DI.Concepts.BasicConcepts.ContainerDI;

// ── Minimal .NET 6+ console app demonstrating container wiring ────────────────

public static class Program
{
    public static void Main()
    {
        // 1. Build the host (this is the equivalent of Program.cs in ASP.NET Core)
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(RegisterServices)
            .Build();

        // 2. Resolve from the root container — ONLY do this at composition root
        var orderService = host.Services.GetRequiredService<IOrderService>();
        orderService.PlaceOrder(42, 3, "alice@example.com");
    }

    // ── All registrations in ONE place ──────────────────────────────────────
    private static void RegisterServices(IServiceCollection services)
    {
        // Format: services.Add{Lifetime}<Interface, Implementation>()
        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IOrderService, OrderService>();

        // Register a concrete class with no interface (less common)
        services.AddSingleton<DatabaseHealthCheck>();

        // Register with a factory when you need runtime logic during creation
        services.AddSingleton<IEmailService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            logger.Log("Creating SmtpEmailService...");
            return new SmtpEmailService("smtp.prod.com", 587);
        });

        // Register using an existing instance
        var config = new AppConfiguration { SmtpHost = "smtp.prod.com", SmtpPort = 587 };
        services.AddSingleton(config);
    }
}

// ── ASP.NET Core version (Program.cs) ─────────────────────────────────────────
// Shown as comments since it needs the web SDK:
//
// var builder = WebApplication.CreateBuilder(args);
//
// builder.Services.AddSingleton<ILogger, ConsoleLogger>();
// builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
// builder.Services.AddScoped<IOrderService, OrderService>();
//
// var app = builder.Build();
// app.MapGet("/", () => "Hello World");
// app.Run();


// ── Key IServiceCollection methods ────────────────────────────────────────────
public static class RegistrationPatterns
{
    public static void ShowAllPatterns(IServiceCollection services)
    {
        // ── Basic lifetime registrations ──────────────────────────────────────
        services.AddSingleton<IFoo, FooImpl>();
        services.AddScoped<IFoo, FooImpl>();
        services.AddTransient<IFoo, FooImpl>();

        // ── Same type for multiple interfaces (maps both to same instance) ──
        services.AddSingleton<FooImpl>();
        services.AddSingleton<IFoo>(sp => sp.GetRequiredService<FooImpl>());
        services.AddSingleton<IBar>(sp => sp.GetRequiredService<FooImpl>());

        // ── TryAdd — only registers if no registration exists yet ─────────────
        // Useful in libraries so the app can override your default
        services.TryAddSingleton<IFoo, FooImpl>();

        // ── Replace — removes existing registration and adds new one ─────────
        services.AddSingleton<IFoo, FooImpl>();
        services.Replace(ServiceDescriptor.Singleton<IFoo, AltFooImpl>());

        // ── Register multiple implementations for same interface ──────────────
        // All will be injected into IEnumerable<IFoo>
        services.AddSingleton<IFoo, FooImpl>();
        services.AddSingleton<IFoo, AltFooImpl>();
        // Resolved via: IEnumerable<IFoo> or IReadOnlyList<IFoo>

        // ── Keyed services (.NET 8+) ──────────────────────────────────────────
        services.AddKeyedSingleton<IFoo, FooImpl>("primary");
        services.AddKeyedSingleton<IFoo, AltFooImpl>("secondary");
        // Resolved via: [FromKeyedServices("primary")] IFoo foo
    }
}

// ── Resolving services ────────────────────────────────────────────────────────
public class ResolutionPatterns
{
    private readonly IServiceProvider _sp;

    public ResolutionPatterns(IServiceProvider sp) => _sp = sp;

    public void Examples()
    {
        // GetRequiredService — throws InvalidOperationException if not registered
        var foo = _sp.GetRequiredService<IFoo>();

        // GetService — returns null if not registered (use for optional deps)
        var bar = _sp.GetService<IBar>();

        // Resolve all implementations of an interface
        var allFoos = _sp.GetServices<IFoo>();

        // Create a child scope manually (for scoped services in non-web contexts)
        using var scope = _sp.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<IOrderService>();
    }
}

// ── Stubs to make the file compile ────────────────────────────────────────────
public interface IOrderService  { void PlaceOrder(int productId, int qty, string email); }
public interface IOrderRepository { }
public interface IEmailService    { void Send(string to, string subject, string body); }
public interface ILogger          { void Log(string msg); }
public interface IFoo             { }
public interface IBar             { }

public record AppConfiguration    { public string SmtpHost { get; init; } = ""; public int SmtpPort { get; init; } }

public class ConsoleLogger    : ILogger          { public void Log(string msg)  => Console.WriteLine($"[LOG] {msg}"); }
public class SqlOrderRepository : IOrderRepository { }
public class SmtpEmailService : IEmailService
{
    public SmtpEmailService(string host, int port) { }
    public void Send(string to, string subject, string body) { }
}
public class DatabaseHealthCheck { }
public class FooImpl    : IFoo, IBar { }
public class AltFooImpl : IFoo       { }

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IEmailService    _email;
    private readonly ILogger          _logger;

    public OrderService(IOrderRepository repo, IEmailService email, ILogger logger)
    {
        _repo   = repo;
        _email  = email;
        _logger = logger;
    }

    public void PlaceOrder(int productId, int qty, string customerEmail)
    {
        _logger.Log($"Placing order: product={productId}, qty={qty}");
        _email.Send(customerEmail, "Order Placed", "Your order is confirmed.");
    }
}
