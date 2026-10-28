// ============================================================
// CASE 02: Multi-Tenant Application with Per-Tenant Logging
// Shows how Scoped services carry request context throughout the pipeline
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace DI.Cases.MultiTenant;

// ── Scenario ──────────────────────────────────────────────────────────────────
// SaaS app serving multiple tenants.
// Every log entry, DB query, and error must be tagged with the current tenant.
// The tenant is resolved ONCE per request and flows automatically.

// ── Tenant resolution ─────────────────────────────────────────────────────────
public record TenantInfo(string TenantId, string TenantName, string ConnectionString);

public interface ITenantResolver
{
    TenantInfo? Resolve(string host);
}

public class SubdomainTenantResolver : ITenantResolver
{
    private static readonly Dictionary<string, TenantInfo> _tenants = new()
    {
        ["acme"]    = new("T001", "Acme Corp",  "Server=db1;Database=Acme;"),
        ["globex"]  = new("T002", "Globex Inc", "Server=db2;Database=Globex;"),
        ["initech"] = new("T003", "Initech",    "Server=db3;Database=Initech;"),
    };

    public TenantInfo? Resolve(string host)
    {
        // host: "acme.myapp.com" → subdomain = "acme"
        var subdomain = host.Split('.')[0].ToLower();
        return _tenants.TryGetValue(subdomain, out var tenant) ? tenant : null;
    }
}

// ── Per-request tenant context (Scoped) ───────────────────────────────────────
// This is the KEY scoped service — everything downstream depends on it.
public interface ITenantContext
{
    TenantInfo Tenant { get; }
}

public class TenantContext : ITenantContext
{
    private TenantInfo? _tenant;

    public TenantInfo Tenant => _tenant
        ?? throw new InvalidOperationException("Tenant has not been set for this request.");

    public void SetTenant(TenantInfo tenant) => _tenant = tenant;
}

// ── Per-tenant DbContext (Scoped) ─────────────────────────────────────────────
// Each request gets a DbContext connected to THE TENANT'S database.
public interface ITenantDbContext
{
    IQueryable<T> Query<T>() where T : class;
    Task SaveAsync();
}

public class TenantDbContext : ITenantDbContext
{
    private readonly ITenantContext _tenantContext;

    public TenantDbContext(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        Console.WriteLine($"[DB] Connected to {tenantContext.Tenant.ConnectionString}");
    }

    public IQueryable<T> Query<T>() where T : class => new List<T>().AsQueryable();
    public Task SaveAsync() => Task.CompletedTask;
}

// ── Per-tenant logger (Scoped) ─────────────────────────────────────────────────
// Automatically tags all log entries with tenant info
public interface ITenantLogger
{
    void Info(string message);
    void Error(string message, Exception? ex = null);
}

public class TenantLogger : ITenantLogger
{
    private readonly ITenantContext _tenantContext;
    private readonly string _category;

    public TenantLogger(ITenantContext tenantContext, string category = "App")
    {
        _tenantContext = tenantContext;
        _category      = category;
    }

    public void Info(string message)
        => Console.WriteLine($"[{_tenantContext.Tenant.TenantId}][{_category}] INFO: {message}");

    public void Error(string message, Exception? ex = null)
        => Console.WriteLine($"[{_tenantContext.Tenant.TenantId}][{_category}] ERROR: {message} {ex?.Message}");
}

// ── Tenant-aware service (Scoped) ─────────────────────────────────────────────
public interface IProductService
{
    Task<List<string>> GetProductsAsync();
}

public class ProductService : IProductService
{
    private readonly ITenantDbContext _db;
    private readonly ITenantLogger    _logger;
    private readonly ITenantContext   _tenant;

    public ProductService(ITenantDbContext db, ITenantLogger logger, ITenantContext tenant)
    {
        _db     = db;
        _logger = logger;
        _tenant = tenant;
    }

    public async Task<List<string>> GetProductsAsync()
    {
        _logger.Info($"Getting products for tenant {_tenant.Tenant.TenantName}");
        await Task.Delay(10);
        return new List<string> { "Widget", "Gadget" };
    }
}

// ── Middleware that sets tenant context ────────────────────────────────────────
// In ASP.NET Core this would be registered as middleware.
// Shown here as a class for illustration.
public class TenantMiddleware
{
    private readonly ITenantResolver _resolver;
    private readonly ITenantContext  _tenantContext;

    public TenantMiddleware(ITenantResolver resolver, ITenantContext tenantContext)
    {
        _resolver      = resolver;
        _tenantContext = tenantContext;
    }

    public void SetTenantForRequest(string host)
    {
        var tenant = _resolver.Resolve(host)
            ?? throw new UnauthorizedAccessException($"Unknown tenant for host: {host}");

        // Cast to the concrete type to call SetTenant
        ((TenantContext)_tenantContext).SetTenant(tenant);
    }
}

// ── Registration ──────────────────────────────────────────────────────────────
public static class MultiTenantRegistration
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        // Singleton: stateless, shared across all requests
        services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();

        // Scoped: one TenantContext per request — flows through entire request pipeline
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // Scoped: depend on the per-request tenant
        services.AddScoped<ITenantDbContext, TenantDbContext>();
        services.AddScoped<ITenantLogger, TenantLogger>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<TenantMiddleware>();

        return services;
    }
}

// ── Demo: Simulating two simultaneous requests ────────────────────────────────
// Each scope = one HTTP request, each with its own TenantContext
public static class MultiTenantDemo
{
    public static async Task RunAsync(IServiceProvider rootProvider)
    {
        // Request 1 → Acme tenant
        using (var scope1 = rootProvider.CreateScope())
        {
            var tenantCtx = scope1.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.SetTenant(new TenantInfo("T001", "Acme Corp", "Server=db1;Database=Acme;"));

            var products = scope1.ServiceProvider.GetRequiredService<IProductService>();
            await products.GetProductsAsync();
        }

        // Request 2 → Globex tenant (completely independent scope)
        using (var scope2 = rootProvider.CreateScope())
        {
            var tenantCtx = scope2.ServiceProvider.GetRequiredService<TenantContext>();
            tenantCtx.SetTenant(new TenantInfo("T002", "Globex Inc", "Server=db2;Database=Globex;"));

            var products = scope2.ServiceProvider.GetRequiredService<IProductService>();
            await products.GetProductsAsync();
        }
    }
}
