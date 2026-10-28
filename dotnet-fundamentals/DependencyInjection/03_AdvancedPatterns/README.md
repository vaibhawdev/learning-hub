# 03 — Advanced DI Patterns

## Patterns Covered

| File | Pattern | When to reach for it |
|------|---------|----------------------|
| `OpenGenerics.cs` | Open generic registration | Apply same logic to many types (validation, logging, caching) |
| `DecoratorPattern.cs` | Decorator via Scrutor | Add cross-cutting behaviour (logging, retry, caching) without touching the original class |
| `FactoryPattern.cs` | Factory / IServiceScopeFactory | Create services at runtime based on conditions |
| `KeyedServices.cs` | Named/Keyed services (.NET 8+) | Multiple implementations of same interface, resolved by key |
| `MultipleImplementations.cs` | `IEnumerable<T>` injection | Strategy/pipeline pattern |
| `ConventionRegistration.cs` | Assembly scanning via Scrutor | Register all services matching a convention automatically |

---

## Open Generics

Register `IValidator<T>` once, get a specific validator for each `T`:

```csharp
services.AddTransient(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));
```

---

## Decorator Pattern

Add caching in front of your repository without changing the repository:

```
IOrderRepository
    ↑
CachedOrderRepository   (added via decorator)
    ↑
SqlOrderRepository       (original)
```

Built-in DI does not support decorators natively — use **Scrutor** NuGet:

```csharp
services.AddScoped<IOrderRepository, SqlOrderRepository>();
services.Decorate<IOrderRepository, CachedOrderRepository>();
```

---

## Keyed Services (.NET 8+)

```csharp
services.AddKeyedSingleton<IPaymentGateway, StripeGateway>("stripe");
services.AddKeyedSingleton<IPaymentGateway, PayPalGateway>("paypal");

// In controller / service:
public MyController([FromKeyedServices("stripe")] IPaymentGateway gateway) { }
```

---

## IEnumerable<T> — Strategy / Pipeline

Register multiple implementations, inject all of them:

```csharp
services.AddTransient<IOrderProcessor, InventoryCheckProcessor>();
services.AddTransient<IOrderProcessor, FraudCheckProcessor>();
services.AddTransient<IOrderProcessor, PaymentProcessor>();

// In your handler:
public class OrderPipeline
{
    public OrderPipeline(IEnumerable<IOrderProcessor> processors) { }
}
// All three are injected in registration order.
```
