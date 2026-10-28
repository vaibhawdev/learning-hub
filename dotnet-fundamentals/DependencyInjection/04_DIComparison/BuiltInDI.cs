// ============================================================
// MICROSOFT.EXTENSIONS.DEPENDENCYINJECTION — Built-in DI
// Full feature tour with real-world patterns.
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;  // TryAdd, Replace

namespace DI.Concepts.Comparison.BuiltIn;

// ── Strengths of built-in DI ──────────────────────────────────────────────────
// 1. Zero extra dependencies in ASP.NET Core projects
// 2. Tightly integrated with IConfiguration, ILogger, IHostedService
// 3. Best performance of any .NET DI container
// 4. Excellent scope validation in development

public static class BuiltInDIShowcase
{
    public static void RegisterEverything(IServiceCollection services)
    {
        // ── Basic registrations ───────────────────────────────────────────────
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddTransient<IEmailSender, SmtpEmailSender>();

        // ── Register with factory (runtime logic) ─────────────────────────────
        services.AddSingleton<ISmtpClient>(sp =>
        {
            var config = sp.GetRequiredService<EmailConfig>();
            return new SmtpClientWrapper(config.Host, config.Port);
        });

        // ── Register concrete type with multiple interfaces ────────────────────
        // Ensures the SAME INSTANCE serves both interfaces (as Singleton)
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<EventBus>());
        services.AddSingleton<IEventSubscriber>(sp => sp.GetRequiredService<EventBus>());

        // ── TryAdd — library-friendly registration (won't override app's choice) ─
        services.TryAddSingleton<ICacheService, InMemoryCacheService>();  // skipped if already registered
        services.TryAddEnumerable(ServiceDescriptor.Transient<IValidator, NotEmptyValidator>());

        // ── Replace — override a previously registered service ────────────────
        services.Replace(ServiceDescriptor.Singleton<ICacheService, RedisCacheService>());

        // ── Open generic ─────────────────────────────────────────────────────
        services.AddTransient(typeof(IRepository<>), typeof(EfRepository<>));

        // ── Keyed services (.NET 8+) ──────────────────────────────────────────
        services.AddKeyedSingleton<IEmailSender, SmtpEmailSender>("transactional");
        services.AddKeyedSingleton<IEmailSender, SendGridEmailSender>("marketing");

        // ── Register options (configuration binding) ──────────────────────────
        // services.Configure<EmailConfig>(configuration.GetSection("Email"));
        // services.AddOptions<EmailConfig>()
        //     .BindConfiguration("Email")
        //     .ValidateDataAnnotations();

        // ── Hosted services (background jobs) ────────────────────────────────
        services.AddHostedService<OrderProcessingBackgroundService>();
    }

    // ── Scope validation (enable in development) ─────────────────────────────
    // builder.Host.UseDefaultServiceProvider(o =>
    // {
    //     o.ValidateScopes  = true;   // detect captive dependency at request time
    //     o.ValidateOnBuild = true;   // catch missing registrations at startup
    // });
}

// ── IServiceCollection extension methods — keep Program.cs clean ─────────────
// Group related registrations into extension methods per feature/module
public static class OrderServiceExtensions
{
    public static IServiceCollection AddOrderServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddTransient<IOrderValidator, OrderValidator>();
        return services;
    }
}

public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, EmailConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton<ISmtpClient>(sp => new SmtpClientWrapper(config.Host, config.Port));
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        return services;
    }
}

// Usage in Program.cs:
//   builder.Services.AddOrderServices();
//   builder.Services.AddEmailServices(new EmailConfig { Host = "smtp.prod.com", Port = 587 });

// ── LIMITATIONS of built-in DI ────────────────────────────────────────────────
// 1. No property injection (must use constructor)
// 2. Decorator support only via Scrutor (no native API before .NET 8)
// 3. No assembly scanning (register each service explicitly or use Scrutor)
// 4. Named services only in .NET 8+ (basic key = object, not full named)
// 5. No interception / AOP
// 6. ValidateOnBuild only catches missing registrations, not lifetime mismatches
//    at build time (unless all constructors are inspected — happens at first resolve)

// ── Stub types ────────────────────────────────────────────────────────────────
public interface ICacheService   { }
public interface IOrderService   { }
public interface IEmailSender    { }
public interface ISmtpClient     { }
public interface IEventPublisher { }
public interface IEventSubscriber{ }
public interface IValidator      { }
public interface IOrderRepository{ }
public interface IOrderValidator { }
public interface IRepository<T>  { }

public record EmailConfig { public string Host { get; init; } = ""; public int Port { get; init; } }

public class InMemoryCacheService : ICacheService { }
public class RedisCacheService    : ICacheService { }
public class OrderService : IOrderService
{
    public OrderService(IOrderRepository repo, IEmailSender email, IOrderValidator validator) { }
}
public class SmtpEmailSender      : IEmailSender     { }
public class SendGridEmailSender   : IEmailSender     { }
public class SmtpClientWrapper    : ISmtpClient
{
    public SmtpClientWrapper(string host, int port) { }
}
public class EventBus : IEventPublisher, IEventSubscriber { }
public class SqlOrderRepository : IOrderRepository { }
public class OrderValidator     : IOrderValidator  { }
public class EfRepository<T>    : IRepository<T>   { }
public class NotEmptyValidator  : IValidator       { }

public class OrderProcessingBackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
