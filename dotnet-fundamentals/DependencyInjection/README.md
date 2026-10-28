# Dependency Injection (DI) — Complete Guide

> **Level:** Beginner → Advanced → Expert  
> **Platform:** General concepts + .NET 6/7/8 specifics

---

## What This Module Covers

| Folder | Topic |
|--------|-------|
| `01_BasicConcepts` | What DI is, IoC, DIP, manual wiring |
| `02_ServiceLifetimes` | Singleton, Scoped, Transient — when and why |
| `03_AdvancedPatterns` | Decorators, factories, open generics, keyed services |
| `04_DIComparison` | Built-in vs Autofac vs Scrutor vs SimpleInjector |
| `05_CaseExamples` | 5 real-world scenarios end-to-end |
| `06_Gotchas` | Captive dependencies, circular refs, anti-patterns |
| `07_InjectionTypes` | Constructor vs Property vs Method injection; what can go into what |

---

## 1. The Core Problem DI Solves

Without DI your class reaches out and grabs its own dependencies:

```csharp
// BAD — tightly coupled
public class OrderService
{
    private readonly SqlOrderRepository _repo = new SqlOrderRepository("Server=prod;...");
    private readonly EmailSender _email     = new EmailSender("smtp.company.com", 587);
}
```

Problems:
- You cannot unit-test `OrderService` without a live SQL server and SMTP host.
- Changing the database means editing `OrderService`.
- You can never swap `EmailSender` for a mock or a different provider.

---

## 2. Three Principles Behind DI

### 2.1 Dependency Inversion Principle (DIP) — *what* to depend on
> High-level modules must not depend on low-level modules.  
> Both must depend on **abstractions** (interfaces / abstract classes).

```
OrderService  →  IOrderRepository  ←  SqlOrderRepository
                                   ←  InMemoryOrderRepository (tests)
```

### 2.2 Inversion of Control (IoC) — *who* creates the dependency
Normal flow: class creates its dependencies.  
IoC flow: an external authority (container / host) creates and hands them in.

### 2.3 Dependency Injection (DI) — *how* the dependency arrives
The specific technique where dependencies are **pushed into** the class rather than pulled by it. DI is one way to achieve IoC.

---

## 3. Three Injection Styles (Quick Reference)

| Style | How | When to use |
|-------|-----|-------------|
| **Constructor** | Parameter on `ctor` | Required dependencies — prefer this always |
| **Property** | Public settable property | Optional dependencies, legacy code you cannot change |
| **Method** | Parameter on a specific method | Dependency used only inside that one operation |

See `07_InjectionTypes/` for detailed examples and rules on what can inject into what.

---

## 4. Service Lifetimes (Quick Reference)

| Lifetime | Created | Destroyed | Shared? |
|----------|---------|-----------|---------|
| **Singleton** | Once per app | App shutdown | All requests, all scopes |
| **Scoped** | Once per request (HTTP scope) | Request ends | Within the same request |
| **Transient** | Every time resolved | When caller disposes | Never shared |

> **Golden rule:** never inject a shorter-lived service into a longer-lived one.  
> Scoped → Singleton = "captive dependency" bug. See `06_Gotchas/`.

---

## 5. .NET Built-in DI at a Glance

```csharp
// Program.cs (.NET 6+)
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICache, RedisCache>();
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

var app = builder.Build();
```

The `IServiceCollection` is the **registration API**.  
The `IServiceProvider` is the **resolution API**.  
The `WebApplication` host owns both and manages lifetimes.

---

## 6. Why Not Always Use Built-in DI?

Built-in (`Microsoft.Extensions.DependencyInjection`) is excellent for 95% of apps.  
You reach for a third-party container when you need:

| Feature | Built-in | Autofac | SimpleInjector |
|---------|----------|---------|----------------|
| Named / keyed registrations | .NET 8+ (basic) | Yes (full) | Yes |
| Decorator pipeline | Via Scrutor | Native | Native |
| Convention-based scanning | Via Scrutor | Native | Native |
| Property injection | No | Yes | Yes |
| Interception / AOP | No | Yes (Castle) | Yes |
| Diagnostic / verification | Basic | Good | Excellent |

See `04_DIComparison/` for code.

---

## 7. Learning Path

```
Beginner:  01_BasicConcepts → 07_InjectionTypes (constructor only)
Intermediate: 02_ServiceLifetimes → 05_CaseExamples (Cases 1-3)
Advanced:  03_AdvancedPatterns → 05_CaseExamples (Cases 4-5) → 06_Gotchas
Expert:    04_DIComparison → all Gotchas → build your own mini-container
```
