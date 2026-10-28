// See 02_Inheritance.md for the full guide, anti-patterns, and decision tables.
using System;
using System.Collections.Generic;
using System.Text;

namespace OOP_Fundamentals.InheritancePrinciple
{
    namespace BadCode
    {
        public class EmployeeBase_Deep
        {
            public string Name { get; set; }
            public EmployeeBase_Deep(string name) { Name = name; }
            public virtual decimal CalculatePay()   => 0;
            public virtual decimal CalculateBonus() => 0;
            public virtual string  GetReport()      => $"Employee: {Name}";
        }

        public class PermanentEmployee_Deep : EmployeeBase_Deep
        {
            public decimal BaseSalary { get; set; }
            public PermanentEmployee_Deep(string name, decimal salary) : base(name) => BaseSalary = salary;
            public override decimal CalculatePay() => BaseSalary;
        }

        public class SalariedEmployee_Deep : PermanentEmployee_Deep
        {
            public decimal BonusPercentage { get; set; }
            public SalariedEmployee_Deep(string name, decimal salary, decimal pct) : base(name, salary) => BonusPercentage = pct;
            public override decimal CalculateBonus() => BaseSalary * BonusPercentage;
        }

        public class SeniorSalariedEmployee_Deep : SalariedEmployee_Deep
        {
            public decimal StockOptions { get; set; }
            public SeniorSalariedEmployee_Deep(string name, decimal salary, decimal pct, decimal stock)
                : base(name, salary, pct) => StockOptions = stock;
            public override string GetReport() => base.GetReport() + $", Stock: {StockOptions:C}";
        }

        public class ExecutiveSalariedEmployee_Deep : SeniorSalariedEmployee_Deep
        {
            public string ExecutiveLevel { get; set; }
            public ExecutiveSalariedEmployee_Deep(string name, decimal salary, decimal pct, decimal stock, string level)
                : base(name, salary, pct, stock) => ExecutiveLevel = level;
            public override decimal CalculatePay() => base.CalculatePay() + StockOptions;
        }

        // Contractors don't fit — forced to override CalculateBonus() with a lie
        public class ContractEmployee_Deep : EmployeeBase_Deep
        {
            public decimal DailyRate  { get; set; }
            public int     DaysWorked { get; set; }
            public ContractEmployee_Deep(string name, decimal rate, int days) : base(name)
            { DailyRate = rate; DaysWorked = days; }
            public override decimal CalculatePay()   => DailyRate * DaysWorked;
            public override decimal CalculateBonus() => 0;  // forced stub — doesn't apply
        }
    }

    namespace GoodCode
    {
        public abstract class AuditableEntity
        {
            public Guid           Id        { get; } = Guid.NewGuid();
            public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
            public DateTimeOffset UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;
            public string         CreatedBy { get; }

            protected AuditableEntity(string createdBy)
            {
                if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("CreatedBy required");
                CreatedBy = createdBy;
            }

            protected void TouchUpdatedAt() => UpdatedAt = DateTimeOffset.UtcNow;
        }

        public interface IPayrollStrategy { decimal Calculate(decimal baseSalary, int? daysWorked = null); }
        public interface IBonusStrategy   { decimal Calculate(decimal baseSalary); }

        public sealed class SalaryPayrollStrategy : IPayrollStrategy
        {
            public decimal Calculate(decimal baseSalary, int? daysWorked = null) => baseSalary;
        }

        public sealed class HourlyPayrollStrategy : IPayrollStrategy
        {
            private readonly decimal _hourlyRate;
            private readonly int     _hoursPerMonth;
            public HourlyPayrollStrategy(decimal rate, int hours = 160) { _hourlyRate = rate; _hoursPerMonth = hours; }
            public decimal Calculate(decimal baseSalary, int? daysWorked = null) => _hourlyRate * _hoursPerMonth;
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
            public PercentageBonusStrategy(decimal pct)
            {
                if (pct is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(pct), "Must be 0–1");
                _pct = pct;
            }
            public decimal Calculate(decimal baseSalary) => baseSalary * _pct;
        }

        public sealed class FlatBonusStrategy : IBonusStrategy
        {
            private readonly decimal _amount;
            public FlatBonusStrategy(decimal amount) => _amount = amount;
            public decimal Calculate(decimal baseSalary) => _amount;
        }

        public sealed class NoBonusStrategy : IBonusStrategy
        {
            public decimal Calculate(decimal baseSalary) => 0m;
        }

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
                IPayrollStrategy payroll, IBonusStrategy bonus, string createdBy)
                : base(createdBy)
            {
                Name       = name ?? throw new ArgumentNullException(nameof(name));
                Role       = role ?? throw new ArgumentNullException(nameof(role));
                BaseSalary = baseSalary > 0 ? baseSalary : throw new ArgumentException("Must be positive");
                _payroll   = payroll ?? throw new ArgumentNullException(nameof(payroll));
                _bonus     = bonus   ?? throw new ArgumentNullException(nameof(bonus));
            }

            public void RecordDaysWorked(int days)
            {
                if (days <= 0) throw new ArgumentException("Days must be positive");
                DaysWorked += days;
                TouchUpdatedAt();
            }

            public decimal CalculatePay()   => _payroll.Calculate(BaseSalary, DaysWorked);
            public decimal CalculateBonus() => _bonus.Calculate(BaseSalary);

            public string GenerateReport()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Name: {Name}  Role: {Role}");
                sb.AppendLine($"Pay:  {CalculatePay():C}  Bonus: {CalculateBonus():C}");
                return sb.ToString();
            }
        }

        public class DomainException : Exception
        {
            public string ErrorCode { get; }
            public DomainException(string code, string message) : base(message) => ErrorCode = code;
            public DomainException(string code, string message, Exception inner) : base(message, inner) => ErrorCode = code;
        }

        public sealed class EmployeeNotFoundException : DomainException
        {
            public Guid EmployeeId { get; }
            public EmployeeNotFoundException(Guid id)
                : base("EMPLOYEE_NOT_FOUND", $"Employee {id} was not found")
                => EmployeeId = id;
        }

        public sealed class PayrollCalculationException : DomainException
        {
            public PayrollCalculationException(Guid id, string reason)
                : base("PAYROLL_ERROR", $"Payroll failed for {id}: {reason}") { }
        }

        public abstract class ReportGenerator
        {
            // Sealed so the algorithm cannot be broken by subclasses
            public sealed string Generate(IEnumerable<Employee> employees)
            {
                var sb = new StringBuilder();
                WriteHeader(sb);
                foreach (var emp in employees) WriteEmployee(sb, emp);
                WriteFooter(sb);
                return sb.ToString();
            }

            protected abstract void WriteHeader(StringBuilder sb);
            protected abstract void WriteEmployee(StringBuilder sb, Employee emp);
            protected abstract void WriteFooter(StringBuilder sb);
        }

        public sealed class CsvReportGenerator : ReportGenerator
        {
            protected override void WriteHeader(StringBuilder sb)   => sb.AppendLine("Name,Role,Pay,Bonus");
            protected override void WriteEmployee(StringBuilder sb, Employee emp)
                => sb.AppendLine($"{emp.Name},{emp.Role},{emp.CalculatePay():F2},{emp.CalculateBonus():F2}");
            protected override void WriteFooter(StringBuilder sb)   => sb.AppendLine($"# {DateTimeOffset.UtcNow:u}");
        }

        public sealed class HtmlReportGenerator : ReportGenerator
        {
            protected override void WriteHeader(StringBuilder sb)
                => sb.AppendLine("<table><thead><tr><th>Name</th><th>Role</th><th>Pay</th></tr></thead><tbody>");
            protected override void WriteEmployee(StringBuilder sb, Employee emp)
                => sb.AppendLine($"<tr><td>{emp.Name}</td><td>{emp.Role}</td><td>{emp.CalculatePay():C}</td></tr>");
            protected override void WriteFooter(StringBuilder sb) => sb.AppendLine("</tbody></table>");
        }
    }

    namespace AdvancedConcepts
    {
        using GoodCode;

        public static class EmployeeFactory
        {
            public static Employee CreateSalaried(string name, decimal salary, string by)
                => new(name, "Salaried", salary, new SalaryPayrollStrategy(), new PercentageBonusStrategy(0.10m), by);

            public static Employee CreateContractor(string name, decimal dailyRate, string by)
                => new(name, "Contractor", dailyRate, new ContractPayrollStrategy(dailyRate), new NoBonusStrategy(), by);

            public static Employee CreateExecutive(string name, decimal salary, decimal bonusPct, string by)
                => new(name, "Executive", salary, new SalaryPayrollStrategy(), new PercentageBonusStrategy(bonusPct), by);
        }

        public class Shape
        {
            public virtual string Describe() => "Shape";
            public         string Label()    => "Shape";
        }

        public class Circle : Shape
        {
            public override string Describe() => "Circle";  // polymorphic
            public new     string Label()     => "Circle";  // hides base — NOT polymorphic
        }

        public sealed class OptimizedCalculator
        {
            // sealed → JIT devirtualizes and can inline
            public decimal Calculate(decimal salary, decimal bonusPct) => salary + (salary * bonusPct);
        }
    }

    public static class InheritanceDemo
    {
        public static void Run()
        {
            var salaried   = AdvancedConcepts.EmployeeFactory.CreateSalaried("Alice", 8000m, "system");
            var contractor = AdvancedConcepts.EmployeeFactory.CreateContractor("Bob", 600m, "system");
            var executive  = AdvancedConcepts.EmployeeFactory.CreateExecutive("Carol", 20000m, 0.30m, "system");

            contractor.RecordDaysWorked(20);

            var employees = new[] { salaried, contractor, executive };
            foreach (var e in employees) Console.Write(e.GenerateReport());

            GoodCode.ReportGenerator csv = new GoodCode.CsvReportGenerator();
            Console.WriteLine(csv.Generate(employees));

            // new vs override
            GoodCode.Shape s = new AdvancedConcepts.Circle(); // wait - wrong namespace
            // Demonstrating via AdvancedConcepts directly:
            var shape = (GoodCode.Shape)new AdvancedConcepts.Circle();
            Console.WriteLine(shape.Describe());   // "Circle" — override is polymorphic
            Console.WriteLine(shape.Label());      // "Shape"  — 'new' is NOT polymorphic
        }
    }
}
