// ============================================================
// CAPTIVE DEPENDENCY — the most common DI bug in .NET
// Also covers: Transient IDisposable in Singleton, root scope misuse
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Gotchas.CaptiveDependency;

// ── What is a captive dependency? ────────────────────────────────────────────
// A longer-lived service holds a reference to a shorter-lived service.
// The shorter-lived service can never be replaced or properly disposed.

// ────────────────────────────────────────────────────────────────────────────
// GOTCHA 1: Scoped → Singleton (classic captive dependency)
// ────────────────────────────────────────────────────────────────────────────

// BROKEN:
public class ReportGeneratorBroken  // Registered as Singleton
{
    private readonly IDataContext _context;  // Registered as Scoped!

    public ReportGeneratorBroken(IDataContext context)
    {
        _context = context;
        // _context was created during the FIRST request's scope.
        // When that request ends, the scope is disposed → _context is disposed.
        // On the next request, _context is a zombie object.
    }

    public async Task<string> GenerateAsync()
    {
        // Second call: _context.QueryAsync() throws ObjectDisposedException
        return await _context.QueryAsync("SELECT 1");
    }
}

// FIXED — Option A: Make ReportGenerator Scoped too (usually the right call)
public class ReportGeneratorFixed_AsScoped   // Register as Scoped
{
    private readonly IDataContext _context;
    public ReportGeneratorFixed_AsScoped(IDataContext context) => _context = context;
    public Task<string> GenerateAsync() => _context.QueryAsync("SELECT 1");
}

// FIXED — Option B: Use IServiceScopeFactory (when it MUST be Singleton)
public class ReportGeneratorFixed_WithFactory  // Singleton
{
    private readonly IServiceScopeFactory _factory;
    public ReportGeneratorFixed_WithFactory(IServiceScopeFactory factory) => _factory = factory;

    public async Task<string> GenerateAsync()
    {
        // Create a fresh scope for each operation
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        return await context.QueryAsync("SELECT 1");
        // scope.DisposeAsync() called here — context is properly disposed
    }
}

// ────────────────────────────────────────────────────────────────────────────
// GOTCHA 2: Transient IDisposable held by Singleton
// ────────────────────────────────────────────────────────────────────────────

// A Transient means "create new each time". But when a Singleton holds it,
// the Transient is captured forever — effectively becomes a Singleton.
// Worse: if it's IDisposable, it's never disposed until app shuts down.

public class ConnectionManager  // Singleton
{
    // BROKEN: HeavyConnection is Transient + IDisposable
    // It's captured by this Singleton → it's NEVER disposed until app restarts
    private readonly HeavyConnection _connection;

    public ConnectionManager(HeavyConnection connection)  // DON'T DO THIS
        => _connection = connection;

    public void Execute(string sql) => _connection.Execute(sql);
}

// FIXED: Request a fresh instance per operation
public class ConnectionManagerFixed  // Singleton
{
    private readonly IServiceScopeFactory _factory;
    public ConnectionManagerFixed(IServiceScopeFactory factory) => _factory = factory;

    public async Task ExecuteAsync(string sql)
    {
        await using var scope = _factory.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<HeavyConnection>();
        connection.Execute(sql);
        // scope.DisposeAsync() → HeavyConnection.Dispose() called here
    }
}

// ────────────────────────────────────────────────────────────────────────────
// GOTCHA 3: Resolving Scoped from the ROOT IServiceProvider
// ────────────────────────────────────────────────────────────────────────────

// The ROOT IServiceProvider is a Singleton-scoped container.
// Resolving a Scoped service from it creates an instance that NEVER gets disposed.

public class RootProviderMisuse
{
    // BROKEN: calling GetRequiredService on rootProvider without a scope
    public static void Broken(IServiceProvider rootProvider)
    {
        // This Scoped service now lives until the app restarts — it's a memory/resource leak
        var context = rootProvider.GetRequiredService<IDataContext>();  // DON'T
        _ = context.QueryAsync("SELECT 1");
    }

    // FIXED: always use a scope
    public static async Task Fixed(IServiceProvider rootProvider)
    {
        await using var scope = rootProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        _ = await context.QueryAsync("SELECT 1");
    }  // context.Dispose() called here
}

// ────────────────────────────────────────────────────────────────────────────
// How .NET 8 catches this for you
// ────────────────────────────────────────────────────────────────────────────

// In Development, enable validation:
//
// builder.Host.UseDefaultServiceProvider(opts =>
// {
//     opts.ValidateScopes  = true;   // throws when Scoped is resolved from Singleton
//     opts.ValidateOnBuild = true;   // throws at startup if any constructor can't be satisfied
// });
//
// With ValidateScopes = true:
//   GetRequiredService<IScopedService>() on root provider → throws immediately
//   Singleton holding Scoped via constructor → throws on first request
//
// This turns a runtime mystery into an obvious startup error.

// ── Registration showing the problem vs. fix ─────────────────────────────────
public static class CaptiveRegistration
{
    public static void ShowBroken(IServiceCollection services)
    {
        services.AddScoped<IDataContext, FakeDataContext>();
        services.AddSingleton<ReportGeneratorBroken>();  // Scoped captured by Singleton!
        // With ValidateScopes=true, this will throw at runtime on first request.
    }

    public static void ShowFixed(IServiceCollection services)
    {
        services.AddScoped<IDataContext, FakeDataContext>();
        services.AddScoped<ReportGeneratorFixed_AsScoped>();        // both Scoped — OK
        services.AddSingleton<ReportGeneratorFixed_WithFactory>();   // Singleton using factory — OK
    }
}

// Stubs
public interface IDataContext : IAsyncDisposable
{
    Task<string> QueryAsync(string sql);
}

public class FakeDataContext : IDataContext
{
    private bool _disposed;
    public Task<string> QueryAsync(string sql)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FakeDataContext));
        return Task.FromResult("data");
    }
    public ValueTask DisposeAsync() { _disposed = true; return ValueTask.CompletedTask; }
}

public class HeavyConnection : IDisposable
{
    public HeavyConnection()  => Console.WriteLine("[HeavyConnection] Created (expensive!)");
    public void Execute(string sql) => Console.WriteLine($"[HeavyConnection] {sql}");
    public void Dispose()           => Console.WriteLine("[HeavyConnection] Disposed");
}
