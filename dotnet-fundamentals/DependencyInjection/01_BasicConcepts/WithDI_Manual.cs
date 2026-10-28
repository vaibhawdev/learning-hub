// ============================================================
// MANUAL Dependency Injection — no container, pure constructor injection
// Use this to understand DI mechanics before adding a container.
// ============================================================

namespace DI.Concepts.BasicConcepts.ManualDI;

// ── Step 1: Define abstractions (interfaces) ──────────────────────────────────

public interface IOrderRepository
{
    void Save(Order order);
    Order? GetById(int id);
}

public interface IEmailService
{
    void Send(string to, string subject, string body);
}

public interface ILogger
{
    void Log(string message);
}

// ── Step 2: Write business logic against abstractions ONLY ────────────────────

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService    _emailService;
    private readonly ILogger          _logger;

    // Constructor injection — dependencies are PUSHED in, never pulled
    public OrderService(
        IOrderRepository repository,
        IEmailService    emailService,
        ILogger          logger)
    {
        // Guard clauses: fail fast if wiring is missing
        _repository   = repository   ?? throw new ArgumentNullException(nameof(repository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));
    }

    public void PlaceOrder(int productId, int qty, string customerEmail)
    {
        var order = new Order(productId, qty);
        _repository.Save(order);
        _emailService.Send(customerEmail, "Order Confirmed", $"Your order #{order.Id} is placed.");
        _logger.Log($"Order {order.Id} placed for {customerEmail}");
    }
}

// ── Step 3: Write concrete implementations ────────────────────────────────────

public class SqlOrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public SqlOrderRepository(string connectionString)
        => _connectionString = connectionString;

    public void Save(Order order)
        => Console.WriteLine($"[SQL] Saving order {order.Id} to {_connectionString}");

    public Order? GetById(int id)
    {
        Console.WriteLine($"[SQL] Fetching order {id}");
        return new Order(1, 1);
    }
}

public class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int    _port;

    public SmtpEmailService(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public void Send(string to, string subject, string body)
        => Console.WriteLine($"[SMTP:{_host}:{_port}] → {to}: {subject}");
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[LOG] {message}");
}

// ── MOCK implementations for unit tests ───────────────────────────────────────
// These live in your test project, but shown here for illustration.

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _store = new();

    public void Save(Order order)        => _store.Add(order);
    public Order? GetById(int id)        => _store.FirstOrDefault(o => o.Id == id);
    public IReadOnlyList<Order> GetAll() => _store.AsReadOnly();
}

public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject)> SentEmails { get; } = new();

    public void Send(string to, string subject, string body)
        => SentEmails.Add((to, subject));
}

// ── Step 4: Wire everything at the Composition Root (entry point) ─────────────

public static class ManualCompositionRoot
{
    public static void Run()
    {
        // All "new" keywords live HERE — not scattered through business code
        var repository = new SqlOrderRepository("Server=dev-db;Database=Orders;");
        var emailer    = new SmtpEmailService("smtp.dev.com", 587);
        var logger     = new ConsoleLogger();

        var orderService = new OrderService(repository, emailer, logger);
        orderService.PlaceOrder(productId: 42, qty: 3, customerEmail: "alice@example.com");
    }

    // For unit tests, wire fake implementations instead:
    public static OrderService BuildForTest()
    {
        return new OrderService(
            repository:   new InMemoryOrderRepository(),
            emailService: new FakeEmailService(),
            logger:       new ConsoleLogger());
    }
}

// ── Domain model ──────────────────────────────────────────────────────────────
public class Order
{
    private static int _nextId = 1;
    public int Id        { get; }
    public int ProductId { get; }
    public int Quantity  { get; }

    public Order(int productId, int qty)
    {
        Id        = _nextId++;
        ProductId = productId;
        Quantity  = qty;
    }
}
