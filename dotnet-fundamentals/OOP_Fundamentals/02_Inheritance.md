# 02 — Inheritance

> **Core idea:** Inheritance is for *IS-A* relationships and shared identity — not for code reuse. When in doubt, compose instead.

---

## What is Inheritance?

Inheritance lets a **derived (child) class** acquire the fields, properties, and methods of a **base (parent) class**. In C#, a class can inherit from exactly one base class.

The child IS-A kind of parent. A `Dog` IS-A `Animal`. A `SavingsAccount` IS-A `BankAccount`. The relationship must be genuinely true — not just "I want to reuse some of that code."

---

## Key Concepts

| Concept | Meaning | C# Keyword |
|---|---|---|
| **Is-A** | Child genuinely IS a specialisation of parent | `: BaseClass` |
| **Constructor chaining** | Parent constructor runs before child's body | `base(args)` |
| **Overriding** | Child replaces a parent behaviour | `virtual` + `override` |
| **Forcing implementation** | Child must provide its own version | `abstract` |
| **Preventing subclassing** | No further inheritance allowed | `sealed` |
| **Hiding (not overriding)** | Child declares a member with the same name — NOT polymorphic | `new` |

---

## 5 Real-World Use Cases

1. **Exception hierarchy** — `NotFoundException : HttpException : Exception`. Each level adds context; all are catchable as `Exception`.
2. **UI control tree** — `Button : Control`, `TextBox : Control`. Shared rendering pipeline in the base; specific behaviour overridden.
3. **Auditable EF Core entities** — `AuditableEntity` base with `CreatedAt` / `UpdatedAt` / `CreatedBy`; all database entities inherit it.
4. **Report generators** — Template Method pattern: base defines the algorithm skeleton, subclasses fill in format-specific steps.
5. **Domain exception hierarchy** — `DomainException : Exception`, `EmployeeNotFoundException : DomainException`. Callers catch the right level of abstraction.

---

## ❌ Anti-Pattern: Deep Inheritance Hierarchy

A 5-level hierarchy built around an `Employee` type. Every new requirement forces either a change to the base (breaking all descendants) or adding another level.

**The Fragile Base Class problem:** changing `EmployeeBase_Deep` silently breaks every class below it. You cannot change the base without auditing all 5 levels.

**The Gorilla-Banana problem:** you wanted a banana (bonus calculation logic), you got the gorilla (`Employee`) and the entire jungle (the whole hierarchy).

```csharp
// Level 1
public class EmployeeBase_Deep
{
    public string  Name { get; set; }
    public virtual decimal CalculatePay()   => 0;
    public virtual decimal CalculateBonus() => 0;
    public virtual string  GetReport()      => $"Employee: {Name}";
    public virtual void    Log(string msg)  => Console.WriteLine(msg); // ❌ wrong concern in base
}

// Level 2
public class PermanentEmployee_Deep : EmployeeBase_Deep
{
    public decimal BaseSalary { get; set; }
    public override decimal CalculatePay() => BaseSalary;
}

// Level 3
public class SalariedEmployee_Deep : PermanentEmployee_Deep
{
    public decimal BonusPercentage { get; set; }
    public override decimal CalculateBonus() => BaseSalary * BonusPercentage;
}

// Level 4
public class SeniorSalariedEmployee_Deep : SalariedEmployee_Deep
{
    public decimal StockOptions { get; set; }
    public override string GetReport()
        => base.GetReport() + $", Stock: {StockOptions:C}";
}

// Level 5
public class ExecutiveSalariedEmployee_Deep : SeniorSalariedEmployee_Deep
{
    public string ExecutiveLevel { get; set; }
    public override decimal CalculatePay() => base.CalculatePay() + StockOptions;
}

// NEW REQUIREMENT: "Contractors get a daily rate, not a salary"
// Where does ContractEmployee fit? It doesn't.
// Jamming it under EmployeeBase creates an incoherent hierarchy:
public class ContractEmployee_Deep : EmployeeBase_Deep
{
    public decimal DailyRate  { get; set; }
    public int     DaysWorked { get; set; }
    public override decimal CalculatePay()   => DailyRate * DaysWorked;
    public override decimal CalculateBonus() => 0;  // ❌ forced to "implement" a concept that doesn't apply
}
```

---

## ✅ Pattern: Composition Over Inheritance

Replace the hierarchy with a **thin base class** (for genuine shared identity) + **injected strategies** (for variable behaviour) + **sealed concrete classes** (no further inheritance).

### Step 1 — Thin base class: shared infrastructure only

```csharp
// Only fields that EVERY entity genuinely needs. No behaviour beyond identity.
public abstract class AuditableEntity
{
    public Guid            Id        { get; } = Guid.NewGuid();
    public DateTimeOffset  CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset  UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public string          CreatedBy { get; }

    protected AuditableEntity(string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required");
        CreatedBy = createdBy;
    }

    protected void TouchUpdatedAt() => UpdatedAt = DateTimeOffset.UtcNow;
}
```

### Step 2 — Strategy interfaces: the variable behaviour

```csharp
public interface IPayrollStrategy
{
    decimal Calculate(decimal baseSalary, int? daysWorked = null);
}

public interface IBonusStrategy
{
    decimal Calculate(decimal baseSalary);
}

// Each strategy is sealed and independently testable
public sealed class SalaryPayrollStrategy : IPayrollStrategy
{
    public decimal Calculate(decimal baseSalary, int? daysWorked = null) => baseSalary;
}

public sealed class ContractPayrollStrategy : IPayrollStrategy
{
    private readonly decimal _dailyRate;
    public ContractPayrollStrategy(decimal dailyRate) => _dailyRate = dailyRate;

    public decimal Calculate(decimal baseSalary, int? daysWorked = null)
    {
        if (!daysWorked.HasValue || daysWorked <= 0)
            throw new ArgumentException("Contract employees require daysWorked");
        return _dailyRate * daysWorked.Value;
    }
}

public sealed class PercentageBonusStrategy : IBonusStrategy
{
    private readonly decimal _pct;
    public PercentageBonusStrategy(decimal pct) => _pct = pct;
    public decimal Calculate(decimal baseSalary) => baseSalary * _pct;
}

public sealed class NoBonusStrategy : IBonusStrategy
{
    public decimal Calculate(decimal baseSalary) => 0m;
}
```

### Step 3 — Flat, sealed concrete class composed from strategies

```csharp
public sealed class Employee : AuditableEntity
{
    private readonly IPayrollStrategy _payroll;
    private readonly IBonusStrategy   _bonus;

    public string  Name       { get; }
    public string  Role       { get; }
    public decimal BaseSalary { get; }
    public int     DaysWorked { get; private set; }

    public Employee(
        string name, string role, decimal baseSalary,
        IPayrollStrategy payroll, IBonusStrategy bonus,
        string createdBy) : base(createdBy)
    {
        Name       = name;
        Role       = role;
        BaseSalary = baseSalary > 0 ? baseSalary : throw new ArgumentException("Must be positive");
        _payroll   = payroll ?? throw new ArgumentNullException(nameof(payroll));
        _bonus     = bonus   ?? throw new ArgumentNullException(nameof(bonus));
    }

    public void RecordDaysWorked(int days) { DaysWorked += days; TouchUpdatedAt(); }

    public decimal CalculatePay()   => _payroll.Calculate(BaseSalary, DaysWorked);
    public decimal CalculateBonus() => _bonus.Calculate(BaseSalary);
}

// Factory hides the construction complexity from callers
public static class EmployeeFactory
{
    public static Employee CreateSalaried(string name, decimal salary, string by)
        => new(name, "Salaried", salary, new SalaryPayrollStrategy(), new PercentageBonusStrategy(0.10m), by);

    public static Employee CreateContractor(string name, decimal dailyRate, string by)
        => new(name, "Contractor", dailyRate, new ContractPayrollStrategy(dailyRate), new NoBonusStrategy(), by);

    public static Employee CreateExecutive(string name, decimal salary, decimal bonusPct, string by)
        => new(name, "Executive", salary, new SalaryPayrollStrategy(), new PercentageBonusStrategy(bonusPct), by);
}
```

---

## Intermediate: Legitimate Inheritance — Exception Hierarchy

Exceptions ARE a genuine IS-A hierarchy. This is a correct use of inheritance.

```csharp
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message) : base(message)
        => ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));

    public DomainException(string errorCode, string message, Exception inner)
        : base(message, inner) => ErrorCode = errorCode;
}

public sealed class EmployeeNotFoundException : DomainException
{
    public Guid EmployeeId { get; }

    public EmployeeNotFoundException(Guid id)
        : base("EMPLOYEE_NOT_FOUND", $"Employee {id} was not found")
        => EmployeeId = id;
}

// Callers can catch at the right level:
// catch (EmployeeNotFoundException)  — specific handling
// catch (DomainException)            — all domain errors
// catch (Exception)                  — last resort
```

---

## Intermediate: Template Method — One Correct Use of Abstract Inheritance

The base class defines the **algorithm skeleton**. Subclasses fill in the variable steps. The template method itself is `sealed` so the algorithm cannot be broken.

```csharp
public abstract class ReportGenerator
{
    // Sealed template — algorithm is fixed, steps are variable
    public sealed string Generate(IEnumerable<Employee> employees)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        foreach (var emp in employees)
            WriteEmployee(sb, emp);
        WriteFooter(sb);
        return sb.ToString();
    }

    protected abstract void WriteHeader(StringBuilder sb);
    protected abstract void WriteEmployee(StringBuilder sb, Employee emp);
    protected abstract void WriteFooter(StringBuilder sb);
}

public sealed class CsvReportGenerator : ReportGenerator
{
    protected override void WriteHeader(StringBuilder sb)
        => sb.AppendLine("Name,Role,Pay,Bonus");

    protected override void WriteEmployee(StringBuilder sb, Employee emp)
        => sb.AppendLine($"{emp.Name},{emp.Role},{emp.CalculatePay():F2},{emp.CalculateBonus():F2}");

    protected override void WriteFooter(StringBuilder sb)
        => sb.AppendLine($"# Generated {DateTimeOffset.UtcNow:u}");
}

public sealed class HtmlReportGenerator : ReportGenerator
{
    protected override void WriteHeader(StringBuilder sb)
        => sb.AppendLine("<table><thead><tr><th>Name</th><th>Pay</th></tr></thead><tbody>");

    protected override void WriteEmployee(StringBuilder sb, Employee emp)
        => sb.AppendLine($"<tr><td>{emp.Name}</td><td>{emp.CalculatePay():C}</td></tr>");

    protected override void WriteFooter(StringBuilder sb)
        => sb.AppendLine("</tbody></table>");
}
```

---

## Advanced: Constructor Chaining Order

`base()` runs **before** the derived constructor body. Fields are initialized in order: base field initializers → base constructor → derived field initializers → derived constructor.

```csharp
public class Base
{
    public Base() { Console.WriteLine("1. Base constructor"); }
}

public class Derived : Base
{
    private readonly string _name;

    public Derived(string name) : base()          // base() called first
    {
        _name = name;                             // then derived body
        Console.WriteLine($"2. Derived constructor, name={_name}");
    }
}
// Output:
// 1. Base constructor
// 2. Derived constructor, name=Alice
```

**Critical gotcha: never call virtual methods from a constructor.**

```csharp
public class Base
{
    public Base()
    {
        Describe();  // ❌ Calls the DERIVED override before derived fields are initialised
    }
    public virtual void Describe() => Console.WriteLine("Base");
}

public class Derived : Base
{
    private readonly string _name;

    public Derived(string name) : base()  // base() fires → Describe() → _name is still null!
    {
        _name = name;
    }

    public override void Describe()
        => Console.WriteLine(_name ?? "<<not initialized yet>>");  // prints null
}
```

---

## Advanced: `new` Keyword — Hiding is NOT Polymorphism

This is a subtle C# footgun. `override` participates in virtual dispatch; `new` does not.

```csharp
public class Shape
{
    public virtual string Describe() => "Shape";  // virtual
    public         string Name()     => "Shape";  // non-virtual
}

public class Circle : Shape
{
    public override string Describe() => "Circle";  // ✅ polymorphic
    public new     string Name()      => "Circle";  // ⚠️ hides base — NOT polymorphic
}

Shape s = new Circle();
Console.WriteLine(s.Describe());  // "Circle" — override dispatches to derived ✅
Console.WriteLine(s.Name());      // "Shape"  — hiding uses static type (Shape) ⚠️

Circle c = new Circle();
Console.WriteLine(c.Name());      // "Circle" — only when using the concrete type
```

**Rule:** If you find yourself writing `new` on a method that matches a base member, stop and ask whether `override` is what you actually meant. `new` is rarely the right answer.

---

## Advanced: `sealed` and Devirtualization

Every `virtual` call requires a vtable lookup at runtime. When a class is `sealed`, the JIT knows there are no derived classes and can **devirtualize** — turning the indirect vtable call into a direct call, enabling inlining.

In tight loops (game engines, financial simulations), devirtualized calls can be 3–5× faster due to inlining cascades.

```csharp
// Unsealed — every call to Area() is an indirect vtable dispatch
public class Shape { public virtual double Area() => 0; }

// Sealed — JIT inlines Area() at call sites that hold Circle references
public sealed class Circle : Shape
{
    private readonly double _r;
    public Circle(double r) => _r = r;
    public sealed override double Area() => Math.PI * _r * _r;
}
```

**Rule:** If a class is not designed for extension, mark it `sealed`. It is a free performance gain and a clear design signal to readers.

---

## Decision Guide

| Situation | Recommendation |
|---|---|
| Types share only infrastructure fields (CreatedAt, Id) | Thin `abstract` base class — no behaviour |
| Types share an algorithm with variable steps | Template Method in abstract base |
| Types share a contract but NO state | Interface — not a base class |
| Code reuse without IS-A relationship | Composition / Strategy pattern |
| Adding a new type should not touch existing code | Interface + factory |
| Legitimate exception hierarchy | Inheritance is correct here |
| Class not designed for extension | Mark it `sealed` |
| Virtual call in a performance-critical loop | `sealed` for devirtualization |
