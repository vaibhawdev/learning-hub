// See 04_Interface_Segregation.md for full guide, fat interface analysis, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SOLID.InterfaceSegregation
{
    namespace BadCode
    {
        // Fat interface — forces all implementors to provide all 8 methods
        public interface IMultiFunctionDevice
        {
            void   Print(byte[] document);
            byte[] Scan();
            void   Fax(byte[] document, string phoneNumber);
            void   Staple(int copies);
            bool   CheckInkLevel();
            void   SetPaperSize(string size);
            void   ColourCalibrate();
            bool   IsOnline();
        }

        // BasicPrinter: can only print and report status — forced to stub 5 methods
        public class BasicPrinter : IMultiFunctionDevice
        {
            public void Print(byte[] doc)  => Console.WriteLine($"[BasicPrinter] Printing {doc.Length} bytes");
            public bool IsOnline()         => true;
            public bool CheckInkLevel()    => true;

            // Forced stubs — these don't apply to a basic printer
            public byte[] Scan()                         => throw new NotImplementedException();
            public void   Fax(byte[] doc, string phone)  => throw new NotImplementedException();
            public void   Staple(int copies)             => throw new NotImplementedException();
            public void   SetPaperSize(string size)      => throw new NotImplementedException();
            public void   ColourCalibrate()              => throw new NotImplementedException();
        }

        // Fat repository interface — read replicas can't write
        public interface IRepository_Fat<T>
        {
            T?             GetById(Guid id);
            IList<T>       GetAll();
            void           Save(T entity);
            void           Delete(Guid id);
            void           BulkInsert(IEnumerable<T> entities);
            void           BulkDelete(IEnumerable<Guid> ids);
            int            Count();
        }

        public class Order { public Guid Id { get; init; } = Guid.NewGuid(); }

        // ReadReplicaRepository can't write — forced to stub 4 methods
        public class ReadReplicaRepository : IRepository_Fat<Order>
        {
            public Order?       GetById(Guid id)                       => null; // real impl: read replica query
            public IList<Order> GetAll()                               => new List<Order>();
            public int          Count()                                => 0;
            public void         Save(Order entity)                     => throw new NotImplementedException();
            public void         Delete(Guid id)                        => throw new NotImplementedException();
            public void         BulkInsert(IEnumerable<Order> entities) => throw new NotImplementedException();
            public void         BulkDelete(IEnumerable<Guid> ids)       => throw new NotImplementedException();
        }
    }

    namespace GoodCode
    {
        // ── Printer: segregated role interfaces ────────────────────────────────
        public interface IPrintable    { void   Print(byte[] document); }
        public interface IScannable    { byte[] Scan(); }
        public interface IFaxable      { void   Fax(byte[] document, string phoneNumber); }
        public interface IStapleable   { void   Staple(int copies); }
        public interface IDeviceStatus { bool   IsOnline(); bool CheckInkLevel(); }

        // ✅ BasicPrinter — only what it genuinely supports. No stubs.
        public sealed class BasicPrinter : IPrintable, IDeviceStatus
        {
            public void Print(byte[] doc)  => Console.WriteLine($"[BasicPrinter] Printing {doc.Length} bytes");
            public bool IsOnline()         => true;
            public bool CheckInkLevel()    => true;
        }

        // ✅ OfficePrinter — supports all roles
        public sealed class OfficePrinter : IPrintable, IScannable, IFaxable, IStapleable, IDeviceStatus
        {
            public void   Print(byte[] doc)               => Console.WriteLine("[OfficePrinter] Printing");
            public byte[] Scan()                          => new byte[] { 0xFF, 0xD8 }; // JPEG header
            public void   Fax(byte[] doc, string phone)   => Console.WriteLine($"[OfficePrinter] Faxing → {phone}");
            public void   Staple(int copies)              => Console.WriteLine($"[OfficePrinter] Stapling {copies}");
            public bool   IsOnline()                      => true;
            public bool   CheckInkLevel()                 => true;
        }

        // ✅ NetworkScanner — only scans
        public sealed class NetworkScanner : IScannable, IDeviceStatus
        {
            public byte[] Scan()          => new byte[] { 0xFF, 0xD8 };
            public bool   IsOnline()      => true;
            public bool   CheckInkLevel() => true;
        }

        // ✅ Each caller depends only on what it uses
        public sealed class DocumentPrintService
        {
            private readonly IPrintable    _printer;
            private readonly IDeviceStatus _status;

            public DocumentPrintService(IPrintable printer, IDeviceStatus status)
                => (_printer, _status) = (printer, status);

            public void PrintDocument(byte[] doc)
            {
                if (!_status.IsOnline()) throw new InvalidOperationException("Device offline");
                if (!_status.CheckInkLevel()) throw new InvalidOperationException("Low ink");
                _printer.Print(doc);
            }
        }

        public sealed class DocumentScanService
        {
            private readonly IScannable _scanner;
            public DocumentScanService(IScannable scanner) => _scanner = scanner;
            public byte[] ScanDocument() => _scanner.Scan();
        }

        public sealed class FaxService
        {
            private readonly IFaxable _fax;
            public FaxService(IFaxable fax) => _fax = fax;
            public void Send(byte[] doc, string phone) => _fax.Fax(doc, phone);
        }

        // ── Repository: segregated read and write contracts ────────────────────
        public sealed class Order { public Guid Id { get; init; } = Guid.NewGuid(); public decimal Total { get; init; } }

        public interface IReadRepository<T>
        {
            Task<T?>               GetByIdAsync(Guid id, CancellationToken ct = default);
            Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
            Task<IReadOnlyList<T>> FindAsync(Func<T, bool> predicate, CancellationToken ct = default);
            Task<int>              CountAsync(CancellationToken ct = default);
        }

        public interface IWriteRepository<T>
        {
            Task SaveAsync(T entity, CancellationToken ct = default);
            Task DeleteAsync(Guid id, CancellationToken ct = default);
            Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken ct = default);
        }

        // Full read-write for primary database
        public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T> { }

        public sealed class InMemoryOrderRepository : IRepository<Order>
        {
            private readonly Dictionary<Guid, Order> _store = new();

            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null);

            public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = _store.Values.ToList();
                return Task.FromResult(r);
            }

            public Task<IReadOnlyList<Order>> FindAsync(Func<Order, bool> pred, CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = _store.Values.Where(pred).ToList();
                return Task.FromResult(r);
            }

            public Task<int> CountAsync(CancellationToken ct = default)
                => Task.FromResult(_store.Count);

            public Task SaveAsync(Order entity, CancellationToken ct = default)
            { _store[entity.Id] = entity; return Task.CompletedTask; }

            public Task DeleteAsync(Guid id, CancellationToken ct = default)
            { _store.Remove(id); return Task.CompletedTask; }

            public Task BulkInsertAsync(IEnumerable<Order> entities, CancellationToken ct = default)
            {
                foreach (var e in entities) _store[e.Id] = e;
                return Task.CompletedTask;
            }
        }

        // ✅ Read replica — only implements IReadRepository<T>, no write stubs
        public sealed class ReadReplicaOrderRepository : IReadRepository<Order>
        {
            public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult<Order?>(null); // real impl: read replica query

            public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = new List<Order>();
                return Task.FromResult(r);
            }

            public Task<IReadOnlyList<Order>> FindAsync(Func<Order, bool> pred, CancellationToken ct = default)
            {
                IReadOnlyList<Order> r = new List<Order>();
                return Task.FromResult(r);
            }

            public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(0);
        }

        // ── CQRS: segregated query and command services ────────────────────────
        public record OrderSummary(Guid Id, string Status, decimal Total);
        public record OrderStats(int TotalOrders, decimal TotalRevenue);
        public record PlaceOrderRequest(string CustomerId, decimal Total);

        public interface IOrderQueryService
        {
            Task<OrderSummary?>        GetSummaryAsync(Guid orderId, CancellationToken ct = default);
            Task<IReadOnlyList<Order>> GetPendingAsync(CancellationToken ct = default);
            Task<OrderStats>           GetStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
        }

        public interface IOrderCommandService
        {
            Task<Guid> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);
            Task       CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default);
            Task       ConfirmPaymentAsync(Guid orderId, string transactionId, CancellationToken ct = default);
        }

        // Dashboard only needs queries
        public sealed class DashboardService
        {
            private readonly IOrderQueryService _queries;
            public DashboardService(IOrderQueryService queries) => _queries = queries;
            public Task<OrderStats> GetTodayStatsAsync(CancellationToken ct = default)
                => _queries.GetStatsAsync(DateTimeOffset.UtcNow.Date, DateTimeOffset.UtcNow, ct);
        }

        // Order API only needs commands
        public sealed class OrderApiHandler
        {
            private readonly IOrderCommandService _commands;
            public OrderApiHandler(IOrderCommandService commands) => _commands = commands;
            public Task<Guid> PlaceAsync(PlaceOrderRequest req, CancellationToken ct = default)
                => _commands.PlaceOrderAsync(req, ct);
        }
    }

    namespace AdvancedConcepts
    {
        // ── Default interface methods — backward-compatible evolution ──────────
        public interface ILogger
        {
            void Log(string level, string message);

            // Defaults — existing loggers don't need to change
            void Info(string msg)    => Log("INFO",    msg);
            void Warning(string msg) => Log("WARNING", msg);
            void Error(string msg)   => Log("ERROR",   msg);
        }

        // Minimal logger — only implements the required member
        public sealed class ConsoleLogger : ILogger
        {
            public void Log(string level, string message)
                => Console.WriteLine($"[{DateTimeOffset.UtcNow:HH:mm:ss}][{level}] {message}");
        }

        // Performance logger — overrides Warning to check level before allocating
        public sealed class FilteredLogger : ILogger
        {
            private readonly string _minLevel;
            private static readonly string[] _levels = { "INFO", "WARNING", "ERROR" };

            public FilteredLogger(string minLevel) => _minLevel = minLevel;

            public void Log(string level, string message)
            {
                if (Array.IndexOf(_levels, level) >= Array.IndexOf(_levels, _minLevel))
                    Console.WriteLine($"[FILTERED][{level}] {message}");
            }

            // Override to skip string allocation when filtered
            void ILogger.Info(string msg)
            {
                if (_minLevel == "INFO") Log("INFO", msg);
            }
        }
    }

    public static class IspDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== ISP DEMO ===\n");

            // ── Printer role interfaces ────────────────────────────────────────
            Console.WriteLine("── Printer role interfaces ──");
            var basic  = new GoodCode.BasicPrinter();
            var office = new GoodCode.OfficePrinter();

            var printService = new GoodCode.DocumentPrintService(basic, basic);
            printService.PrintDocument(new byte[512]);

            var scanService = new GoodCode.DocumentScanService(office);
            var scan = scanService.ScanDocument();
            Console.WriteLine($"Scanned {scan.Length} bytes");

            // BasicPrinter cannot be passed to DocumentScanService — compile error if attempted ✅
            // var bad = new GoodCode.DocumentScanService(basic); // ❌ BasicPrinter doesn't implement IScannable

            // ── Repository: read replica ───────────────────────────────────────
            Console.WriteLine("\n── Repository segregation ──");
            var writeRepo = new GoodCode.InMemoryOrderRepository();
            await writeRepo.SaveAsync(new GoodCode.Order { Total = 250m });
            await writeRepo.SaveAsync(new GoodCode.Order { Total = 750m });

            var count = await writeRepo.CountAsync();
            Console.WriteLine($"Written {count} orders");

            GoodCode.IReadRepository<GoodCode.Order> readRepo = writeRepo;  // use as read-only
            var all = await readRepo.GetAllAsync();
            Console.WriteLine($"Read {all.Count} orders (totals: {string.Join(", ", all.Select(o => o.Total:C))})");

            // ── Default interface methods ──────────────────────────────────────
            Console.WriteLine("\n── Default interface methods ──");
            AdvancedConcepts.ILogger logger  = new AdvancedConcepts.ConsoleLogger();
            AdvancedConcepts.ILogger filtered = new AdvancedConcepts.FilteredLogger("WARNING");

            logger.Info("Application started");
            logger.Warning("Low memory");
            filtered.Info("This is filtered out");
            filtered.Warning("This shows");
            filtered.Error("Critical error");
        }
    }
}
