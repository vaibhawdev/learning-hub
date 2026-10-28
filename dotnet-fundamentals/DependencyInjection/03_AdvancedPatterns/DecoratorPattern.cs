// ============================================================
// DECORATOR PATTERN with DI
// Add caching, logging, retry without modifying original classes.
// Requires Scrutor NuGet: dotnet add package Scrutor
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DI.Concepts.AdvancedPatterns.Decorator;

// ── 1. Core interface and implementation ──────────────────────────────────────
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<List<Product>> GetAllAsync();
    Task AddAsync(Product product);
}

public class SqlProductRepository : IProductRepository
{
    public async Task<Product?> GetByIdAsync(int id)
    {
        Console.WriteLine($"[SQL] SELECT * FROM Products WHERE Id = {id}");
        await Task.Delay(50); // simulate DB latency
        return new Product(id, $"Product {id}", 9.99m);
    }

    public async Task<List<Product>> GetAllAsync()
    {
        Console.WriteLine("[SQL] SELECT * FROM Products");
        await Task.Delay(100);
        return new List<Product> { new(1, "Widget", 9.99m), new(2, "Gadget", 19.99m) };
    }

    public async Task AddAsync(Product product)
    {
        Console.WriteLine($"[SQL] INSERT INTO Products ...");
        await Task.Delay(30);
    }
}

// ── 2. Caching Decorator ─────────────────────────────────────────────────────
// Wraps the real repository — adds a caching layer without touching SqlProductRepository
public class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;  // the wrapped service
    private readonly ConcurrentDictionary<int, Product> _cache = new();

    public CachedProductRepository(IProductRepository inner) => _inner = inner;

    public async Task<Product?> GetByIdAsync(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            Console.WriteLine($"[CACHE HIT] Product {id}");
            return cached;
        }

        var product = await _inner.GetByIdAsync(id);  // delegate to real repo
        if (product is not null)
            _cache[id] = product;

        return product;
    }

    public Task<List<Product>> GetAllAsync() => _inner.GetAllAsync();  // no caching here

    public async Task AddAsync(Product product)
    {
        await _inner.AddAsync(product);
        _cache[product.Id] = product;  // keep cache warm
    }
}

// ── 3. Logging Decorator ──────────────────────────────────────────────────────
public class LoggingProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;

    public LoggingProductRepository(IProductRepository inner) => _inner = inner;

    public async Task<Product?> GetByIdAsync(int id)
    {
        Console.WriteLine($"[REPO LOG] GetByIdAsync({id}) called");
        var result = await _inner.GetByIdAsync(id);
        Console.WriteLine($"[REPO LOG] GetByIdAsync({id}) → {(result is null ? "null" : result.Name)}");
        return result;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        Console.WriteLine("[REPO LOG] GetAllAsync() called");
        var result = await _inner.GetAllAsync();
        Console.WriteLine($"[REPO LOG] GetAllAsync() → {result.Count} items");
        return result;
    }

    public async Task AddAsync(Product product)
    {
        Console.WriteLine($"[REPO LOG] AddAsync({product.Name}) called");
        await _inner.AddAsync(product);
        Console.WriteLine("[REPO LOG] AddAsync() completed");
    }
}

// ── 4. Retry Decorator ────────────────────────────────────────────────────────
public class RetryProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;
    private readonly int _maxAttempts;

    public RetryProductRepository(IProductRepository inner, int maxAttempts = 3)
    {
        _inner = inner;
        _maxAttempts = maxAttempts;
    }

    public async Task<Product?> GetByIdAsync(int id)
        => await RetryAsync(() => _inner.GetByIdAsync(id));

    public async Task<List<Product>> GetAllAsync()
        => await RetryAsync(() => _inner.GetAllAsync());

    public async Task AddAsync(Product product)
    {
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try { await _inner.AddAsync(product); return; }
            catch when (attempt < _maxAttempts)
            {
                Console.WriteLine($"[RETRY] Attempt {attempt} failed. Retrying...");
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> operation)
    {
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try { return await operation(); }
            catch when (attempt < _maxAttempts)
            {
                Console.WriteLine($"[RETRY] Attempt {attempt} failed.");
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }
        return await operation(); // last attempt — let it throw
    }
}

// ── Decorator chain: Logging → Caching → Retry → SQL ────────────────────────
// When a request arrives:
// 1. LoggingProductRepository.GetByIdAsync() logs the call
// 2. CachedProductRepository.GetByIdAsync() checks cache
// 3. RetryProductRepository.GetByIdAsync() handles transient failures
// 4. SqlProductRepository.GetByIdAsync() hits the database

// ── Registration using Scrutor ────────────────────────────────────────────────
public static class DecoratorRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Register the innermost implementation first
        services.AddScoped<IProductRepository, SqlProductRepository>();

        // Scrutor's Decorate() wraps the existing registration
        // Applied in reverse order: last Decorate = outermost layer
        services.Decorate<IProductRepository, RetryProductRepository>();
        services.Decorate<IProductRepository, CachedProductRepository>();
        services.Decorate<IProductRepository, LoggingProductRepository>();

        // Result: Logging(Caching(Retry(Sql)))
        return services;
    }

    // ── Manual decorator wiring WITHOUT Scrutor ──────────────────────────────
    public static IServiceCollection RegisterManually(IServiceCollection services)
    {
        services.AddScoped<IProductRepository>(sp =>
        {
            // Build from inside out
            IProductRepository sql     = new SqlProductRepository();
            IProductRepository retry   = new RetryProductRepository(sql, maxAttempts: 3);
            IProductRepository cached  = new CachedProductRepository(retry);
            IProductRepository logging = new LoggingProductRepository(cached);
            return logging;
        });

        return services;
    }
}

public record Product(int Id, string Name, decimal Price);
