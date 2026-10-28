# 04 — DI Framework Comparison

## Quick Decision Guide

```
Start here → Is built-in DI sufficient?
    YES → Use Microsoft.Extensions.DependencyInjection (default)
    NO  → Do you need decorators only? → Use Scrutor (thin add-on to built-in)
         → Do you need property injection / AOP? → Use Autofac
         → Do you need strict verification / diagnostics? → Use SimpleInjector
```

---

## Feature Matrix

| Feature | Built-in (.NET) | Scrutor | Autofac | SimpleInjector | Ninject |
|---------|----------------|---------|---------|----------------|---------|
| NuGet package | Built-in | `Scrutor` | `Autofac.Extensions.DependencyInjection` | `SimpleInjector` | `Ninject` |
| Constructor injection | Yes | Yes | Yes | Yes | Yes |
| Property injection | No | No | Yes | Yes | Yes |
| Method injection | No | No | Yes | No | Yes |
| Decorator pattern | No | **Yes** | Yes | Yes | Yes |
| Assembly scanning | No | **Yes** | Yes | Yes | Yes |
| Named / Keyed services | .NET 8+ | No | Yes (full) | Yes | Yes |
| Open generics | Yes | Yes | Yes | Yes | Yes |
| AOP / Interception | No | No | Yes (Castle) | Limited | Yes |
| Lifetime validation | Good | Good | Basic | **Excellent** | Basic |
| Async disposal | Yes | Yes | Yes | Yes | No |
| Performance | **Fastest** | Fast | Fast | Fast | Slow |
| Active maintenance | Yes | Yes | Yes | Yes | Sparse |
| ASP.NET Core integration | Native | Via built-in | Yes | Yes | Community |

---

## When to use each

### Microsoft.Extensions.DI (built-in)
- New projects — always start here
- Microservices, APIs, Worker services
- When your team prefers minimal dependencies

### Scrutor
- You want decorator patterns, assembly scanning but don't want a full container
- Thin wrapper over the built-in container — no paradigm shift

### Autofac
- Large enterprise apps with complex wiring
- Need AOP / interception (with Castle.Core)
- Need property injection for legacy code
- Teams already familiar with it

### SimpleInjector
- When correctness matters more than convenience
- Strict diagnostics catch captive dependencies at startup
- Used in regulated industries (finance, healthcare)

### Ninject
- Legacy .NET Framework projects (not recommended for new work)

---

## Files

| File | Content |
|------|---------|
| `BuiltInDI.cs` | Full features of built-in DI with practical tips |
| `ScrutorPatterns.cs` | Assembly scanning + decorators with Scrutor |
| `AutofacPatterns.cs` | Modules, property injection, AOP in Autofac |
| `SimpleInjectorPatterns.cs` | Verification and diagnostics with SimpleInjector |
