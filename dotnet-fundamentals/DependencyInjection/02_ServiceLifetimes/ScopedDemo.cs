// ============================================================
// SCOPED lifetime — one instance per HTTP request (or manual scope)
// The most common lifetime for data-access and business logic services.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DI.Concepts.ServiceLifetimes.Scoped;

// ── 1. DbContext — the canonical scoped service ───────────────────────────────
// EF Core DbContext tracks changes and represents one unit of work.
// One per request = one transaction per request = clean state.

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders   => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
}

// ── 2. Repository — scoped because it wraps DbContext ─────────────────────────
// Repository inherits the scope of the DbContext it depends on.

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<List<Order>> GetAllAsync();
    void Add(Order order);
    Task SaveChangesAsync();
}

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public Task<Order?> GetByIdAsync(int id)
        => _context.Orders.FirstOrDefaultAsync(o => o.Id == id);

    public Task<List<Order>> GetAllAsync()
        => _context.Orders.ToListAsync();

    public void Add(Order order) => _context.Orders.Add(order);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}

// ── 3. Current user service — captures the authenticated user for THIS request ─
public interface ICurrentUserService
{
    string? UserId   { get; }
    string? UserName { get; }
    bool    IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
}

// In ASP.NET Core you'd inject IHttpContextAccessor here.
// Shown simplified for clarity.
public class CurrentUserService : ICurrentUserService
{
    // In real code: inject IHttpContextAccessor and read ClaimsPrincipal
    public string? UserId         { get; private set; }
    public string? UserName       { get; private set; }
    public bool    IsAuthenticated { get; private set; }
    public IEnumerable<string> Roles { get; private set; } = Enumerable.Empty<string>();

    public void SetUser(string userId, string userName, IEnumerable<string> roles)
    {
        UserId         = userId;
        UserName       = userName;
        IsAuthenticated = true;
        Roles          = roles;
    }
}

// ── 4. Unit of Work pattern (coordinates multiple repositories) ────────────────
public interface IUnitOfWork : IDisposable
{
    IOrderRepository Orders { get; }
    Task<int> CommitAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IOrderRepository?     _orders;

    public UnitOfWork(AppDbContext context) => _context = context;

    // Lazy init: repository created only if accessed
    public IOrderRepository Orders
        => _orders ??= new OrderRepository(_context);

    public Task<int> CommitAsync() => _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}

// ── 5. Request-scoped audit collector ─────────────────────────────────────────
// Collects audit events during a request, then flushes them all at the end.
// Must be Scoped — each request gets its own independent list.

public interface IAuditCollector
{
    void Record(string action, string entityId, object? payload = null);
    IReadOnlyList<AuditEvent> Events { get; }
}

public class AuditCollector : IAuditCollector
{
    private readonly List<AuditEvent> _events = new();

    public void Record(string action, string entityId, object? payload = null)
        => _events.Add(new AuditEvent(action, entityId, DateTimeOffset.UtcNow, payload));

    public IReadOnlyList<AuditEvent> Events => _events.AsReadOnly();
}

public record AuditEvent(string Action, string EntityId, DateTimeOffset Timestamp, object? Payload);

// ── Scoped services in non-web contexts (background jobs, worker services) ─────
public class ScopedInBackgroundExample
{
    private readonly IServiceProvider _provider;

    public ScopedInBackgroundExample(IServiceProvider provider) => _provider = provider;

    public async Task ProcessJobAsync()
    {
        // You MUST create an explicit scope in background services
        // because there is no HTTP request to create one automatically.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var orders     = await repository.GetAllAsync();
        Console.WriteLine($"Processing {orders.Count} orders");
    } // scope.Dispose() called here — DbContext is disposed safely
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class ScopedRegistration
{
    public static IServiceCollection Register(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(connectionString));  // AddDbContext registers as Scoped by default

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditCollector, AuditCollector>();

        return services;
    }
}

// Domain models
public class Order   { public int Id { get; set; } public int ProductId { get; set; } }
public class Product { public int Id { get; set; } public string Name { get; set; } = ""; }
