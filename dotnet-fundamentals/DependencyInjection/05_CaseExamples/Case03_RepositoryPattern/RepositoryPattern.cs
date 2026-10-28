// ============================================================
// CASE 03: Generic Repository Pattern with Unit of Work
// Classic data-access pattern — shows how DI coordinates
// multiple repositories sharing a single DbContext.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Cases.Repository;

// ── Generic repository interface ──────────────────────────────────────────────
public interface IRepository<TEntity, TId> where TEntity : class
{
    Task<TEntity?>           GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Func<TEntity, bool> predicate, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Delete(TEntity entity);
}

// ── Generic EF Core implementation ───────────────────────────────────────────
// AppDbContext is injected — all repositories within a scope share the SAME context
public class EfRepository<TEntity, TId> : IRepository<TEntity, TId> where TEntity : class
{
    protected readonly AppDbContext Context;

    public EfRepository(AppDbContext context) => Context = context;

    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        // Real impl: return Context.Set<TEntity>().FindAsync(new object?[] { id }, ct).AsTask();
        Console.WriteLine($"[EF] FindAsync<{typeof(TEntity).Name}>({id})");
        return Task.FromResult<TEntity?>(null);
    }

    public Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[EF] GetAll<{typeof(TEntity).Name}>()");
        return Task.FromResult<IReadOnlyList<TEntity>>(new List<TEntity>());
    }

    public Task<IReadOnlyList<TEntity>> FindAsync(Func<TEntity, bool> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TEntity>>(new List<TEntity>());

    public Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        Console.WriteLine($"[EF] Add<{typeof(TEntity).Name}>()");
        // Context.Set<TEntity>().Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity) => Console.WriteLine($"[EF] Update<{typeof(TEntity).Name}>()");
    public void Delete(TEntity entity) => Console.WriteLine($"[EF] Remove<{typeof(TEntity).Name}>()");
}

// ── Specialised repositories (extend generic with domain-specific queries) ────
public interface IOrderRepository : IRepository<Order, int>
{
    Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(string customerId, CancellationToken ct = default);
    Task<Order?> GetOrderWithItemsAsync(int orderId, CancellationToken ct = default);
}

public class OrderRepository : EfRepository<Order, int>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(string customerId, CancellationToken ct = default)
    {
        Console.WriteLine($"[EF] Orders for customer {customerId}");
        return Task.FromResult<IReadOnlyList<Order>>(new List<Order>());
    }

    public Task<Order?> GetOrderWithItemsAsync(int orderId, CancellationToken ct = default)
    {
        Console.WriteLine($"[EF] Order {orderId} with items");
        return Task.FromResult<Order?>(null);
    }
}

public interface IProductRepository : IRepository<Product, int>
{
    Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<bool> ExistsAsync(int productId, CancellationToken ct = default);
}

public class ProductRepository : EfRepository<Product, int>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        Console.WriteLine($"[EF] Products in category {category}");
        return Task.FromResult<IReadOnlyList<Product>>(new List<Product>());
    }

    public Task<bool> ExistsAsync(int productId, CancellationToken ct = default)
        => Task.FromResult(true);
}

// ── Unit of Work ──────────────────────────────────────────────────────────────
// Coordinates all repositories — one save = one database transaction
public interface IUnitOfWork : IAsyncDisposable
{
    IOrderRepository   Orders   { get; }
    IProductRepository Products { get; }
    Task<int> CommitAsync(CancellationToken ct = default);
    Task RollbackAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IOrderRepository?   _orders;
    private IProductRepository? _products;

    public UnitOfWork(AppDbContext context) => _context = context;

    // Lazy-created repositories — all share the same _context instance
    public IOrderRepository   Orders   => _orders   ??= new OrderRepository(_context);
    public IProductRepository Products => _products ??= new ProductRepository(_context);

    public Task<int> CommitAsync(CancellationToken ct = default)
    {
        Console.WriteLine("[UoW] Committing transaction");
        // return _context.SaveChangesAsync(ct);
        return Task.FromResult(0);
    }

    public Task RollbackAsync()
    {
        Console.WriteLine("[UoW] Rolling back transaction");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // await _context.DisposeAsync();
        Console.WriteLine("[UoW] Disposed");
        await Task.CompletedTask;
    }
}

// ── Application service using Unit of Work ────────────────────────────────────
public class TransferOrderService
{
    private readonly IUnitOfWork _uow;

    public TransferOrderService(IUnitOfWork uow) => _uow = uow;

    public async Task TransferOrderAsync(int orderId, string newCustomerId)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null) throw new InvalidOperationException($"Order {orderId} not found.");

        // Verify new customer has products
        var products = await _uow.Products.GetByCategoryAsync("electronics");

        order.CustomerId = newCustomerId;
        _uow.Orders.Update(order);

        // Single commit covers BOTH operations (orders + products) in one transaction
        await _uow.CommitAsync();
        Console.WriteLine($"[Transfer] Order {orderId} transferred to {newCustomerId}");
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class RepositoryRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // DbContext — Scoped so all repos within a request share it
        services.AddScoped<AppDbContext>();

        // Unit of Work — Scoped, wraps the DbContext
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories — Scoped, all receive the same AppDbContext from the scope
        services.AddScoped<IOrderRepository,   OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        // Open generic registration (register all repos at once)
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));

        // Application services
        services.AddScoped<TransferOrderService>();

        return services;
    }
}

// Domain models
public class Order
{
    public int    Id         { get; set; }
    public string CustomerId { get; set; } = "";
}

public class Product
{
    public int    Id       { get; set; }
    public string Name     { get; set; } = "";
    public string Category { get; set; } = "";
}

public class AppDbContext { /* EF Core DbContext */ }
