# SQL Deep Dive

> **Phase:** 3 — Database Engineering  
> **Status:** Not Started

## Overview

SQL databases are the backbone of most production systems, yet most engineers only scratch the surface of what they can do and how they actually work. This section covers the internals of SQL execution, indexing, transactions, and scalability — knowledge that turns slow queries into fast ones and unstable systems into reliable ones.

## Topics

### Query Execution Plans

**What it is:** The step-by-step plan a query optimizer chooses to execute a SQL query — describing which indexes to use, join algorithms, sort operations, and data access paths.

**Why it matters:** The execution plan is the most important diagnostic tool for slow queries — the difference between a 10ms query and a 10-second query often comes down to plan choice.

**Key concepts to understand:**
- `EXPLAIN` (PostgreSQL/MySQL) or `EXPLAIN ANALYZE` / `SET STATISTICS IO ON` / Actual Execution Plan (SQL Server)
- Operators: Table Scan, Index Seek, Index Scan, Hash Join, Nested Loop Join, Merge Join
- Estimated vs actual rows: large discrepancies indicate stale statistics
- Cost model: the optimizer uses statistics-based cost estimates to choose plans
- Plan reuse and parameterization: cached plans can be reused but can be stale after data distribution changes
- Forcing plans: hints, query store plan forcing, and when to use them

**Resources to explore:**
- [ ] (to be filled)

---

### Index Internals

**What it is:** The physical B-Tree (or other) structure used by a database to enable fast lookups, range scans, and sort operations without full table scans.

**Why it matters:** Indexes are the primary lever for query performance — but every index has a write overhead and storage cost, requiring deliberate design rather than "add an index whenever queries are slow."

**Key concepts to understand:**
- B-Tree structure: balanced tree with sorted keys in leaf nodes, pointers to data pages
- Index page splits: when a page fills, it splits — fragmentation over time
- Fill factor: how full to pack index pages at build time to reduce immediate splits
- Index fragmentation: logical (out-of-order pages) vs physical (non-contiguous pages) — both impact sequential scan performance
- `REBUILD` vs `REORGANIZE` for index maintenance
- Filtered indexes: partial indexes covering a subset of rows for selective queries

**Resources to explore:**
- [ ] (to be filled)

---

### Clustered vs Non-Clustered Indexes

**What it is:** A clustered index defines the physical storage order of table rows (one per table); non-clustered indexes are separate structures with key columns and a row locator (clustered key or RID).

**Why it matters:** The choice of clustered index key affects insert performance, range scan efficiency, and how non-clustered indexes are sized — a poor clustered index choice degrades every query.

**Key concepts to understand:**
- Clustered index: leaf level IS the data — no row lookup needed for covered queries
- Non-clustered index: leaf level contains index key + clustered key (or heap RID) — row lookup required unless covered
- Key lookup (bookmark lookup): the extra I/O when a non-clustered index doesn't cover all requested columns
- Good clustered index keys: narrow, monotonically increasing (identity/sequence), unique
- UUID/GUID as clustered key: causes random insertion order, high fragmentation
- Heap tables (no clustered index): forwarding records can be created on updates

**Resources to explore:**
- [ ] (to be filled)

---

### Covering Indexes

**What it is:** An index that includes all columns needed by a query — both filter columns and select columns — so the query can be answered entirely from the index without touching the base table.

**Why it matters:** Covering indexes eliminate the key lookup (the single most common cause of non-clustered index underperformance) and can reduce query I/O by orders of magnitude.

**Key concepts to understand:**
- `INCLUDE` clause: adding non-key columns to the index leaf level for covering
- Included columns vs key columns: included columns cannot be used for ordering or range filtering
- Index intersection: query satisfied by combining two indexes — less efficient than a single covering index
- Over-indexing risk: every covered index increases write amplification
- Identifying covering index opportunities from execution plans with key lookup operators
- Partial covering: sometimes covering just the most selective filter is enough

**Resources to explore:**
- [ ] (to be filled)

---

### Composite Indexes

**What it is:** An index on multiple columns — the key order within the index determines which queries benefit.

**Why it matters:** Column order in a composite index is the most commonly misunderstood indexing concept — an index on (A, B, C) helps queries filtering on A or A+B but not on B alone.

**Key concepts to understand:**
- Leftmost prefix rule: a composite index on (A, B, C) supports queries on (A), (A,B), (A,B,C) but not (B) or (C) alone
- Range predicates and the column ordering rule: range predicate columns should come last
- Index selectivity: put more selective columns first for equality predicates
- `WHERE A = 1 AND B > 5 ORDER BY B`: composite index (A, B) supports this optimally
- Skip scan: some databases can use non-leftmost prefix columns when the leading columns have few distinct values
- Covering composite indexes: combine filtering and covering in one index

**Resources to explore:**
- [ ] (to be filled)

---

### Statistics

**What it is:** Histograms and density vectors maintained by the database about column value distribution, used by the query optimizer to estimate row counts and choose execution plans.

**Why it matters:** Stale or missing statistics cause the optimizer to choose bad plans — a common root cause of sudden query slowdowns after data distribution changes.

**Key concepts to understand:**
- Histogram: bucketed frequency distribution of column values, sampled from table data
- Density vector: average selectivity for column combinations — used for multi-column estimates
- Auto-update statistics: triggered when enough rows change (20% threshold default, or 500 rows in SQL Server 2016+)
- `UPDATE STATISTICS` and `AUTO_UPDATE_STATISTICS_ASYNC` for controlling update behavior
- Trace Flag 2371 (SQL Server): dynamic threshold for large tables
- Statistics on filtered indexes and computed columns

**Resources to explore:**
- [ ] (to be filled)

---

### Parameter Sniffing

**What it is:** SQL Server's behavior of caching a query plan based on the first set of parameter values — causing problems when subsequent calls use very different parameter values that need a different plan.

**Why it matters:** Parameter sniffing is one of the most common causes of "this query was fast yesterday and slow today" in SQL Server — identifying and resolving it is a core DBA/developer skill.

**Key concepts to understand:**
- Plan cache: compiled query plans are cached and reused by parameter hash
- Sniffing problem: plan optimized for `WHERE UserID = 1` (returns 1 row) performs poorly for `WHERE UserID = 2` (returns 1M rows)
- `OPTIMIZE FOR UNKNOWN`: hint to use average statistics instead of sniffed parameters
- `OPTION (RECOMPILE)`: recompile the query each time — eliminates sniffing at the cost of compile time
- Query Store: identify queries with plan regressions and force stable plans
- Adaptive query processing (SQL Server 2017+): batch mode adaptive joins, memory grant feedback

**Resources to explore:**
- [ ] (to be filled)

---

### Locking

**What it is:** The mechanism databases use to prevent concurrent transactions from interfering with each other, using shared locks (reads), exclusive locks (writes), and intent locks.

**Why it matters:** Understanding locking reveals why queries block each other, how to reduce contention, and why certain query patterns cause disproportionate lock escalation.

**Key concepts to understand:**
- Lock granularity: row, page, table, extent
- Shared (S) vs exclusive (X) vs update (U) vs intent locks: compatibility matrix
- Lock escalation: row locks promoted to table lock when count exceeds threshold — often causes blocking
- Lock hints: `NOLOCK` (dirty reads), `UPDLOCK`, `HOLDLOCK`, `ROWLOCK` — use sparingly and deliberately
- Optimistic vs pessimistic locking at the application level
- `sys.dm_exec_requests` and `sys.dm_os_waiting_tasks` for live lock monitoring

**Resources to explore:**
- [ ] (to be filled)

---

### Deadlocks

**What it is:** A cycle of two or more transactions each waiting for the other to release a lock — the database resolves by killing one transaction (the deadlock victim).

**Why it matters:** Deadlocks cause transaction rollbacks and error propagation to clients — systematic elimination requires understanding access patterns and consistent lock ordering.

**Key concepts to understand:**
- Deadlock cycle: T1 holds A, wants B; T2 holds B, wants A
- Deadlock victim selection: typically the transaction with least "undo" cost
- SQL Server deadlock graph: XML capture in system health trace or Extended Events
- Prevention strategies: consistent lock order, shorter transactions, reduced lock scope
- Row-level vs table-level locking strategies
- Index design to reduce deadlocks: covering indexes reduce lookups, reducing lock chains

**Resources to explore:**
- [ ] (to be filled)

---

### Isolation Levels

**What it is:** Transaction isolation levels define how visible concurrent transaction changes are to each other, trading between data consistency anomalies and concurrency.

**Why it matters:** Choosing the wrong isolation level either causes data consistency bugs (dirty reads, phantom reads) or unnecessary lock contention that tanks throughput.

**Key concepts to understand:**
- READ UNCOMMITTED: dirty reads — fastest, never use for financial data
- READ COMMITTED: no dirty reads, but non-repeatable reads possible (SQL Server default)
- REPEATABLE READ: no dirty or non-repeatable reads, but phantom reads possible
- SERIALIZABLE: full isolation, no anomalies — highest lock contention
- READ COMMITTED SNAPSHOT (RCSI): optimistic row-versioning reads — no readers block writers
- SNAPSHOT isolation: consistent read snapshot from transaction start — good for reporting queries

**Resources to explore:**
- [ ] (to be filled)

---

### Transactions

**What it is:** A unit of work that is atomic (all-or-nothing), consistent (leaves the database in a valid state), isolated (concurrent transactions don't see each other's partial work), and durable (committed changes survive failures) — ACID.

**Why it matters:** Correct transaction design is the foundation of data integrity — incorrect boundaries lead to partial failures, phantom data, and consistency violations.

**Key concepts to understand:**
- Explicit vs implicit transactions: `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK`
- Transaction log: write-ahead log (WAL) ensures durability — every modification logged before data pages
- Long-running transactions: hold locks, prevent log truncation, block checkpoints
- Distributed transactions (MSDTC, XA): 2-phase commit across multiple databases — avoid if possible
- Savepoints: partial rollback within a transaction
- Nested transactions in SQL Server: only the outermost `COMMIT` actually commits

**Resources to explore:**
- [ ] (to be filled)

---

### MVCC

**What it is:** Multi-Version Concurrency Control maintains multiple versions of rows so readers can access a consistent snapshot without blocking writers — the foundation of modern database concurrency.

**Why it matters:** MVCC is why PostgreSQL can have readers and writers not block each other — understanding it explains tempdb pressure in SQL Server and vacuuming in PostgreSQL.

**Key concepts to understand:**
- Row versioning: old versions retained in a version store (tempdb in SQL Server, table storage in PostgreSQL)
- Read snapshot: readers see a consistent view of data from a specific transaction ID
- Version store growth: long-running transactions accumulate versions — can cause tempdb growth
- Vacuum in PostgreSQL: reclaims space from dead tuples — autovacuum tuning is critical
- Transaction ID wraparound in PostgreSQL: a critical operational concern for high-write databases
- MVCC vs pessimistic locking: MVCC favors read-heavy; 2PL favors write-heavy with frequent conflicts

**Resources to explore:**
- [ ] (to be filled)

---

### Partitioning

**What it is:** Dividing a large table into smaller, more manageable pieces (partitions) based on a partition key — enabling partition elimination, faster maintenance, and tiered storage.

**Why it matters:** Partitioning is a key scalability technique for time-series and event data — deleting old partitions (switch out) is dramatically faster than row-by-row DELETE.

**Key concepts to understand:**
- Partition function: defines range boundaries for the partition key
- Partition scheme: maps partitions to filegroups
- Partition elimination: queries with partition key predicates skip irrelevant partitions
- Sliding window pattern: switch oldest partition to archive table, add new empty partition
- Aligned indexes: non-clustered indexes must use the same partition scheme
- Partition-wise joins: some databases can parallelize joins across partitions

**Resources to explore:**
- [ ] (to be filled)

---

### Replication

**What it is:** The process of copying and synchronizing data from a primary database to one or more replicas, enabling read scaling, high availability, and disaster recovery.

**Why it matters:** Replication is foundational for high-availability SQL deployments — understanding lag, consistency, and failover behavior prevents surprises in production.

**Key concepts to understand:**
- Synchronous vs asynchronous replication: zero data loss vs lower latency for primary
- Always On Availability Groups (SQL Server): synchronous primary + async secondaries
- Logical replication (PostgreSQL): per-table, per-operation replication for selective sync
- Replication lag: how far behind the replica is — critical for read replica consistency decisions
- Read replicas and stale reads: application must tolerate slightly stale data
- Failover and promotion: automatic failover in HA groups, manual failover for maintenance

**Resources to explore:**
- [ ] (to be filled)

---

### Sharding

**What it is:** Horizontal partitioning of data across multiple independent database instances (shards) based on a shard key — the primary technique for scaling beyond what a single database can handle.

**Why it matters:** Sharding is the ultimate scale-out for databases, but it comes with significant complexity — cross-shard queries, transactions, and rebalancing are all hard problems.

**Key concepts to understand:**
- Shard key selection: must distribute load evenly and avoid cross-shard queries for common access patterns
- Range-based vs hash-based sharding: range allows range scans, hash distributes load more evenly
- Hot spots: a single shard receiving disproportionate traffic due to poor shard key selection
- Cross-shard queries: require scatter-gather — expensive and should be minimized
- Resharding: adding shards and migrating data without downtime is the hardest operational challenge
- Application-level sharding vs middleware sharding (Vitess, Citus)

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Entity Framework Core](./02-entity-framework-core.md)
- [NoSQL & Caching](./03-nosql-and-caching.md)
- [Database Performance](../Phase-08-Performance-Engineering/02-database-performance.md)
- [Advanced Systems](../Phase-15-Advanced-Topics/01-advanced-systems.md)
