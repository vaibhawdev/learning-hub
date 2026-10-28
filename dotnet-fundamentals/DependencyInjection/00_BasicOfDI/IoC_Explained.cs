// ============================================================
// IoC_Explained.cs
// Inversion of Control — what it means, why it matters, and how DI fits in
//
// IoC is a PRINCIPLE, not a pattern. It says:
//   "Stop letting your class decide what it depends on.
//    Let something external provide those decisions."
//
// The term was coined by Martin Fowler (2004). The patterns below all implement IoC —
// but they vary significantly in how well they support testability and readability.
// ============================================================

// ── Pattern 0: Traditional control (no IoC) ──────────────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.IoC.TraditionalFlow
{
    // The class owns its own dependencies. It decides what to create, when, and how.
    // This is "normal" code — and the source of tight coupling.
    public class ReportGenerator_NaiveControl
    {
        public string GenerateSalesReport()
        {
            // Control lives HERE: class decides to use SQL, local disk, and console.
            var db = new SqlDataFetcher();           // hard decision: SQL only
            var formatter = new CsvFormatter();      // hard decision: CSV only
            var logger = new ConsoleLogger();        // hard decision: console only

            var data = db.FetchSalesData();
            logger.Log("Fetched data, formatting...");
            return formatter.Format(data);

            // Consequences:
            // - Can't swap to a JSON formatter without editing this class.
            // - Can't run tests without a live SQL server.
            // - Can't see what this class needs just by looking at its constructor.
        }
    }

    internal class SqlDataFetcher
    {
        public string[] FetchSalesData() => new[] { "Sale: $100", "Sale: $200" };
    }

    internal class CsvFormatter
    {
        public string Format(string[] data) => string.Join(",", data);
    }

    internal class ConsoleLogger
    {
        public void Log(string msg) => Console.WriteLine($"[LOG] {msg}");
    }
}

// ── Pattern 1: IoC via Factory ────────────────────────────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.IoC.FactoryPattern
{
    // Control is moved to a Factory — the class no longer decides which concrete type it gets.
    // Still a form of IoC: the class doesn't call 'new' — it asks the factory.
    public interface IFormatter { string Format(string[] data); }
    public class CsvFormatter : IFormatter { public string Format(string[] data) => string.Join(",", data); }
    public class JsonFormatter : IFormatter { public string Format(string[] data) => $"[{string.Join(",", data.Select(d => $"\"{d}\""))}]"; }

    public static class FormatterFactory
    {
        // The factory holds the "which concrete type" decision.
        // Swap the concrete type here, and no consumer class needs to change.
        public static IFormatter Create(string type) => type switch
        {
            "json" => new JsonFormatter(),
            _ => new CsvFormatter()
        };
    }

    public class ReportGenerator_Factory
    {
        public string GenerateReport(string formatterType)
        {
            // No 'new JsonFormatter()' here — factory decides.
            IFormatter formatter = FormatterFactory.Create(formatterType);
            return formatter.Format(new[] { "Sale: $100", "Sale: $200" });
        }

        // Limitation: the class still controls WHEN to call the factory and WHAT type string to pass.
        // It's better than nothing, but the class still has some control over its dependency.
    }
}

// ── Pattern 2: IoC via Template Method ────────────────────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.IoC.TemplateMethod
{
    // The base class controls the algorithm skeleton (the "when").
    // Subclasses control the specific steps (the "what").
    // Classic GoF pattern — IoC via inheritance.
    public abstract class ReportGeneratorBase
    {
        // Template method: controls the sequence — subclass cannot change the order.
        public string GenerateReport()
        {
            var data = FetchData();        // step 1 — subclass decides HOW
            var formatted = Format(data);  // step 2 — subclass decides HOW
            Audit(formatted);              // step 3 — subclass decides HOW
            return formatted;
        }

        // "Hollywood Principle": don't call us, we'll call you.
        // The base class calls the abstract methods — control is inverted.
        protected abstract string[] FetchData();
        protected abstract string Format(string[] data);
        protected virtual void Audit(string result) { }  // optional hook
    }

    public class SalesReportFromSql : ReportGeneratorBase
    {
        protected override string[] FetchData() => new[] { "Sale: $100", "Sale: $200" };
        protected override string Format(string[] data) => string.Join(", ", data);
    }

    // Limitation: requires inheritance — can't mix and match independently.
    // If you need SqlFetch + JsonFormat, you need a new subclass. Combinatorial explosion.
}

// ── Pattern 3: IoC via Service Locator (ANTI-PATTERN — shown to explain why it's avoided) ─────────

namespace DI.Concepts.BasicOfDI.IoC.ServiceLocator
{
    // The class asks a global registry for its dependencies at runtime.
    // This IS IoC — the class doesn't 'new' things — but it's the wrong kind.
    public interface IDataFetcher { string[] Fetch(); }
    public interface IFormatter { string Format(string[] data); }

    // The global registry
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T implementation) where T : notnull
            => _services[typeof(T)] = implementation;

        public static T Resolve<T>() => (T)_services[typeof(T)];
    }

    public class ReportGenerator_ServiceLocator
    {
        public string GenerateReport()
        {
            // Dependencies are hidden. You can't tell what this class needs by reading its constructor.
            var fetcher = ServiceLocator.Resolve<IDataFetcher>();  // ← hidden dependency
            var formatter = ServiceLocator.Resolve<IFormatter>();  // ← hidden dependency

            return formatter.Format(fetcher.Fetch());

            // Why this is an anti-pattern:
            // 1. Hidden dependencies — the class's contract (constructor) lies about what it needs.
            // 2. Tests must pre-populate the global ServiceLocator before running any test.
            //    Tests become order-dependent and fragile.
            // 3. The registry is a global mutable state — a source of subtle runtime bugs.
            // 4. Refactoring is dangerous: remove a registration, get a runtime error, not a compile error.
        }
    }

    // Setup code (typically hidden in Program.cs or a bootstrap method):
    public static class ServiceLocatorBootstrap
    {
        public static void Register()
        {
            ServiceLocator.Register<IDataFetcher>(new SqlFetcher());
            ServiceLocator.Register<IFormatter>(new CsvFormatter());
        }
    }

    internal class SqlFetcher : IDataFetcher { public string[] Fetch() => new[] { "Sale: $100" }; }
    internal class CsvFormatter : IFormatter { public string Format(string[] data) => string.Join(",", data); }
}

// ── Pattern 4: IoC via Dependency Injection (RECOMMENDED) ────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.IoC.DependencyInjection
{
    // Dependencies are PUSHED IN from outside — via constructor, property, or method parameter.
    // The class declares what it needs; the caller provides it.
    // This is DI: the recommended form of IoC.
    public interface IDataFetcher { string[] Fetch(); }
    public interface IFormatter { string Format(string[] data); }
    public interface ILogger { void Log(string message); }

    public class ReportGenerator_DI
    {
        private readonly IDataFetcher _fetcher;
        private readonly IFormatter _formatter;
        private readonly ILogger _logger;

        // Constructor injection: the contract is explicit and honest.
        // Readers can see exactly what this class needs — no hidden state, no global lookup.
        public ReportGenerator_DI(IDataFetcher fetcher, IFormatter formatter, ILogger logger)
        {
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GenerateReport()
        {
            var data = _fetcher.Fetch();
            _logger.Log($"Fetched {data.Length} records.");
            return _formatter.Format(data);
        }
    }

    // ── Real implementations ─────────────────────────────────────────────────────────────────────
    public class SqlDataFetcher : IDataFetcher
    {
        public string[] Fetch() => new[] { "Sale: $100", "Sale: $200" };
    }

    public class JsonFormatter : IFormatter
    {
        public string Format(string[] data)
            => $"[{string.Join(", ", data.Select(d => $"\"{d}\""))}]";
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine($"[LOG] {message}");
    }

    // ── Test fakes — trivial because the constructor is honest ───────────────────────────────────
    public class FakeDataFetcher : IDataFetcher
    {
        private readonly string[] _data;
        public FakeDataFetcher(params string[] data) => _data = data;
        public string[] Fetch() => _data;
    }

    public class FakeFormatter : IFormatter
    {
        public string Format(string[] data) => string.Join("|", data);  // deterministic, no I/O
    }

    public class NullLogger : ILogger
    {
        public void Log(string message) { }  // intentional no-op for tests
    }

    // ── Composition Root ─────────────────────────────────────────────────────────────────────────
    // The ONE place that decides which concretions satisfy which abstractions.
    public static class CompositionRoot
    {
        public static ReportGenerator_DI BuildProductionGenerator()
        {
            return new ReportGenerator_DI(
                new SqlDataFetcher(),
                new JsonFormatter(),
                new ConsoleLogger()
            );
        }

        public static ReportGenerator_DI BuildTestGenerator()
        {
            // Inject fakes — no DB, no console I/O, fully deterministic
            return new ReportGenerator_DI(
                new FakeDataFetcher("TestSale: $50", "TestSale: $75"),
                new FakeFormatter(),
                new NullLogger()
            );
        }
    }
}

// ── Pattern 5: IoC Container (automates DI) ───────────────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.IoC.Container
{
    using Microsoft.Extensions.DependencyInjection;

    // Manual DI at the Composition Root gets verbose when the object graph is deep.
    // An IoC Container automates the wiring: you register types once, and the container
    // resolves the entire graph on demand.
    public interface IDataFetcher { string[] Fetch(); }
    public interface IFormatter { string Format(string[] data); }
    public interface ILogger { void Log(string message); }

    public class SqlDataFetcher : IDataFetcher
    {
        public string[] Fetch() => new[] { "Sale: $100", "Sale: $200" };
    }

    public class JsonFormatter : IFormatter
    {
        public string Format(string[] data)
            => $"[{string.Join(", ", data.Select(d => $"\"{d}\""))}]";
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine($"[LOG] {message}");
    }

    public class ReportGenerator
    {
        private readonly IDataFetcher _fetcher;
        private readonly IFormatter _formatter;
        private readonly ILogger _logger;

        public ReportGenerator(IDataFetcher fetcher, IFormatter formatter, ILogger logger)
        {
            _fetcher = fetcher;
            _formatter = formatter;
            _logger = logger;
        }

        public string GenerateReport()
        {
            var data = _fetcher.Fetch();
            _logger.Log($"Fetched {data.Length} records.");
            return _formatter.Format(data);
        }
    }

    public class IoCContainerDemo
    {
        public static void Run()
        {
            // ── Registration: tell the container how to build each abstraction ────────────────────
            var services = new ServiceCollection();
            services.AddTransient<IDataFetcher, SqlDataFetcher>();
            services.AddTransient<IFormatter, JsonFormatter>();
            services.AddSingleton<ILogger, ConsoleLogger>();
            services.AddTransient<ReportGenerator>();  // container resolves its 3 dependencies automatically

            ServiceProvider container = services.BuildServiceProvider();

            // ── Resolution: ask for the root type — container builds the whole graph ─────────────
            // Container sees ReportGenerator needs IDataFetcher, IFormatter, ILogger.
            // It constructs them all and injects them. No manual 'new' needed here.
            var generator = container.GetRequiredService<ReportGenerator>();

            Console.WriteLine("=== IoC Container Demo ===\n");
            string report = generator.GenerateReport();
            Console.WriteLine($"Report: {report}");
        }
    }
}

// ── IoC Pattern Comparison Summary ────────────────────────────────────────────────────────────────

// Pattern            | Hidden deps? | Testable? | Composable? | Recommended?
// -------------------|--------------|-----------|-------------|-------------
// Traditional (new)  | Yes          | No        | No          | No
// Factory            | Partially    | Partial   | Partial     | Situational
// Template Method    | No           | Yes       | No          | Situational
// Service Locator    | Yes (hidden) | Hard      | Yes         | NO (anti-pattern)
// Dependency Injection| No          | Yes       | Yes         | YES
// IoC Container (DI) | No          | Yes       | Yes         | YES (production)
