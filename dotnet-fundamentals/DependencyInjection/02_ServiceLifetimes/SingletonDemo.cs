// ============================================================
// SINGLETON lifetime — one instance for the entire app lifetime
// Key concern: THREAD SAFETY — multiple requests hit it concurrently
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DI.Concepts.ServiceLifetimes.Singleton;

// ── 1. Correct: thread-safe in-memory cache ───────────────────────────────────
public interface IAppCache
{
    void Set(string key, object value, TimeSpan ttl);
    bool TryGet<T>(string key, out T? value);
    void Remove(string key);
}

public sealed class InMemoryAppCache : IAppCache
{
    // ConcurrentDictionary makes this thread-safe without manual locking
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

    public void Set(string key, object value, TimeSpan ttl)
    {
        var entry = new CacheEntry(value, DateTimeOffset.UtcNow.Add(ttl));
        _store[key] = entry;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_store.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            value = (T)entry.Value;
            return true;
        }
        _store.TryRemove(key, out _);
        value = default;
        return false;
    }

    public void Remove(string key) => _store.TryRemove(key, out _);

    private record CacheEntry(object Value, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    }
}

// ── 2. Correct: request counter (uses Interlocked for atomicity) ───────────────
public interface IRequestCounter
{
    long Increment();
    long CurrentCount { get; }
}

public sealed class AtomicRequestCounter : IRequestCounter
{
    private long _count = 0;

    // Interlocked.Increment is atomic — no lock needed
    public long Increment() => Interlocked.Increment(ref _count);
    public long CurrentCount => Interlocked.Read(ref _count);
}

// ── 3. Correct: configuration wrapper (immutable after startup) ────────────────
// Immutable objects are always thread-safe — perfect for Singleton
public sealed class AppSettings
{
    public string DatabaseConnection { get; init; } = "";
    public string SmtpHost           { get; init; } = "";
    public int    SmtpPort           { get; init; } = 587;
    public bool   FeatureXEnabled    { get; init; } = false;
}

// ── 4. Wrong: mutable state without synchronization ──────────────────────────
public sealed class UnsafeSingletonCounter  // DON'T do this
{
    private int _count = 0;

    // Race condition: two threads can read _count = 5, both compute 6, both write 6
    // Result: one increment is lost
    public void Increment() => _count++;  // NOT thread-safe!
    public int Count => _count;
}

// ── 5. Correct: lazy initialization of expensive resource ─────────────────────
public interface IExpensiveService
{
    string GetData();
}

public sealed class LazyInitializedService : IExpensiveService
{
    // Lazy<T> is thread-safe by default (LazyThreadSafetyMode.ExecutionAndPublication)
    private readonly Lazy<ExpensiveResource> _resource;

    public LazyInitializedService()
        => _resource = new Lazy<ExpensiveResource>(() => new ExpensiveResource());

    public string GetData() => _resource.Value.Compute();

    private class ExpensiveResource
    {
        public ExpensiveResource() => Console.WriteLine("[SLOW] Initializing expensive resource...");
        public string Compute()    => "data";
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class SingletonRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        services.AddSingleton<IAppCache, InMemoryAppCache>();
        services.AddSingleton<IRequestCounter, AtomicRequestCounter>();
        services.AddSingleton<IExpensiveService, LazyInitializedService>();

        // Register AppSettings loaded from config (common pattern)
        services.AddSingleton(new AppSettings
        {
            DatabaseConnection = "Server=prod;",
            SmtpHost = "smtp.prod.com",
            SmtpPort = 587
        });

        return services;
    }
}

// ── GOTCHA: Singleton holding DbContext (WRONG) ───────────────────────────────
// DbContext is NOT thread-safe and MUST be Scoped, never Singleton.
// The following class shows what NOT to do:
//
// public class BadDataService  // <- registered as Singleton
// {
//     private readonly AppDbContext _context;  // <- DbContext is Scoped
//     public BadDataService(AppDbContext context) { _context = context; }
//     // Now _context is a SCOPED object captured by a SINGLETON!
//     // After the first request ends, _context is disposed but BadDataService holds it.
//     // Next request → ObjectDisposedException
// }
