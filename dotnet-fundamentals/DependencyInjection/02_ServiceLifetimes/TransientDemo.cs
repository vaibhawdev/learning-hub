// ============================================================
// TRANSIENT lifetime — new instance every time it is resolved
// Best for lightweight, stateless, short-lived operations.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.ServiceLifetimes.Transient;

// ── 1. Validator (stateless, cheap to create) ─────────────────────────────────
public interface IValidator<T>
{
    ValidationResult Validate(T instance);
}

public class CreateOrderValidator : IValidator<CreateOrderRequest>
{
    public ValidationResult Validate(CreateOrderRequest request)
    {
        var errors = new List<string>();

        if (request.ProductId <= 0)
            errors.Add("ProductId must be positive.");
        if (request.Quantity is <= 0 or > 1000)
            errors.Add("Quantity must be between 1 and 1000.");
        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            errors.Add("Customer email is required.");

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}

public record CreateOrderRequest(int ProductId, int Quantity, string CustomerEmail);

public class ValidationResult
{
    public bool IsValid { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = Array.Empty<string>();

    public static ValidationResult Success()             => new() { IsValid = true };
    public static ValidationResult Failure(List<string> errors) => new() { IsValid = false, Errors = errors };
}

// ── 2. Command handler (CQRS style — one-shot, then discarded) ─────────────────
public interface ICommandHandler<TCommand>
{
    Task HandleAsync(TCommand command);
}

public class PlaceOrderCommandHandler : ICommandHandler<CreateOrderRequest>
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService    _emailer;

    public PlaceOrderCommandHandler(IOrderRepository repository, IEmailService emailer)
    {
        _repository = repository;
        _emailer    = emailer;
    }

    public async Task HandleAsync(CreateOrderRequest command)
    {
        var order = new Order(command.ProductId, command.Quantity);
        await _repository.SaveAsync(order);
        await _emailer.SendAsync(command.CustomerEmail, "Order Confirmed", $"Order #{order.Id} placed.");
    }
}

// ── 3. Data mapper / transformer ──────────────────────────────────────────────
public interface IMapper<TSource, TDest>
{
    TDest Map(TSource source);
}

public class OrderToOrderDtoMapper : IMapper<Order, OrderDto>
{
    public OrderDto Map(Order source) => new(source.Id, source.ProductId, source.Quantity, source.CreatedAt);
}

public record OrderDto(int Id, int ProductId, int Quantity, DateTimeOffset CreatedAt);

// ── 4. Template / message builder ─────────────────────────────────────────────
public interface IEmailTemplateBuilder
{
    string BuildOrderConfirmation(Order order, string customerName);
    string BuildPasswordReset(string resetLink);
}

public class HtmlEmailTemplateBuilder : IEmailTemplateBuilder
{
    public string BuildOrderConfirmation(Order order, string customerName)
        => $"<h1>Hi {customerName}</h1><p>Order #{order.Id} confirmed.</p>";

    public string BuildPasswordReset(string resetLink)
        => $"<p>Click <a href='{resetLink}'>here</a> to reset your password.</p>";
}

// ── 5. GUID / ID generator ────────────────────────────────────────────────────
// Transient so tests can replace it with a deterministic fake
public interface IIdGenerator
{
    string NewId();
}

public class GuidIdGenerator : IIdGenerator
{
    public string NewId() => Guid.NewGuid().ToString("N");
}

public class SequentialIdGenerator : IIdGenerator  // for tests
{
    private int _counter = 0;
    public string NewId() => $"ID-{Interlocked.Increment(ref _counter):D6}";
}

// ── GOTCHA: Transient IDisposable captured by a Singleton ─────────────────────
// If a Singleton holds a reference to a Transient that implements IDisposable,
// the Transient will NOT be disposed until the Singleton is disposed (app shutdown).
// This is a memory/resource leak.
//
// Pattern to avoid:
public class ProblematicSingleton  // DON'T do this
{
    // This transient is captured forever even though it should be short-lived
    private readonly IDisposableTransient _transient;

    public ProblematicSingleton(IDisposableTransient transient)
        => _transient = transient;
}

// FIX: Inject a factory instead of the transient directly
public class CorrectSingleton
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CorrectSingleton(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public void DoWork()
    {
        using var scope = _scopeFactory.CreateScope();
        var transient = scope.ServiceProvider.GetRequiredService<IDisposableTransient>();
        transient.Execute();
        // transient is disposed when scope exits
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class TransientRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        services.AddTransient<IValidator<CreateOrderRequest>, CreateOrderValidator>();
        services.AddTransient<ICommandHandler<CreateOrderRequest>, PlaceOrderCommandHandler>();
        services.AddTransient<IMapper<Order, OrderDto>, OrderToOrderDtoMapper>();
        services.AddTransient<IEmailTemplateBuilder, HtmlEmailTemplateBuilder>();
        services.AddTransient<IIdGenerator, GuidIdGenerator>();

        return services;
    }
}

// Stubs
public interface IOrderRepository { Task SaveAsync(Order o); }
public interface IEmailService    { Task SendAsync(string to, string sub, string body); }
public interface IDisposableTransient : IDisposable { void Execute(); }

public class Order
{
    public int           Id         { get; set; }
    public int           ProductId  { get; set; }
    public int           Quantity   { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order(int productId, int qty)
    {
        ProductId = productId;
        Quantity  = qty;
    }
}
