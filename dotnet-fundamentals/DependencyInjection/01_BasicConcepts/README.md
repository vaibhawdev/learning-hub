# 01 — Basic Concepts

## Files in this folder

| File | What it shows |
|------|--------------|
| `WithoutDI.cs` | Tight coupling — the "before" state |
| `WithDI_Manual.cs` | Manual DI (pure constructor injection, no container) |
| `WithDI_Container.cs` | Container-based DI using .NET built-in |
| `InterfaceSegregation.cs` | How to design thin interfaces for DI |

---

## What is a Dependency?

Any object your class needs to do its job.

```csharp
public class ReportService
{
    // IDataFetcher and IFormatter are "dependencies" of ReportService
    public ReportService(IDataFetcher fetcher, IFormatter formatter) { }
}
```

---

## The Object Graph

When DI resolves `ReportService`, it walks the whole tree:

```
ReportService
├── IDataFetcher  →  SqlDataFetcher
│   └── IDbConnection  →  SqlConnection
└── IFormatter    →  CsvFormatter
```

The container builds this graph bottom-up automatically.

---

## Key Terms

| Term | Definition |
|------|-----------|
| **Service** | Any class registered in the container |
| **Abstraction** | The interface / abstract class your code depends on |
| **Implementation** | The concrete class satisfying that abstraction |
| **Container / IoC Container** | The runtime that wires implementations to abstractions |
| **Registration** | Telling the container: "when someone asks for `IFoo`, give them `FooImpl`" |
| **Resolution** | Asking the container for an instance: `provider.GetRequiredService<IFoo>()` |
| **Composition Root** | The single place in the app where all registrations happen (`Program.cs`) |

---

## The Composition Root Rule

> All DI registrations live in ONE place — `Program.cs` (or the startup class).  
> Business logic code never calls the container directly.

Violating this is the **Service Locator anti-pattern** — covered in `06_Gotchas/`.
