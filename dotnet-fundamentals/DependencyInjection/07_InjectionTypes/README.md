# 07 — Injection Types & What Can Go Into What

## The Three Injection Styles

| Style | Support in built-in DI | Use |
|-------|----------------------|-----|
| **Constructor** | Full | Required dependencies — always prefer |
| **Property** | Manual only (via factory) | Optional / legacy scenarios |
| **Method** | N/A (caller passes it) | One-method-only dependency |

---

## What Can Be Injected Into What (full table)

```
┌─────────────────────────────────────────────────────────┐
│             CONSUMER LIFETIME                           │
│  Dependency  │ Singleton  │  Scoped   │  Transient      │
├──────────────┼────────────┼───────────┼─────────────────┤
│ Singleton    │    ✅      │    ✅     │     ✅           │
│ Scoped       │ ❌ CAPTIVE │    ✅     │     ✅           │
│ Transient    │ ⚠️ captured│    ✅     │     ✅           │
└─────────────────────────────────────────────────────────┘
```

Legend:
- ✅ = Safe  
- ❌ = Bug — do NOT do this  
- ⚠️ = Technically works but the transient becomes effectively Singleton (OK if intended, e.g., stateless helpers)

---

## Special Built-in Services You Can Always Inject

| Type | Lifetime | Purpose |
|------|----------|---------|
| `IServiceProvider` | Scoped (request scope) | Resolve services at runtime |
| `IServiceScopeFactory` | Singleton | Create child scopes in Singleton services |
| `ILogger<T>` | Singleton | Logging |
| `IOptions<T>` | Singleton | Read config once at startup |
| `IOptionsSnapshot<T>` | Scoped | Re-reads config every request |
| `IOptionsMonitor<T>` | Singleton | Live config updates |
| `IConfiguration` | Singleton | Raw config access |
| `IEnumerable<T>` | Varies | All registered implementations of T |
| `IKeyedServiceProvider` | Scoped | Resolve by key at runtime (.NET 8+) |

---

## Constructor Injection into ASP.NET Core Types

| Type | How registered |
|------|---------------|
| `Controller` | `services.AddControllers()` |
| `PageModel` (Razor Pages) | `services.AddRazorPages()` |
| `IHostedService` | `services.AddHostedService<T>()` |
| Minimal API handler | Lambda — use `app.MapGet("...", (IFoo foo) => ...)` |
| Middleware | `UseMiddleware<T>()` — remaining ctor params injected |
| `IHealthCheck` | `services.AddHealthChecks().AddCheck<T>("name")` |
