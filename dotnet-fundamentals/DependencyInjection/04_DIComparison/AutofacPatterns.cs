// ============================================================
// AUTOFAC — Enterprise DI container
// dotnet add package Autofac
// dotnet add package Autofac.Extensions.DependencyInjection
// ============================================================
// NOTE: Code below is reference/documentation style.
// To run it, add the NuGet packages and uncomment.

namespace DI.Concepts.Comparison.Autofac;

// ── 1. Integrating Autofac with ASP.NET Core ──────────────────────────────────
//
// Program.cs:
//   builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
//   builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
//   {
//       containerBuilder.RegisterModule<OrderModule>();
//       containerBuilder.RegisterModule<InfrastructureModule>();
//   });

// ── 2. Autofac Module — grouping related registrations ────────────────────────
// public class OrderModule : Module
// {
//     protected override void Load(ContainerBuilder builder)
//     {
//         builder.RegisterType<OrderService>()
//                .As<IOrderService>()
//                .InstancePerLifetimeScope();           // = Scoped
//
//         builder.RegisterType<SqlOrderRepository>()
//                .As<IOrderRepository>()
//                .InstancePerLifetimeScope();
//
//         builder.RegisterType<SmtpEmailSender>()
//                .As<IEmailSender>()
//                .SingleInstance();                     // = Singleton
//
//         // Named registrations
//         builder.RegisterType<StripeGateway>()
//                .Named<IPaymentGateway>("stripe")
//                .SingleInstance();
//
//         builder.RegisterType<PayPalGateway>()
//                .Named<IPaymentGateway>("paypal")
//                .SingleInstance();
//
//         // Assembly scanning
//         builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
//                .Where(t => t.Name.EndsWith("Service"))
//                .AsImplementedInterfaces()
//                .InstancePerLifetimeScope();
//
//         // Open generics
//         builder.RegisterGeneric(typeof(EfRepository<>))
//                .As(typeof(IRepository<>))
//                .InstancePerLifetimeScope();
//
//         // Decorator
//         builder.RegisterDecorator<CachedOrderService, IOrderService>();
//     }
// }

// ── 3. Property Injection (NOT supported in built-in DI) ─────────────────────
// Autofac can inject into public properties — useful for legacy code
// where constructor injection is too disruptive.
//
// public class LegacyController
// {
//     // Property injection — Autofac sets this automatically
//     public ILogger Logger { get; set; }  // no constructor needed
// }
//
// builder.RegisterType<LegacyController>()
//        .PropertiesAutowired();            // inject all public properties

// ── 4. Method Injection ───────────────────────────────────────────────────────
// Autofac can call a method on an object after creation:
//
// public class Plugin
// {
//     public void Initialize(IConfiguration config) { /* setup */ }
// }
//
// builder.RegisterType<Plugin>()
//        .OnActivating(e => e.Instance.Initialize(e.Context.Resolve<IConfiguration>()));

// ── 5. AOP / Interception with Castle.Core ────────────────────────────────────
// dotnet add package Autofac.Extras.DynamicProxy
// dotnet add package Castle.Core
//
// public class LoggingInterceptor : IInterceptor
// {
//     public void Intercept(IInvocation invocation)
//     {
//         Console.WriteLine($"Before: {invocation.Method.Name}");
//         invocation.Proceed();
//         Console.WriteLine($"After: {invocation.Method.Name}");
//     }
// }
//
// builder.RegisterType<LoggingInterceptor>();
// builder.RegisterType<OrderService>()
//        .As<IOrderService>()
//        .EnableInterfaceInterceptors()
//        .InterceptedBy(typeof(LoggingInterceptor));

// ── 6. Lifetime scopes in Autofac ────────────────────────────────────────────
// Autofac term         | .NET DI equivalent
// ---------------------|-------------------
// SingleInstance       | Singleton
// InstancePerLifetimeScope | Scoped (per HTTP request in ASP.NET)
// InstancePerRequest   | Scoped (older, HTTP-specific)
// InstancePerDependency| Transient (default)
// InstancePerMatchingLifetimeScope("tag") | Custom named scope

// ── 7. Resolving at runtime (Autofac-specific) ────────────────────────────────
// var gateway = container.ResolveNamed<IPaymentGateway>("stripe");
// var allGateways = container.Resolve<IEnumerable<IPaymentGateway>>();

// ── Autofac vs Built-in summary ──────────────────────────────────────────────
// Autofac pros:
//   - Full decorator support (RegisterDecorator)
//   - Assembly scanning built-in
//   - Property and method injection
//   - Named registrations
//   - AOP via Castle.Core
//   - Lifetime scopes with tags
//
// Autofac cons:
//   - Extra dependency and learning curve
//   - Slightly slower than built-in
//   - Configuration is more verbose (Module pattern)
//   - Some patterns conflict with minimal hosting model

// ── Placeholder types for reference ──────────────────────────────────────────
public interface IOrderService    { }
public interface IOrderRepository { }
public interface IEmailSender     { }
public interface IPaymentGateway  { }
public interface ILogger          { }
public interface IConfiguration   { }
public interface IRepository<T>   { }

public class OrderService      : IOrderService    { }
public class SqlOrderRepository: IOrderRepository { }
public class SmtpEmailSender   : IEmailSender     { }
public class StripeGateway     : IPaymentGateway  { }
public class PayPalGateway     : IPaymentGateway  { }
public class EfRepository<T>   : IRepository<T>   { }
public class CachedOrderService: IOrderService    { public CachedOrderService(IOrderService inner) { } }
