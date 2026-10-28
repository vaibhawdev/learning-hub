// ============================================================
// CIRCULAR DEPENDENCIES — detection and three ways to fix them
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Gotchas.CircularDependency;

// ── What is a circular dependency? ───────────────────────────────────────────
// A → B → A
// When the container tries to create A, it needs B.
// To create B, it needs A, which it is still creating.
// The container throws: "A circular dependency was detected."

// ── Common circular dependency patterns ──────────────────────────────────────

// Pattern 1: Direct cycle
// OrderService needs EmailService, EmailService needs OrderService (bad design)
public class OrderService_Circular
{
    private readonly IEmailService_Circular _emailService;
    public OrderService_Circular(IEmailService_Circular emailService)
        => _emailService = emailService;
}

public class EmailService_Circular
{
    private readonly IOrderService_Circular _orderService;
    public EmailService_Circular(IOrderService_Circular orderService)  // circular!
        => _orderService = orderService;
}

public interface IOrderService_Circular { }
public interface IEmailService_Circular { }

// Pattern 2: Indirect cycle
// A → B → C → A (container still detects this)

// ── FIX 1: Break the cycle by extracting a shared abstraction ─────────────────
// If both services need the same data, extract a third service that holds it.
// This is usually the "right" fix — the cycle indicates a design problem.

// OrderService needs to know if an email was sent.
// EmailService needs an order to format the email.
// Solution: neither should depend on the other — extract a domain event.

public interface IOrderRepository    { Task<Order?> FindAsync(int id); }
public interface IEmailTemplateService { string BuildOrderEmail(Order order); }
public interface INotificationDispatcher { Task SendAsync(string to, string body); }

public class OrderService_Fixed
{
    private readonly IOrderRepository         _orders;
    private readonly INotificationDispatcher  _dispatcher;
    private readonly IEmailTemplateService    _templates;

    public OrderService_Fixed(
        IOrderRepository orders,
        INotificationDispatcher dispatcher,
        IEmailTemplateService templates)
    {
        _orders     = orders;
        _dispatcher = dispatcher;
        _templates  = templates;
    }

    public async Task PlaceAndNotifyAsync(int productId)
    {
        var order = new Order(productId);
        var body  = _templates.BuildOrderEmail(order);
        await _dispatcher.SendAsync("customer@example.com", body);
    }
}

// EmailTemplateService only formats text — no dependency on OrderService
public class EmailTemplateService : IEmailTemplateService
{
    public string BuildOrderEmail(Order order) => $"Your order #{order.Id} is confirmed.";
}

// ── FIX 2: Lazy<T> — defer instantiation of one side ─────────────────────────
// Use when you can't easily restructure and one dependency is rarely used.

public class ServiceA
{
    private readonly Lazy<IServiceB> _b;

    // Lazy<IServiceB> is injected — the container provides a Lazy wrapper
    // ServiceB is not actually created until _b.Value is first accessed
    public ServiceA(Lazy<IServiceB> b) => _b = b;

    public void UseB() => _b.Value.DoSomething();  // created on first call
}

public class ServiceB : IServiceB
{
    private readonly IServiceA _a;
    public ServiceB(IServiceA a) => _a = a;  // now ServiceA is already constructed
    public void DoSomething() => Console.WriteLine("[ServiceB] DoSomething()");
}

public interface IServiceA { }
public interface IServiceB { void DoSomething(); }

// Register Lazy<T> factory (built-in DI doesn't support Lazy<T> natively — use a factory):
// services.AddTransient<Lazy<IServiceB>>(sp => new Lazy<IServiceB>(sp.GetRequiredService<IServiceB>));

// ── FIX 3: Property injection or method injection ─────────────────────────────
// One side uses constructor injection, the other uses property injection.
// Requires a DI container that supports property injection (Autofac, SimpleInjector).
// NOT recommended for built-in .NET DI — use it only if Fix 1 and Fix 2 are not feasible.

public class ServiceC
{
    private readonly IServiceD _d;
    public ServiceC(IServiceD d) => _d = d;
}

public class ServiceD : IServiceD
{
    // Property injection — set AFTER construction, breaks the cycle
    public IServiceC? ServiceC { get; set; }
}

public interface IServiceC { }
public interface IServiceD { }

// ── FIX 4: Mediator pattern ───────────────────────────────────────────────────
// If two services need to communicate, put a mediator in between.
// Neither depends on the other — both depend on the mediator.
// This is how MediatR breaks cycles: services publish events, others subscribe.

public interface IDomainEventBus
{
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
    void Subscribe<T>(Func<T, Task> handler) where T : IDomainEvent;
}

public interface IDomainEvent { }
public record OrderPlacedEvent(int OrderId, string CustomerEmail) : IDomainEvent;

// OrderService publishes an event — no dependency on EmailService
public class OrderService_WithMediator
{
    private readonly IOrderRepository _orders;
    private readonly IDomainEventBus  _events;

    public OrderService_WithMediator(IOrderRepository orders, IDomainEventBus events)
    {
        _orders = orders;
        _events = events;
    }

    public async Task PlaceOrderAsync(int productId, string customerEmail)
    {
        var order = new Order(productId);
        await _events.PublishAsync(new OrderPlacedEvent(order.Id, customerEmail));
    }
}

// EmailService subscribes to the event — no dependency on OrderService
public class EmailService_WithMediator
{
    public EmailService_WithMediator(IDomainEventBus events)
    {
        events.Subscribe<OrderPlacedEvent>(async e =>
        {
            Console.WriteLine($"[Email] Sending confirmation for order {e.OrderId} to {e.CustomerEmail}");
            await Task.CompletedTask;
        });
    }
}

// ── How to detect cycles before they happen ──────────────────────────────────
// 1. Enable ValidateOnBuild:
//    builder.Host.UseDefaultServiceProvider(o => o.ValidateOnBuild = true);
//    Circular deps throw at startup, not in production under load.
//
// 2. Draw the dependency graph. If any arrow forms a cycle, redesign.
//
// 3. Rule of thumb: if Service A and Service B both need each other,
//    something in the middle WANTS to exist. Extract it.

// Domain models
public record Order(int ProductId) { public int Id { get; } = new Random().Next(1000); }
