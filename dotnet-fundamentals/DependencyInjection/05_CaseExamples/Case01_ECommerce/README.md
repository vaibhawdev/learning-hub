# Case 01 — E-Commerce Order Placement

## Scenario
An e-commerce API needs to handle order placement with:
- Inventory check
- Payment processing (multiple gateways)
- Notification (email + SMS)
- Audit logging

## DI Decisions

| Service | Lifetime | Why |
|---------|----------|-----|
| `IOrderRepository` | Scoped | Wraps DbContext; one transaction per request |
| `IInventoryService` | Scoped | Reads/writes inventory in same DB transaction |
| `IPaymentGateway` | Singleton (Keyed) | Stateless HTTP client, expensive to instantiate |
| `INotificationService` | Transient | Stateless, fire-and-forget |
| `IAuditService` | Scoped | Collects events for the whole request, flushes at end |
| `ICurrentUserService` | Scoped | Reads from HTTP context, per-request |

## Files

- `OrderDomain.cs` — entities and value objects
- `OrderServices.cs` — all service interfaces and implementations
- `OrderRegistration.cs` — DI wiring
- `OrderController.cs` — how the API endpoint uses injected services
