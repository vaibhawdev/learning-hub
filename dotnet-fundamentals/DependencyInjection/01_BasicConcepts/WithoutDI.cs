// ============================================================
// WITHOUT Dependency Injection — the "before" state
// This file shows exactly what problems DI solves.
// ============================================================

namespace DI.Concepts.BasicConcepts.WithoutDI;

// ── Problem 1: Class creates its own dependencies ─────────────────────────────
// OrderService knows exactly WHICH database and email provider to use.
// You cannot swap either without editing OrderService.
public class OrderService_Tight
{
    // Hard-coded: creates concrete types directly
    private readonly SqlOrderRepository _repository = new SqlOrderRepository();
    private readonly SmtpEmailService   _emailer    = new SmtpEmailService("smtp.prod.com", 587);

    public void PlaceOrder(int productId, int qty)
    {
        _repository.Save(new Order(productId, qty));
        _emailer.Send("user@example.com", "Order placed!");
    }
}

// ── Problem 2: Cannot unit-test without real infrastructure ───────────────────
// To test PlaceOrder() you MUST have:
//   • a SQL Server running and a matching schema
//   • an SMTP server accepting connections
// This makes tests slow, flaky, and environment-dependent.

// ── Problem 3: Static dependencies are even worse ─────────────────────────────
public class AuditLogger_Static
{
    public void Log(string msg)
    {
        // Calls static method directly — completely impossible to mock
        FileLogger.WriteToFile(msg);
    }
}

// ── Problem 4: "new" keyword chains scatter configuration ─────────────────────
// Each team member might create SqlOrderRepository with DIFFERENT connection
// strings. There is no single place to change it.

// ── Infrastructure stubs (simulating what you'd normally reference) ────────────
internal class Order
{
    public Order(int productId, int qty) { }
}

internal class SqlOrderRepository
{
    // Hardcodes its own connection string — another smell
    private const string ConnStr = "Server=prod-db;Database=Orders;";
    public void Save(Order order) { /* hits SQL */ }
}

internal class SmtpEmailService
{
    public SmtpEmailService(string host, int port) { }
    public void Send(string to, string body) { /* hits SMTP */ }
}

internal static class FileLogger
{
    public static void WriteToFile(string msg) { /* writes to disk */ }
}
