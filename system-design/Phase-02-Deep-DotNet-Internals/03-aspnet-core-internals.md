# ASP.NET Core Internals

> **Phase:** 2 — Deep .NET Internals  
> **Status:** Not Started

## Overview

ASP.NET Core is the platform for building modern .NET web services, but most engineers only use its surface API. This section digs into the internals — how the middleware pipeline works, how DI is resolved, how Kestrel handles connections, and how to leverage advanced features like Minimal APIs and Native AOT for maximum performance and correctness.

## Topics

### Middleware Pipeline

**What it is:** A chain of request-handling components where each middleware can process the request, call the next middleware, and process the response — ASP.NET Core's primary extensibility mechanism.

**Why it matters:** The middleware pipeline is where cross-cutting concerns (auth, logging, rate limiting, exception handling) live; understanding its execution order and short-circuit behavior prevents security and correctness bugs.

**Key concepts to understand:**
- `IApplicationBuilder.Use` / `Run` / `Map` / `MapWhen` pipeline construction
- Request pipeline as a linked list of `RequestDelegate` (Func<HttpContext, Task>)
- Middleware order matters: authentication before authorization, exception handling at the outermost layer
- Short-circuiting: middleware that doesn't call `_next()` stops pipeline execution
- Branching: `Map` creates a sub-pipeline for matching paths
- Convention-based vs interface-based middleware: `IMiddleware` with DI vs `Invoke(HttpContext, IDep dep)` convention

**Resources to explore:**
- [ ] (to be filled)

---

### Minimal APIs

**What it is:** A lightweight model (introduced in .NET 6) for defining HTTP endpoints directly on the `WebApplication` without controllers, reducting ceremony for small services and serverless functions.

**Why it matters:** Minimal APIs have lower overhead per request than MVC controllers and compile to simpler IL — they're the preferred model for new .NET APIs and Native AOT deployments.

**Key concepts to understand:**
- `app.MapGet/Post/Put/Delete/Patch` with route pattern and handler delegate
- Route parameter binding: `{id:int}`, `{name:minlength(3)}`
- Parameter binding sources: route, query string, body (JSON), headers, services (DI)
- Return type conventions: `IResult`, `TypedResults`, direct object returns
- Route groups: `app.MapGroup("/api/v1")` for common prefixes and filters
- Filters in Minimal APIs: `IEndpointFilter` for cross-cutting concerns without middleware

**Resources to explore:**
- [ ] (to be filled)

---

### Endpoint Routing

**What it is:** The routing infrastructure introduced in ASP.NET Core 3.0 that separates route matching from dispatch, enabling middleware components to know which endpoint will handle a request.

**Why it matters:** Endpoint routing enables authentication, authorization, and rate limiting middleware to make decisions based on route metadata before the handler executes.

**Key concepts to understand:**
- `UseRouting()` (matching) and `UseEndpoints()` (dispatching) and why order matters
- Route templates: literals, parameters, optional segments, catch-all
- Constraints: built-in (`int`, `guid`, `minlength`) and custom `IRouteConstraint`
- Endpoint metadata: `[Authorize]`, `[AllowAnonymous]`, custom metadata for middleware inspection
- Link generation: `LinkGenerator` for building URLs to named routes/actions
- Route precedence: specificity ordering to resolve ambiguous routes

**Resources to explore:**
- [ ] (to be filled)

---

### Model Binding

**What it is:** The process by which ASP.NET Core maps HTTP request data (route values, query strings, form data, JSON body) to action/handler parameters and model objects.

**Why it matters:** Model binding is automatic but opaque to many developers — understanding it explains unexpected null values, type conversion failures, and how to extend binding for custom types.

**Key concepts to understand:**
- Binding sources: `[FromRoute]`, `[FromQuery]`, `[FromBody]`, `[FromHeader]`, `[FromForm]`, `[FromServices]`
- `[ApiController]` attribute: infers binding sources automatically, enables automatic model validation
- Custom model binders: `IModelBinder` and `IModelBinderProvider` for custom types
- Input formatters: how JSON/XML body deserialization plugs into model binding
- `TryValidateModel` and `ModelState.IsValid` for programmatic validation
- Minimal API binding: source inferred by type — `HttpContext`, `CancellationToken`, DI services are auto-bound

**Resources to explore:**
- [ ] (to be filled)

---

### Filters

**What it is:** Attributes or interfaces that run code at specific points in the MVC/API request pipeline — before/after action execution, around exception handling, or wrapping result execution.

**Why it matters:** Filters provide a structured way to implement cross-cutting concerns (logging, caching, authorization, audit) that are tightly coupled to action execution semantics.

**Key concepts to understand:**
- Filter types: Authorization, Resource, Action, Exception, Result — each with distinct pipeline position
- Synchronous vs asynchronous filter interfaces
- Filter pipeline execution order and short-circuiting via `context.Result`
- Global filters via `services.AddControllers(options => options.Filters.Add(...))`
- `IFilterFactory` for DI-enabled filters that need services
- Filters vs Middleware: filters run inside the MVC pipeline (have access to action context), middleware runs around it

**Resources to explore:**
- [ ] (to be filled)

---

### Dependency Injection Internals

**What it is:** ASP.NET Core's built-in IoC container (`ServiceCollection` / `IServiceProvider`) that manages object creation, lifetime management, and dependency resolution.

**Why it matters:** DI is the composition root of every .NET service — understanding its internals reveals why certain lifetime combinations cause bugs and how to optimize resolution for high-frequency paths.

**Key concepts to understand:**
- Service lifetimes: Singleton (one per container), Scoped (one per scope/request), Transient (new per resolution)
- Captive dependency bug: Singleton depending on a Scoped service — the scoped service never gets replaced
- `IServiceScope` and scope creation: how request scopes are created and disposed in ASP.NET Core
- Root vs scope service provider: root provider cannot resolve scoped services
- `ActivatorUtilities.CreateInstance` for creating DI-unregistered instances with injected dependencies
- Service provider validation: `ValidateScopes` and `ValidateOnBuild` at startup
- Keyed services in .NET 8: `[FromKeyedServices("name")]` for multiple implementations of same interface

**Resources to explore:**
- [ ] (to be filled)

---

### Configuration Providers

**What it is:** The pluggable configuration system in .NET that aggregates settings from multiple sources (JSON files, environment variables, command-line args, Azure Key Vault) into a unified `IConfiguration`.

**Why it matters:** Production applications need layered configuration (defaults in JSON, overrides in environment variables, secrets in Key Vault) — understanding the provider model lets you build correct configuration hierarchies.

**Key concepts to understand:**
- Provider precedence: last-registered wins — environment variables override `appsettings.json`
- `appsettings.json` → `appsettings.{Environment}.json` → environment variables → command-line
- Hierarchical key format: `Section:SubSection:Key` in JSON maps to `SECTION__SUBSECTION__KEY` in env vars
- Custom providers: `IConfigurationProvider` / `IConfigurationSource` for custom sources
- Reloadable configuration: `IOptionsMonitor<T>` for runtime config changes
- Secrets in development: `dotnet user-secrets` and `IUserSecretsConfigurationSource`

**Resources to explore:**
- [ ] (to be filled)

---

### Options Pattern

**What it is:** A convention for binding configuration sections to strongly-typed POCO classes using `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`.

**Why it matters:** The options pattern replaces stringly-typed configuration access with type-safe, validated, dependency-injectable settings objects — reducing configuration bugs in complex services.

**Key concepts to understand:**
- `IOptions<T>`: singleton, doesn't reflect config changes after startup
- `IOptionsSnapshot<T>`: scoped, re-evaluated per request — reflects config changes
- `IOptionsMonitor<T>`: singleton with change notification via `OnChange` callback
- `services.AddOptions<T>().Bind(config.GetSection("Section")).ValidateDataAnnotations()`
- `IValidateOptions<T>` for complex validation logic beyond data annotations
- Named options: `IOptionsMonitor<T>.Get("name")` for multiple configurations of the same type

**Resources to explore:**
- [ ] (to be filled)

---

### Kestrel Internals

**What it is:** The cross-platform, event-loop-based HTTP server built into ASP.NET Core, implemented on top of `System.IO.Pipelines` and `System.Net.Sockets`.

**Why it matters:** Kestrel's performance characteristics (connections/sec, throughput, latency) are directly affected by its configuration and the transport layer — understanding it enables tuning for high-load scenarios.

**Key concepts to understand:**
- Transport layer: `SocketTransport` (default), `TlsTransport`, and custom `IConnectionListenerFactory`
- Connection context: per-connection `ConnectionContext` with Input/Output `PipeReader/Writer`
- HTTP/1.1 pipeline: request parsing, response writing, connection keep-alive
- HTTP/2: multiplexed streams, HPACK header compression, flow control
- Limits: `MaxConcurrentConnections`, `MaxRequestBodySize`, `KeepAliveTimeout`
- Thread model: I/O on I/O threads, application code on thread pool — minimize blocking on I/O threads

**Resources to explore:**
- [ ] (to be filled)

---

### HTTP Pipeline

**What it is:** The sequence of processing from raw bytes arriving at Kestrel through transport → protocol → middleware → endpoint and back through response writing.

**Why it matters:** Understanding the full HTTP pipeline from socket to handler reveals where latency is introduced, where allocations happen, and where to place optimizations.

**Key concepts to understand:**
- Request parsing: zero-copy parsing using `ReadOnlySequence<byte>` from the pipe
- `HttpContext` creation and pooling to reduce per-request allocations
- Response buffering: synchronous vs asynchronous response flushing
- Connection multiplexing in HTTP/2: stream IDs, concurrent streams, prioritization
- Chunked transfer encoding and its interaction with response buffering
- Trailers in HTTP/2 for post-response metadata

**Resources to explore:**
- [ ] (to be filled)

---

### gRPC in .NET

**What it is:** A high-performance RPC framework using Protobuf serialization and HTTP/2 transport, supported in .NET via `Grpc.AspNetCore` (server) and `Grpc.Net.Client` (client).

**Why it matters:** gRPC is the preferred inter-service communication protocol for performance-sensitive microservice communication, offering streaming, strong typing via Protobuf, and 5-10x better throughput than JSON/REST.

**Key concepts to understand:**
- Protobuf `.proto` files and code generation: `Grpc.Tools` and the gRPC source generator
- Unary, server-streaming, client-streaming, and bidirectional-streaming RPC methods
- Interceptors: server-side and client-side for cross-cutting concerns (auth, logging, retry)
- `gRPC-Web`: browser support via HTTP/1.1 transcoding (limited to unary and server streaming)
- Deadlines and cancellation: propagated via HTTP/2 headers and CancellationToken
- `ChannelCredentials` and mTLS for secure gRPC channels

**Resources to explore:**
- [ ] (to be filled)

---

### SignalR Internals

**What it is:** A library for adding real-time, bidirectional communication (server push) to ASP.NET Core applications using WebSockets with fallback to Server-Sent Events or Long Polling.

**Why it matters:** SignalR is the standard solution for real-time features (notifications, live dashboards, chat) in .NET — understanding its transport negotiation and hub model prevents common scaling mistakes.

**Key concepts to understand:**
- Transport negotiation: WebSocket → SSE → Long Polling fallback
- Hub: server-side class representing a connection context; `Clients.Caller`, `Clients.Group`, `Clients.All`
- Connection model: each SignalR connection has a `ConnectionId` and a negotiated transport
- Scale-out with Redis backplane: `AddStackExchangeRedis()` for multi-server deployments
- MessagePack protocol: binary serialization alternative to JSON for reduced bandwidth
- Strongly-typed hubs: `Hub<IClientInterface>` for compile-time client method type safety

**Resources to explore:**
- [ ] (to be filled)

---

### BackgroundService / Hosted Services

**What it is:** `IHostedService` and its base class `BackgroundService` provide a lifecycle-managed, DI-integrated way to run background work alongside an ASP.NET Core application.

**Why it matters:** Background services are the correct pattern for polling, scheduled jobs, queue consumers, and other long-running background tasks in .NET — incorrect implementation leads to unhandled exceptions that silently kill the host.

**Key concepts to understand:**
- `StartAsync(CancellationToken)` / `StopAsync(CancellationToken)`: host lifecycle integration
- `BackgroundService.ExecuteAsync(CancellationToken stoppingToken)`: the main loop
- Unhandled exceptions in `ExecuteAsync`: crash the host in .NET 6+ (`BackgroundServiceExceptionBehavior.StopHost`)
- `IHostApplicationLifetime` for triggering graceful shutdown from background services
- Scoped services in singleton BackgroundService: create `IServiceScope` for each unit of work
- Worker Service template: standalone background service host without HTTP pipeline

**Resources to explore:**
- [ ] (to be filled)

---

### Native AOT APIs

**What it is:** ASP.NET Core endpoints and Minimal APIs designed to be compatible with Native AOT compilation — avoiding reflection, dynamic code generation, and runtime type discovery.

**Why it matters:** Native AOT ASP.NET Core apps start in milliseconds and use significantly less memory — transformative for serverless, CLI, and sidecar workloads where cold start dominates.

**Key concepts to understand:**
- `RequestDelegateGenerator`: source generator that generates Minimal API glue code at compile time
- `JsonSerializerContext` source generation: replaces reflection-based System.Text.Json with compile-time serializer
- AOT-incompatible patterns: `Expression.Compile`, `Type.GetType(string)`, unattributed reflection
- `[JsonSerializable(typeof(MyDto))]` on a `JsonSerializerContext` subclass
- Trimming annotations: `[DynamicDependency]`, `[UnconditionalSuppressMessage]` for marking AOT-safe reflection
- Testing AOT compatibility: `PublishAot=true` in project file, verify with `dotnet publish`

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [CLR Runtime Internals](./01-clr-runtime-internals.md)
- [Async & Concurrency](./02-async-concurrency.md)
- [Networking Fundamentals](../Phase-05-Networking-And-Web/01-networking-fundamentals.md)
- [Application Performance](../Phase-08-Performance-Engineering/01-application-performance.md)
