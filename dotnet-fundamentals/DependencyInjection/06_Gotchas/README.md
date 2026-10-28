# 06 — Gotchas & Anti-Patterns

## The Big Six

| # | Gotcha | Symptom | Fix |
|---|--------|---------|-----|
| 1 | **Captive dependency** | `ObjectDisposedException` on 2nd request | Change Singleton → Scoped, or use `IServiceScopeFactory` |
| 2 | **Service Locator** | Business logic calls `IServiceProvider.GetService()` | Constructor-inject what you need |
| 3 | **Circular dependency** | `InvalidOperationException` at startup (circular constructor args) | Introduce a mediator / break the cycle |
| 4 | **Transient disposable held by Singleton** | Memory / connection leak | Use `IServiceScopeFactory` inside the Singleton |
| 5 | **Resolving from root provider** | Scoped service never disposed | Always use a scope: `provider.CreateScope()` |
| 6 | **Hidden DI in static classes** | Untestable code, singletons everywhere | Convert to instance classes, inject dependencies |

## Files

| File | Gotcha(s) covered |
|------|--------------------|
| `CaptiveDependency.cs` | Gotcha 1 + 4 + 5 — all captive/lifetime misuse patterns |
| `ServiceLocatorAntiPattern.cs` | Gotcha 2 — what it is and how to refactor away from it |
| `CircularDependency.cs` | Gotcha 3 — detection, patterns that cause it, three fixes |
