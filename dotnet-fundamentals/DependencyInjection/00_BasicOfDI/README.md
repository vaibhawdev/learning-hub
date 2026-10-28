# 00 — Basics of DI: DIP and IoC

> **Before DI exists, two principles must be understood: what to aim for (DIP) and how to get there (IoC).**  
> DI is simply the most practical implementation of IoC that satisfies DIP.

---

## Files in This Folder

| File | Demonstrates |
|---|---|
| `DIP_Explained.cs` | Dependency Inversion Principle — violations and correct application |
| `IoC_Explained.cs` | Inversion of Control — traditional flow vs. inverted flow, and IoC patterns |

---

## 1. Dependency Inversion Principle (DIP)

DIP is the **D** in SOLID (Robert C. Martin). It has two rules:

> **Rule 1:** High-level modules must not depend on low-level modules. Both must depend on abstractions.  
> **Rule 2:** Abstractions must not depend on details. Details must depend on abstractions.

### What "High-level" and "Low-level" mean

| Term | Meaning | Example |
|---|---|---|
| High-level module | Business logic / orchestration | `OrderService`, `CheckoutController` |
| Low-level module | Infrastructure / mechanism | `SqlOrderRepository`, `SmtpEmailSender` |
| Abstraction | Interface or abstract class | `IOrderRepository`, `IEmailSender` |
| Detail | Concrete implementation | `SqlOrderRepository : IOrderRepository` |

### DIP Violated

```
OrderService  ──────────────►  SqlOrderRepository
  (high-level)                   (low-level, concrete)
```

`OrderService` directly instantiates or references `SqlOrderRepository`.  
- Can't swap to a different database without changing `OrderService`.  
- Can't unit-test `OrderService` in isolation — the SQL driver ships with it.

### DIP Applied

```
OrderService  ──────────────►  IOrderRepository   ◄────  SqlOrderRepository
  (high-level)                   (abstraction)              (low-level detail)
```

Both `OrderService` AND `SqlOrderRepository` point **inward** to the interface.  
The dependency arrow from high-level to low-level has been **inverted** — hence the name.

### The Ownership Rule

The interface (`IOrderRepository`) should be **owned by the high-level module**, not the low-level one.  
It lives conceptually in the business layer, not in the infrastructure layer.

```
BusinessLayer/
    IOrderRepository.cs   ← owned here
    OrderService.cs

InfrastructureLayer/
    SqlOrderRepository.cs  ← depends on BusinessLayer abstraction
```

---

## 2. Inversion of Control (IoC)

IoC is a **design principle** (not a pattern) that says:

> Stop letting your code decide what it depends on. Hand that decision to something external.

In traditional code, your class creates and controls its dependencies:

```
                ┌─────────────────────────────┐
                │         YourClass           │
                │                             │
                │  new SqlRepo()  ◄── control │
                │  new SmtpSender() ◄── here  │
                └─────────────────────────────┘
```

With IoC, control is **inverted** — an external mechanism decides and provides dependencies:

```
   ┌───────────────────────┐          ┌─────────────────────────┐
   │   IoC Mechanism       │          │       YourClass          │
   │  (Container/Factory/  │─────────►│                          │
   │   Framework)          │ provides │  just uses what it gets  │
   └───────────────────────┘          └─────────────────────────┘
```

### IoC is NOT the same as DI

| Concept | What it is | Scope |
|---|---|---|
| DIP | A SOLID design principle | Goal / what to aim for |
| IoC | A technique to achieve DIP | Broader — many patterns qualify |
| DI | One specific form of IoC | Most common implementation of IoC |

DI is the answer to: *"How do we implement IoC in practice?"*

### IoC Patterns (from least to most recommended)

| Pattern | How control is inverted | Verdict |
|---|---|---|
| Template Method | Base class calls abstract methods defined by subclass | OK for simple cases |
| Factory / Abstract Factory | Factory decides which concrete type to create | Useful but still manual |
| Service Locator | Class asks a registry for its dependencies | **Anti-pattern** — hides dependencies |
| Dependency Injection | Dependencies provided externally via constructor/property/method | **Recommended** |
| IoC Container | Automates DI — resolves and injects the entire object graph | **Production standard** |

---

## 3. How DIP + IoC Lead to DI

```
  DIP says:          "Depend on abstractions, not concretions."
       ↓
  IoC says:          "Don't create your dependencies — let someone else provide them."
       ↓
  DI says:           "I'll provide dependencies through the constructor."
       ↓
  IoC Container:     "I'll wire up and inject all dependencies automatically."
```

The mental model:
- Without DIP → you depend on the wrong things.
- Without IoC → even with interfaces, you still `new` things inside your class.
- With DI → dependencies are passed in from outside, satisfying both DIP and IoC.

---

## 4. Key Terminology

| Term | Definition |
|---|---|
| Dependency | Any object a class needs to do its work |
| Abstraction | Interface or abstract class — defines a contract, not an implementation |
| Concretion | A specific implementation class |
| Composition Root | The single location in the app where all dependencies are wired up |
| Object Graph | The tree of objects created to satisfy a single root dependency |
| IoC Container | A framework component that automates dependency resolution and injection |

---

## 5. Learning Path

```
00_BasicOfDI (here)
    ↓  understand why DIP and IoC exist
01_BasicConcepts
    ↓  see what code looks like without DI vs. with manual DI vs. with a container
02_ServiceLifetimes
    ↓  understand singleton / scoped / transient
03_AdvancedPatterns
    ↓  decorators, keyed services, open generics
```
