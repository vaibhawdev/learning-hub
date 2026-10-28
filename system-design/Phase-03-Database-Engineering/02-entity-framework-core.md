# Entity Framework Core

> **Phase:** 3 — Database Engineering  
> **Status:** Not Started

## Overview

Entity Framework Core is the standard ORM for .NET applications, but its abstractions can hide severe performance problems. This section covers how EF Core actually works — change tracking, query translation, and the concurrency model — enabling you to use it effectively and know when to bypass it.

## Topics

### Change Tracking Internals

**What it is:** The mechanism by which EF Core detects modifications to entity instances and generates the appropriate INSERT/UPDATE/DELETE SQL when `SaveChanges()` is called.

**Why it matters:** Change tracking has CPU and memory overhead — understanding it prevents slow bulk operations and explains why loading 10,000 entities into a tracked context is slow.

**Key concepts to understand:**
- The `ChangeTracker`: stores snapshots of original property values for comparison on SaveChanges
- Snapshot change tracking vs delegate change tracking (INotifyPropertyChanged)
- `DetectChanges()`: called automatically before `SaveChanges()` — O(n) in number of tracked entities
- `DbContext.Entry(entity).State`: explicit state management for performance
- Entity states: Detached, Unchanged, Added, Modified, Deleted
- Attaching entities: `context.Attach(entity)` for disconnected scenarios

**Resources to explore:**
- [ ] (to be filled)

---

### Tracking vs NoTracking

**What it is:** Tracked queries (default) add returned entities to the ChangeTracker for automatic change detection; NoTracking queries return entities without ChangeTracker overhead for read-only scenarios.

**Why it matters:** NoTracking queries can be 2-3x faster for read-only operations because they skip snapshot storage and entity identity resolution — a common missed optimization.

**Key concepts to understand:**
- `AsNoTracking()`: returns entities not managed by ChangeTracker — cannot be used for updates
- `AsNoTrackingWithIdentityResolution()`: NoTracking but with identity resolution (same entity instance for same PK)
- `context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking`: global setting
- When to use tracking: when you will modify and save the entity
- When to use NoTracking: read-only queries, projections, high-throughput read paths
- Projections with `Select()`: best for read-only — avoids loading unused columns

**Resources to explore:**
- [ ] (to be filled)

---

### Compiled Queries

**What it is:** Pre-compiled LINQ query delegates (`EF.CompileQuery`) that translate LINQ to SQL once and cache the result, avoiding repeated translation overhead on each call.

**Why it matters:** LINQ-to-SQL translation in EF Core takes 20-100µs per query — on a hot path handling 10,000 req/s, this becomes a significant CPU consumer.

**Key concepts to understand:**
- `EF.CompileQuery((DbContext ctx, int id) => ctx.Orders.Where(o => o.Id == id))`
- Compiled queries must be stored as static fields or in a query cache
- Limitations: cannot use `Include()` on a compiled query (use separate compiled query)
- `EF.CompileAsyncQuery` for async variants
- Query cache vs compiled queries: EF Core caches query translations internally, but compiled queries bypass this overhead
- When to use: hot paths queried thousands of times per second

**Resources to explore:**
- [ ] (to be filled)

---

### N+1 Problems

**What it is:** A query performance anti-pattern where loading a list of N entities and then accessing a navigation property on each triggers N additional queries instead of one JOIN.

**Why it matters:** N+1 is the most common and impactful EF Core performance problem — a page load that executes 500 queries instead of 2 is a structural bug, not a tuning problem.

**Key concepts to understand:**
- Lazy loading: navigation property access triggers a query — the source of N+1 when in a loop
- Eager loading with `Include()`: one JOIN query loads related entities
- Explicit loading: `context.Entry(entity).Collection(e => e.Orders).Load()` — intentional, not accidental
- Detecting N+1: logging EF Core SQL output, MiniProfiler, Glimpse
- `Include()` on large collections: can load too much data — consider split queries or projections
- `Select()` projections: the best way to avoid N+1 for read scenarios

**Resources to explore:**
- [ ] (to be filled)

---

### Split Queries

**What it is:** An EF Core 5+ feature that splits a single multi-`Include()` query into multiple simpler queries executed in sequence, avoiding the Cartesian explosion problem.

**Why it matters:** A query with multiple collection `Include()`s can return exponentially more rows than necessary — split queries trade multiple round trips for sane result set sizes.

**Key concepts to understand:**
- Cartesian explosion: `Orders` with 3 `Include()`s of 10 items each returns 10³ = 1000 rows instead of 30
- `AsSplitQuery()`: executes separate queries per Include and hydrates the entity graph in memory
- Global split query mode: `optionsBuilder.UseSqlServer(..., o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))`
- Split query isolation risk: separate queries don't run in the same snapshot — potential for inconsistency
- When single query is better: few Includes with small related collections
- Measuring: compare actual query plans and row counts between single and split

**Resources to explore:**
- [ ] (to be filled)

---

### Connection Pooling

**What it is:** The database connection pool (managed by ADO.NET/`SqlClient`) that reuses existing connections instead of opening new TCP connections and authenticating for each query.

**Why it matters:** Opening a SQL Server connection takes ~5ms — without pooling, high-throughput APIs would spend more time connecting than querying.

**Key concepts to understand:**
- ADO.NET connection pool: per-connection-string pool, default max 100 connections
- `Min Pool Size` and `Max Pool Size` in connection string
- Pool exhaustion: all connections in use — new requests wait or timeout
- `DbContext` lifetime and connection pooling: DbContext opens/closes connections per operation (not per context lifetime)
- `DbContextPool` (`AddDbContextPool`): pool DbContext instances to reduce EF initialization overhead
- Connection resiliency: `EnableRetryOnFailure` for transient SQL failures

**Resources to explore:**
- [ ] (to be filled)

---

### DbContext Lifetime

**What it is:** The scope during which a `DbContext` instance lives, tracks entities, and manages the database connection — typically scoped to an HTTP request in ASP.NET Core.

**Why it matters:** DbContext is not thread-safe — a Singleton or incorrectly scoped DbContext causes concurrency exceptions and memory leaks from unbounded change tracking growth.

**Key concepts to understand:**
- Transient: new context per injection — loses change tracking benefits across operations
- Scoped (recommended for web): one context per HTTP request — standard pattern
- Singleton: never — not thread-safe, accumulates tracked entities indefinitely
- `IDbContextFactory<T>`: for creating DbContext instances in background services or non-DI contexts
- Context lifecycle: constructed → queries/changes → SaveChanges → disposed
- DbContext pooling: `AddDbContextPool` reuses context instances after reset — improves throughput

**Resources to explore:**
- [ ] (to be filled)

---

### Bulk Operations

**What it is:** Efficiently inserting, updating, or deleting large numbers of rows — EF Core's default one-row-at-a-time approach is too slow for bulk data operations.

**Why it matters:** Inserting 100,000 rows with standard EF Core `AddRange` + `SaveChanges` takes seconds; bulk insert via `SqlBulkCopy` or EF extensions takes milliseconds.

**Key concepts to understand:**
- `EF.BulkInsert` / `EF.BulkUpdate` via community libraries (EFCore.BulkExtensions, Z.EntityFramework.Extensions)
- `SqlBulkCopy`: ADO.NET bulk insert — fastest but bypasses EF change tracking and validation
- EF Core 7+ `ExecuteUpdate` / `ExecuteDelete`: set-based updates/deletes without loading entities
- Batching: EF Core batches multiple changes into fewer round trips (configurable via `MinBatchSize`, `MaxBatchSize`)
- Table-valued parameters (TVP): passing sets of rows to stored procedures efficiently
- Change tracking overhead for bulk operations: use `AddRange` with `DetectChanges` disabled

**Resources to explore:**
- [ ] (to be filled)

---

### Transactions

**What it is:** EF Core's support for database transactions, either automatic (each `SaveChanges` is a transaction) or explicit (wrapping multiple operations in a single transaction).

**Why it matters:** Incorrect transaction boundaries lead to partial failures — understanding explicit transactions enables atomic multi-step operations across multiple `SaveChanges` calls.

**Key concepts to understand:**
- Implicit transaction: `SaveChanges()` wraps all pending changes in a single transaction by default
- Explicit transaction: `context.Database.BeginTransaction()` / `CommitAsync()` / `RollbackAsync()`
- `await using var transaction = await context.Database.BeginTransactionAsync()`
- Sharing transactions across multiple contexts: `context2.Database.UseTransaction(transaction.GetDbTransaction())`
- Savepoints in EF Core: `context.Database.CreateSavepointAsync("beforeUpdate")`
- Transaction + retry logic: `ExecutionStrategy` wraps entire transaction for transient fault handling

**Resources to explore:**
- [ ] (to be filled)

---

### EF Performance Tuning

**What it is:** A set of techniques for measuring and improving EF Core query and change tracking performance in production applications.

**Why it matters:** EF Core has significant tuning surface — correctly configured, it can handle high-throughput production loads; poorly configured, it becomes a bottleneck.

**Key concepts to understand:**
- Logging queries: `optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information)` or Serilog integration
- `EnableSensitiveDataLogging()`: include parameter values in logs (never in production)
- Query tags: `context.Orders.TagWith("GetRecentOrders").ToList()` for SQL comment correlation
- `Database.ExecuteSqlRaw` / `FromSqlRaw`: escape hatch for raw SQL when LINQ translation is suboptimal
- Benchmark before and after: use BenchmarkDotNet with real database queries
- EF Core migrations: keep migration history clean, avoid auto-migrations in production

**Resources to explore:**
- [ ] (to be filled)

---

### Interceptors

**What it is:** Hook points in EF Core that allow code to run before, after, or instead of database operations — for logging, auditing, soft delete enforcement, and query modification.

**Why it matters:** Interceptors are the correct way to add cross-cutting database concerns in EF Core without polluting repository or service code.

**Key concepts to understand:**
- `IDbCommandInterceptor`: intercept and modify SQL commands before execution
- `ISaveChangesInterceptor`: run code before/after SaveChanges — ideal for auditing (CreatedAt, UpdatedAt)
- `IDbConnectionInterceptor`: intercept connection open/close — for monitoring, retry logic
- Registering interceptors: `optionsBuilder.AddInterceptors(new AuditInterceptor())`
- Soft delete interceptor: modify queries to add `WHERE IsDeleted = 0` automatically
- Query filter alternative: `HasQueryFilter()` as a simpler alternative for per-entity global filters

**Resources to explore:**
- [ ] (to be filled)

---

### Value Converters

**What it is:** EF Core feature for converting a CLR type to a database type and back — enabling storage of custom types, enums as strings, encrypted values, and JSON columns.

**Why it matters:** Value converters eliminate the boilerplate of manual mapping between domain types and database-friendly representations, keeping the domain model clean.

**Key concepts to understand:**
- `HasConversion<TModel, TProvider>()`: register a converter for a property
- Built-in converters: `EnumToStringConverter`, `BoolToZeroOneConverter`, `DateTimeToTicksConverter`
- Custom converter: implement `ValueConverter<TModel, TProvider>` with convert/revert expressions
- `OwnsOne` with JSON column (EF Core 7+): store an owned entity as a JSON column
- Limitations: EF cannot translate converter expressions to SQL for WHERE clauses in all cases
- `PropertyBuilder.HasConversion(new JsonValueConverter<MyType>())` for arbitrary JSON storage

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [SQL Deep Dive](./01-sql-deep-dive.md)
- [NoSQL & Caching](./03-nosql-and-caching.md)
- [Database Performance](../Phase-08-Performance-Engineering/02-database-performance.md)
