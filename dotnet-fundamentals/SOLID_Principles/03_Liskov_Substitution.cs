// See 03_Liskov_Substitution.md for formal rules, Square-Rectangle analysis, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SOLID.LiskovSubstitution
{
    namespace BadCode
    {
        // ── Square-Rectangle violation ──────────────────────────────────────────
        public class Rectangle
        {
            public virtual int Width  { get; set; }
            public virtual int Height { get; set; }
            public int Area() => Width * Height;
        }

        public class Square : Rectangle
        {
            public override int Width
            {
                get => base.Width;
                set { base.Width = value; base.Height = value; }  // breaks the postcondition
            }

            public override int Height
            {
                get => base.Height;
                set { base.Height = value; base.Width = value; }
            }
        }

        // Works for Rectangle, breaks silently for Square
        public static class RectangleTester
        {
            public static void DoubleWidth(Rectangle r)
            {
                var originalHeight = r.Height;
                r.Width *= 2;

                // For Square: setting Width also sets Height → original height is gone
                var expected = r.Width * originalHeight;
                var actual   = r.Area();
                Console.WriteLine($"Expected area: {expected}, Actual area: {actual}, Correct: {expected == actual}");
            }
        }

        // ── NotImplementedException violation ───────────────────────────────────
        public abstract class ReportGenerator_Fat
        {
            public abstract byte[] GeneratePdf();
            public abstract byte[] GenerateCsv();
            public abstract byte[] GenerateExcel();
            public abstract void   EmailReport(string to);
            public abstract void   ArchiveReport(string path);
        }

        // Only generates PDF — forced to stub 4 methods
        public class SimplePdfReport : ReportGenerator_Fat
        {
            public override byte[] GeneratePdf()   => new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
            public override byte[] GenerateCsv()   => throw new NotImplementedException();
            public override byte[] GenerateExcel() => throw new NotImplementedException();
            public override void   EmailReport(string to)     => throw new NotImplementedException();
            public override void   ArchiveReport(string path) => throw new NotImplementedException();
        }

        // ── Strengthened precondition violation ────────────────────────────────
        public class OrderRepository_Base
        {
            // Base contract: persists any non-null Order
            public virtual void Save(Order_Bad order)
            {
                ArgumentNullException.ThrowIfNull(order);
                Console.WriteLine($"[Base] Saved order {order.Id}");
            }
        }

        public class AuditedOrderRepository : OrderRepository_Base
        {
            // ❌ Strengthens precondition — rejects orders base would have accepted
            public override void Save(Order_Bad order)
            {
                if (order.Total < 10)
                    throw new InvalidOperationException("Orders under $10 cannot be audited");
                base.Save(order);
            }
        }

        public class Order_Bad { public Guid Id { get; } = Guid.NewGuid(); public decimal Total { get; init; } }

        // Caller using base type breaks when AuditedOrderRepository is injected
        public static class OrderProcessor_Bad
        {
            public static void Process(OrderRepository_Base repo, IEnumerable<Order_Bad> orders)
            {
                foreach (var o in orders)
                    repo.Save(o);  // throws for small orders if AuditedOrderRepository is injected
            }
        }
    }

    namespace GoodCode
    {
        // ── Fix 1: Flatten Rectangle/Square — shared interface, no subclassing ──
        public interface IShape
        {
            double Area();
            double Perimeter();
        }

        public sealed class Rectangle : IShape
        {
            public double Width  { get; }
            public double Height { get; }

            public Rectangle(double width, double height)
            {
                if (width  <= 0) throw new ArgumentException("Width must be positive");
                if (height <= 0) throw new ArgumentException("Height must be positive");
                Width  = width;
                Height = height;
            }

            // Immutable — returns new instance; no invariant violation possible
            public Rectangle WithWidth(double w)  => new(w, Height);
            public Rectangle WithHeight(double h) => new(Width, h);

            public double Area()      => Width * Height;
            public double Perimeter() => 2 * (Width + Height);
        }

        public sealed class Square : IShape
        {
            public double Side { get; }

            public Square(double side)
            {
                if (side <= 0) throw new ArgumentException("Side must be positive");
                Side = side;
            }

            public double Area()      => Side * Side;
            public double Perimeter() => 4 * Side;
        }

        // ✅ Works correctly for both types — no broken assertions
        public static class ShapeUtils
        {
            public static void PrintInfo(IShape shape)
                => Console.WriteLine($"{shape.GetType().Name}: Area={shape.Area():F2}, Perimeter={shape.Perimeter():F2}");
        }

        // ── Fix 2: ISP solves NotImplementedException ─────────────────────────
        public interface IPdfExportable  { byte[] ExportPdf(); }
        public interface ICsvExportable  { byte[] ExportCsv(); }
        public interface IEmailSendable  { Task SendEmailAsync(string to, CancellationToken ct = default); }
        public interface IArchiveable    { Task ArchiveAsync(string path, CancellationToken ct = default); }

        // ✅ Only implements what it genuinely supports — no stubs, LSP honoured
        public sealed class SimplePdfReport : IPdfExportable
        {
            public byte[] ExportPdf() => new byte[] { 0x25, 0x50, 0x44, 0x46 };
        }

        public sealed class FullReport : IPdfExportable, ICsvExportable, IEmailSendable, IArchiveable
        {
            public byte[] ExportPdf() => new byte[] { 0x25, 0x50, 0x44, 0x46 };
            public byte[] ExportCsv() => System.Text.Encoding.UTF8.GetBytes("Name,Amount\n");
            public Task SendEmailAsync(string to, CancellationToken ct = default)
            { Console.WriteLine($"[EMAIL] Report → {to}"); return Task.CompletedTask; }
            public Task ArchiveAsync(string path, CancellationToken ct = default)
            { Console.WriteLine($"[ARCHIVE] → {path}"); return Task.CompletedTask; }
        }

        // Callers request exactly the capability they need
        public static class ReportActions
        {
            public static void ExportToCsv(ICsvExportable report)
            {
                var data = report.ExportCsv();  // guaranteed — only ICsvExportable accepted
                Console.WriteLine($"Exported {data.Length} bytes of CSV");
            }

            public static async Task SendReportEmail(IEmailSendable report, string to)
            {
                await report.SendEmailAsync(to);
            }
        }

        // ── Fix 3: Audit via decorator — doesn't change the contract ──────────
        public sealed class Order { public Guid Id { get; } = Guid.NewGuid(); public decimal Total { get; init; } }

        public interface IOrderRepository
        {
            // Contract: persists any non-null, valid Order. Never strengthened.
            Task SaveAsync(Order order, CancellationToken ct = default);
            Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
        }

        public sealed class InMemoryOrderRepository : IOrderRepository
        {
            private readonly Dictionary<Guid, Order> _store = new();

            public Task SaveAsync(Order order, CancellationToken ct = default)
            {
                ArgumentNullException.ThrowIfNull(order);
                _store[order.Id] = order;
                return Task.CompletedTask;
            }

            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null);
        }

        public interface IAuditLog { Task RecordAsync(string msg, CancellationToken ct = default); }

        public sealed class ConsoleAuditLog : IAuditLog
        {
            public Task RecordAsync(string msg, CancellationToken ct = default)
            { Console.WriteLine($"[AUDIT] {msg}"); return Task.CompletedTask; }
        }

        // ✅ Audit via decorator — same precondition as the inner repo, LSP intact
        public sealed class AuditingOrderRepository : IOrderRepository
        {
            private readonly IOrderRepository _inner;
            private readonly IAuditLog        _audit;

            public AuditingOrderRepository(IOrderRepository inner, IAuditLog audit)
                => (_inner, _audit) = (inner, audit);

            public async Task SaveAsync(Order order, CancellationToken ct = default)
            {
                await _audit.RecordAsync($"Saving order {order.Id} (total={order.Total:C})", ct);
                await _inner.SaveAsync(order, ct);  // same precondition — no strengthening
            }

            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => _inner.GetByIdAsync(id, ct);
        }
    }

    namespace AdvancedConcepts
    {
        // ── Covariance/Contravariance — type-system LSP enforcement ──────────
        public class Animal  { public string Name { get; init; } = ""; }
        public class Dog : Animal { public string Breed { get; init; } = ""; }

        public interface IProducer<out T> { T Produce(); }
        public interface IConsumer<in T>  { void Consume(T item); }

        public sealed class DogProducer : IProducer<Dog>
        {
            public Dog Produce() => new() { Name = "Rex", Breed = "Labrador" };
        }

        public sealed class AnimalConsumer : IConsumer<Animal>
        {
            public void Consume(Animal a) => Console.WriteLine($"Processing animal: {a.Name}");
        }

        public static class VarianceDemo
        {
            public static void Show()
            {
                IProducer<Dog>    dogProducer    = new DogProducer();
                IProducer<Animal> animalProducer = dogProducer;  // covariance: Dog producer IS-A Animal producer
                Console.WriteLine($"Produced: {animalProducer.Produce().Name}");

                IConsumer<Animal> animalConsumer = new AnimalConsumer();
                IConsumer<Dog>    dogConsumer    = animalConsumer;  // contravariance
                dogConsumer.Consume(new Dog { Name = "Buddy", Breed = "Poodle" });
            }
        }
    }

    public static class LspDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== LSP DEMO ===\n");

            // ── Square-Rectangle violation ─────────────────────────────────────
            Console.WriteLine("── Bad: Square extends Rectangle ──");
            BadCode.Rectangle rect   = new BadCode.Rectangle { Width = 4, Height = 5 };
            BadCode.Rectangle square = new BadCode.Square    { Width = 4 };
            BadCode.RectangleTester.DoubleWidth(rect);    // Correct: true
            BadCode.RectangleTester.DoubleWidth(square);  // Broken:  false

            // ── Fixed: flat interface hierarchy ────────────────────────────────
            Console.WriteLine("\n── Good: IShape hierarchy ──");
            GoodCode.IShape[] shapes =
            {
                new GoodCode.Rectangle(4, 5),
                new GoodCode.Square(4)
            };
            foreach (var s in shapes) GoodCode.ShapeUtils.PrintInfo(s);

            // ── ISP fixes NotImplementedException ─────────────────────────────
            Console.WriteLine("\n── Good: role interfaces ──");
            var full   = new GoodCode.FullReport();
            var simple = new GoodCode.SimplePdfReport();

            GoodCode.ReportActions.ExportToCsv(full);   // ✅ works
            // GoodCode.ReportActions.ExportToCsv(simple); // won't compile — SimplePdfReport doesn't implement ICsvExportable ✅

            // ── Decorator audit — same contract ────────────────────────────────
            Console.WriteLine("\n── Good: audit decorator ──");
            GoodCode.IOrderRepository repo = new GoodCode.AuditingOrderRepository(
                new GoodCode.InMemoryOrderRepository(),
                new GoodCode.ConsoleAuditLog());

            var order = new GoodCode.Order { Total = 5m };  // small order — no longer rejected
            await repo.SaveAsync(order);
            Console.WriteLine($"Saved order {order.Id}");

            // ── Variance demo ──────────────────────────────────────────────────
            Console.WriteLine("\n── Covariance/Contravariance ──");
            AdvancedConcepts.VarianceDemo.Show();
        }
    }
}
