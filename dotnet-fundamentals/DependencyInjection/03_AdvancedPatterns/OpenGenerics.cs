// ============================================================
// OPEN GENERIC REGISTRATION
// Register IHandler<T> once and resolve IHandler<OrderRequest>,
// IHandler<UserRequest>, etc. automatically.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.AdvancedPatterns.OpenGenerics;

// ── Problem without open generics ─────────────────────────────────────────────
// You'd need to register every concrete validator manually:
//   services.AddTransient<IValidator<OrderRequest>, OrderRequestValidator>();
//   services.AddTransient<IValidator<UserRequest>,  UserRequestValidator>();
//   services.AddTransient<IValidator<ProductRequest>, ProductRequestValidator>();
// ... and so on for 50 types.

// ── Solution: Open generic registration ───────────────────────────────────────

// 1. Define the open generic interface
public interface IValidator<T>
{
    ValidationResult Validate(T instance);
}

// 2. Define the open generic implementation
// This uses DataAnnotations attributes — works for ANY T without extra code
public class DataAnnotationsValidator<T> : IValidator<T>
{
    public ValidationResult Validate(T instance)
    {
        if (instance is null)
            return ValidationResult.Failure(["Instance cannot be null."]);

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(instance);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        bool isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            instance, context, results, validateAllProperties: true);

        return isValid
            ? ValidationResult.Success()
            : ValidationResult.Failure(results.Select(r => r.ErrorMessage ?? "Invalid").ToList());
    }
}

// 3. You can also have type-specific overrides
public class OrderRequestValidator : IValidator<OrderRequest>
{
    public ValidationResult Validate(OrderRequest instance)
    {
        // Custom rules beyond data annotations
        if (instance.Quantity > 100 && !instance.IsWholesaleAccount)
            return ValidationResult.Failure(["Large orders require a wholesale account."]);

        return ValidationResult.Success();
    }
}

// ── CQRS Pipeline with open generics ─────────────────────────────────────────

// Open generic handler interface
public interface IRequestHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct = default);
}

// Open generic pipeline behaviour (like MediatR's IPipelineBehavior)
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        Func<Task<TResponse>> next,
        CancellationToken ct = default);
}

// A logging behaviour that applies to ALL request types via open generics
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Func<Task<TResponse>> next, CancellationToken ct = default)
    {
        Console.WriteLine($"[PIPELINE] Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"[PIPELINE] Handled {typeof(TRequest).Name}");
        return response;
    }
}

// A validation behaviour (also open generic)
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public ValidationBehavior(IValidator<TRequest> validator) => _validator = validator;

    public async Task<TResponse> HandleAsync(
        TRequest request, Func<Task<TResponse>> next, CancellationToken ct = default)
    {
        var result = _validator.Validate(request);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);

        return await next();
    }
}

// Concrete request/response types
public record OrderRequest(int ProductId, int Quantity, bool IsWholesaleAccount);
public record OrderResponse(int OrderId, string Status);

public class CreateOrderHandler : IRequestHandler<OrderRequest, OrderResponse>
{
    public Task<OrderResponse> HandleAsync(OrderRequest request, CancellationToken ct = default)
        => Task.FromResult(new OrderResponse(42, "Confirmed"));
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class OpenGenericRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Register open generic validator — covers ALL types automatically
        services.AddTransient(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));

        // Override for a specific type (this takes precedence)
        services.AddTransient<IValidator<OrderRequest>, OrderRequestValidator>();

        // Open generic pipeline behaviours
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Concrete handlers
        services.AddTransient<IRequestHandler<OrderRequest, OrderResponse>, CreateOrderHandler>();

        return services;
    }
}

// ── Resolution demo ───────────────────────────────────────────────────────────
public class OpenGenericResolutionDemo
{
    private readonly IServiceProvider _sp;

    public OpenGenericResolutionDemo(IServiceProvider sp) => _sp = sp;

    public void Demonstrate()
    {
        // Each of these works automatically from the single open generic registration
        var orderValidator   = _sp.GetRequiredService<IValidator<OrderRequest>>();
        var productValidator = _sp.GetRequiredService<IValidator<ProductRequest>>();
        var userValidator    = _sp.GetRequiredService<IValidator<UserRequest>>();

        Console.WriteLine($"Got: {orderValidator.GetType().Name}");
        Console.WriteLine($"Got: {productValidator.GetType().Name}");
        Console.WriteLine($"Got: {userValidator.GetType().Name}");
    }
}

// Supporting types
public record ProductRequest(string Name, decimal Price);
public record UserRequest(string Email, string Password);

public class ValidationResult
{
    public bool IsValid { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = Array.Empty<string>();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(IEnumerable<string> errors)
        => new() { IsValid = false, Errors = errors.ToArray() };
}

public class ValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public ValidationException(IReadOnlyList<string> errors)
        : base(string.Join("; ", errors)) => Errors = errors;
}
