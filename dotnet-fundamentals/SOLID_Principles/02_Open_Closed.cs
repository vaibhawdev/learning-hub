// See 02_Open_Closed.md for full guide, premature abstraction gotcha, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOLID.OpenClosed
{
    namespace BadCode
    {
        public enum CustomerType { Regular, Premium, Employee, VIP, Wholesale }

        // Every new CustomerType requires modifying this class — and every other switch like it.
        public class DiscountCalculator_Closed
        {
            public decimal Calculate(CustomerType type, decimal total)
            {
                return type switch
                {
                    CustomerType.Regular   => total * 0.00m,
                    CustomerType.Premium   => total * 0.10m,
                    CustomerType.Employee  => total * 0.20m,
                    CustomerType.VIP       => total * 0.15m,
                    CustomerType.Wholesale => total > 1000 ? total * 0.25m : total * 0.15m,
                    _                      => throw new ArgumentOutOfRangeException(nameof(type))
                };
            }

            // Second switch that must be kept in sync — they drift apart in practice
            public decimal CalculateBonus(CustomerType type, decimal total)
            {
                return type switch
                {
                    CustomerType.Regular   => 0,
                    CustomerType.Premium   => total * 0.02m,
                    CustomerType.Employee  => 0,
                    CustomerType.VIP       => total * 0.05m,
                    CustomerType.Wholesale => total * 0.01m,
                    _                      => throw new ArgumentOutOfRangeException(nameof(type))
                };
            }
        }

        // Export format switch — another common pattern
        public class ReportService_Closed
        {
            public byte[] Export(string format, IEnumerable<string> rows)
            {
                return format.ToLower() switch
                {
                    "csv"  => System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows)),
                    "json" => System.Text.Encoding.UTF8.GetBytes($"[{string.Join(",", rows.Select(r => $"\"{r}\""))}]"),
                    _      => throw new NotSupportedException($"Format '{format}' not supported")
                    // Adding "excel" or "pdf" requires modifying this method.
                };
            }
        }
    }

    namespace GoodCode
    {
        // ── Discount strategies ────────────────────────────────────────────────
        public record DiscountResult(decimal DiscountAmount, decimal FinalPrice, string AppliedRule);

        public sealed class CustomerContext
        {
            public string  CustomerId   { get; init; } = string.Empty;
            public string  CustomerType { get; init; } = string.Empty;
            public decimal OrderTotal   { get; init; }
            public int     LoyaltyYears { get; init; }
            public bool    IsEmployee   { get; init; }
        }

        public interface IDiscountStrategy
        {
            string Name { get; }
            bool   AppliesTo(CustomerContext ctx);
            DiscountResult Apply(decimal orderTotal);
        }

        public sealed class NoDiscount : IDiscountStrategy
        {
            public string Name => "No Discount";
            public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Regular";
            public DiscountResult Apply(decimal total) => new(0m, total, Name);
        }

        public sealed class PremiumDiscount : IDiscountStrategy
        {
            private const decimal Rate = 0.10m;
            public string Name => "Premium 10%";
            public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Premium";
            public DiscountResult Apply(decimal total)
            {
                var discount = total * Rate;
                return new(discount, total - discount, Name);
            }
        }

        public sealed class EmployeeDiscount : IDiscountStrategy
        {
            private const decimal Rate = 0.20m;
            public string Name => "Employee 20%";
            public bool AppliesTo(CustomerContext ctx) => ctx.IsEmployee;
            public DiscountResult Apply(decimal total)
            {
                var discount = total * Rate;
                return new(discount, total - discount, Name);
            }
        }

        public sealed class WholesaleDiscount : IDiscountStrategy
        {
            public string Name => "Wholesale";
            public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Wholesale";
            public DiscountResult Apply(decimal total)
            {
                var rate     = total > 1000 ? 0.25m : 0.15m;
                var discount = total * rate;
                return new(discount, total - discount, $"{Name} ({rate:P0})");
            }
        }

        // ✅ Adding Corporate: one new class, zero changes to anything above
        public sealed class CorporateDiscount : IDiscountStrategy
        {
            private readonly decimal _negotiatedRate;
            public CorporateDiscount(decimal negotiatedRate)
            {
                if (negotiatedRate is < 0 or > 1)
                    throw new ArgumentOutOfRangeException(nameof(negotiatedRate));
                _negotiatedRate = negotiatedRate;
            }
            public string Name => $"Corporate {_negotiatedRate:P0}";
            public bool AppliesTo(CustomerContext ctx) => ctx.CustomerType == "Corporate";
            public DiscountResult Apply(decimal total)
            {
                var discount = total * _negotiatedRate;
                return new(discount, total - discount, Name);
            }
        }

        // Closed for modification — never changes when new strategies are added
        public sealed class DiscountCalculator
        {
            private readonly IReadOnlyList<IDiscountStrategy> _strategies;

            public DiscountCalculator(IEnumerable<IDiscountStrategy> strategies)
                => _strategies = new List<IDiscountStrategy>(strategies);

            public DiscountResult Calculate(CustomerContext context)
            {
                var strategy = _strategies.FirstOrDefault(s => s.AppliesTo(context))
                    ?? new NoDiscount();
                return strategy.Apply(context.OrderTotal);
            }
        }

        // ── Validation pipeline — chain of responsibility ───────────────────────
        public record ValidationContext(string Email, string Password, string Name);
        public record RuleResult(bool Passed, string? FailureMessage);

        public interface IValidationRule
        {
            RuleResult Evaluate(ValidationContext ctx);
        }

        public sealed class EmailFormatRule : IValidationRule
        {
            public RuleResult Evaluate(ValidationContext ctx)
                => ctx.Email.Contains('@') && ctx.Email.Contains('.')
                    ? new(true, null)
                    : new(false, "Invalid email format");
        }

        public sealed class PasswordLengthRule : IValidationRule
        {
            private readonly int _min;
            public PasswordLengthRule(int min = 8) => _min = min;
            public RuleResult Evaluate(ValidationContext ctx)
                => ctx.Password.Length >= _min
                    ? new(true, null)
                    : new(false, $"Password must be at least {_min} characters");
        }

        public sealed class PasswordComplexityRule : IValidationRule
        {
            public RuleResult Evaluate(ValidationContext ctx)
            {
                if (!ctx.Password.Any(char.IsDigit)) return new(false, "Password must contain a digit");
                if (!ctx.Password.Any(char.IsUpper)) return new(false, "Password must contain an uppercase letter");
                return new(true, null);
            }
        }

        // ✅ New rule: one class, one registration
        public sealed class NameBlocklistRule : IValidationRule
        {
            private static readonly HashSet<string> _blocked =
                new(StringComparer.OrdinalIgnoreCase) { "admin", "root", "system" };
            public RuleResult Evaluate(ValidationContext ctx)
                => _blocked.Contains(ctx.Name)
                    ? new(false, $"Name '{ctx.Name}' is reserved")
                    : new(true, null);
        }

        public sealed class ValidationPipeline
        {
            private readonly IReadOnlyList<IValidationRule> _rules;
            public ValidationPipeline(IEnumerable<IValidationRule> rules)
                => _rules = new List<IValidationRule>(rules);

            public IReadOnlyList<string> Validate(ValidationContext ctx)
                => _rules
                    .Select(r => r.Evaluate(ctx))
                    .Where(r => !r.Passed)
                    .Select(r => r.FailureMessage!)
                    .ToList();
        }

        // ── Export factory — open via registration ─────────────────────────────
        public sealed record ReportRow(string Name, decimal Amount, DateTimeOffset Date);

        public interface IReportExporter
        {
            string Format { get; }
            byte[]  Export(IEnumerable<ReportRow> rows);
        }

        public sealed class CsvExporter : IReportExporter
        {
            public string Format => "csv";
            public byte[] Export(IEnumerable<ReportRow> rows)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Name,Amount,Date");
                foreach (var r in rows)
                    sb.AppendLine($"{r.Name},{r.Amount:F2},{r.Date:yyyy-MM-dd}");
                return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            }
        }

        public sealed class JsonExporter : IReportExporter
        {
            public string Format => "json";
            public byte[] Export(IEnumerable<ReportRow> rows)
                => System.Text.Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(rows.ToList()));
        }

        // ✅ Adding ExcelExporter: new class, registered alongside the others
        public sealed class ExcelExporter : IReportExporter
        {
            public string Format => "excel";
            public byte[] Export(IEnumerable<ReportRow> rows)
            {
                // Real impl: use ClosedXML or EPPlus
                Console.WriteLine("[Excel] Generating xlsx...");
                return Array.Empty<byte>();
            }
        }

        public sealed class ExporterFactory
        {
            private readonly Dictionary<string, IReportExporter> _exporters;

            public ExporterFactory(IEnumerable<IReportExporter> exporters)
                => _exporters = exporters.ToDictionary(e => e.Format, StringComparer.OrdinalIgnoreCase);

            public IReportExporter Get(string format)
                => _exporters.TryGetValue(format, out var e)
                    ? e
                    : throw new NotSupportedException($"Format '{format}' is not supported");
        }
    }

    namespace AdvancedConcepts
    {
        // Plugin discovery via reflection — core system never knows about plugin types
        public interface IPlugin
        {
            string Name { get; }
            void Execute(string input);
        }

        public sealed class PluginLoader
        {
            public IReadOnlyList<IPlugin> LoadFrom(System.Reflection.Assembly assembly)
                => assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t)
                             && !t.IsInterface && !t.IsAbstract
                             && t.GetConstructor(Type.EmptyTypes) is not null)
                    .Select(t => (IPlugin)Activator.CreateInstance(t)!)
                    .ToList();
        }

        // Example plugins — could be in separate assemblies
        public sealed class UpperCasePlugin : IPlugin
        {
            public string Name => "UpperCase";
            public void Execute(string input) => Console.WriteLine(input.ToUpper());
        }

        public sealed class ReversePlugin : IPlugin
        {
            public string Name => "Reverse";
            public void Execute(string input)
                => Console.WriteLine(new string(input.Reverse().ToArray()));
        }
    }

    public static class OcpDemo
    {
        public static void Run()
        {
            Console.WriteLine("=== OCP DEMO ===\n");

            // ── Discount strategies ────────────────────────────────────────────
            var strategies = new GoodCode.IDiscountStrategy[]
            {
                new GoodCode.EmployeeDiscount(),
                new GoodCode.WholesaleDiscount(),
                new GoodCode.PremiumDiscount(),
                new GoodCode.CorporateDiscount(0.18m),
                new GoodCode.NoDiscount()
            };

            var calculator = new GoodCode.DiscountCalculator(strategies);

            var contexts = new[]
            {
                new GoodCode.CustomerContext { CustomerType = "Premium",   OrderTotal = 500m },
                new GoodCode.CustomerContext { CustomerType = "Wholesale", OrderTotal = 1500m, IsEmployee = false },
                new GoodCode.CustomerContext { CustomerType = "Corporate", OrderTotal = 2000m },
                new GoodCode.CustomerContext { IsEmployee = true, OrderTotal = 300m }
            };

            foreach (var ctx in contexts)
            {
                var result = calculator.Calculate(ctx);
                Console.WriteLine($"  {result.AppliedRule}: -{result.DiscountAmount:C} → {result.FinalPrice:C}");
            }

            // ── Validation pipeline ────────────────────────────────────────────
            Console.WriteLine("\n── Validation Pipeline ──");
            var pipeline = new GoodCode.ValidationPipeline(new GoodCode.IValidationRule[]
            {
                new GoodCode.EmailFormatRule(),
                new GoodCode.PasswordLengthRule(10),
                new GoodCode.PasswordComplexityRule(),
                new GoodCode.NameBlocklistRule()
            });

            var errs = pipeline.Validate(new GoodCode.ValidationContext("admin@co.com", "short", "admin"));
            foreach (var e in errs) Console.WriteLine($"  ✗ {e}");

            var ok = pipeline.Validate(new GoodCode.ValidationContext("user@co.com", "Secure123!", "alice"));
            Console.WriteLine(ok.Count == 0 ? "  ✓ Valid" : string.Join(", ", ok));

            // ── Export factory ─────────────────────────────────────────────────
            Console.WriteLine("\n── Export Factory ──");
            var factory = new GoodCode.ExporterFactory(new GoodCode.IReportExporter[]
            {
                new GoodCode.CsvExporter(),
                new GoodCode.JsonExporter(),
                new GoodCode.ExcelExporter()
            });

            var rows = new[] { new GoodCode.ReportRow("Alice", 1200m, DateTimeOffset.UtcNow) };
            var csv  = factory.Get("csv").Export(rows);
            Console.WriteLine($"CSV ({csv.Length} bytes): {System.Text.Encoding.UTF8.GetString(csv)[..40]}...");
        }
    }
}
