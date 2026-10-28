# NoSQL and Caching

> **Phase:** 3 — Database Engineering  
> **Status:** Not Started

## Overview

NoSQL databases and distributed caches are essential components of modern high-scale systems. This section covers the internals and operational characteristics of the most important non-relational stores — Redis, Cosmos DB, MongoDB, and DynamoDB — along with the caching patterns and consistency tradeoffs that govern their use.

## Topics

### Redis Internals

**What it is:** An in-memory data structure server supporting strings, hashes, lists, sets, sorted sets, streams, and more — used as a cache, message broker, session store, and leaderboard engine.

**Why it matters:** Redis is the most widely deployed caching layer in distributed systems — understanding its data structures, persistence, and threading model prevents misuse and operational surprises.

**Key concepts to understand:**
- Single-threaded event loop: Redis processes one command at a time — slow commands block everything
- Data types: String, Hash, List, Set, Sorted Set, HyperLogLog, Streams, Geo
- Persistence: RDB snapshots (point-in-time), AOF (append-only log), hybrid persistence
- Eviction policies: noeviction, allkeys-lru, allkeys-lfu, volatile-lru, allkeys-random
- Redis Cluster: hash slots (16384), data sharding, gossip protocol, master-replica per shard
- Pipelining and MULTI/EXEC: batching commands to reduce round trips

**Resources to explore:**
- [ ] (to be filled)

---

### Distributed Caching Patterns

**What it is:** Strategies for using a shared cache (Redis, Memcached) across multiple application instances to reduce database load and improve response times.

**Why it matters:** Distributed caching is one of the highest-leverage scalability techniques — a well-designed cache can handle 10x the load with the same infrastructure.

**Key concepts to understand:**
- Cache-aside (lazy loading): application checks cache first, fetches from DB on miss, populates cache
- Write-through: cache updated synchronously on every write — strong consistency, slower writes
- Write-behind (write-back): writes go to cache first, async flush to DB — higher risk of data loss
- Read-through: cache transparently fetches from DB on miss — simpler application code
- Cache stampede (thundering herd): many requests simultaneously miss the same key
- Cache-aside with probabilistic early expiration: pre-emptive refresh before expiry to avoid stampede

**Resources to explore:**
- [ ] (to be filled)

---

### Cache Invalidation

**What it is:** The process of removing or updating cached entries when the underlying data changes — famously described as one of the two hard problems in computer science.

**Why it matters:** Stale cache entries serve incorrect data; over-aggressive invalidation reduces cache hit rates — the right strategy depends on consistency requirements.

**Key concepts to understand:**
- TTL-based expiration: simplest approach, accepts bounded staleness
- Event-driven invalidation: publish a change event → subscribers invalidate related keys
- Tag-based invalidation: group related keys with a tag, invalidate all by tag
- Cache versioning: embed a version in the cache key — changing version effectively invalidates
- Consistent invalidation challenge: invalidating across multiple cache nodes without races
- Coalescing: ensure only one request rebuilds a cache entry after invalidation (using locks or Lua scripts)

**Resources to explore:**
- [ ] (to be filled)

---

### CQRS Read Models

**What it is:** In CQRS (Command Query Responsibility Segregation), the read model is a denormalized, query-optimized view of data maintained separately from the write model, often in a cache or read-optimized store.

**Why it matters:** CQRS read models enable extremely fast reads by pre-computing views that exactly match query needs — eliminating complex JOINs and aggregations at query time.

**Key concepts to understand:**
- Materialized views: pre-computed query results stored as tables or cache entries
- Event-driven read model updates: domain events trigger read model projections
- Read model lag: eventual consistency between write model and read model
- Redis as a read model store: hash maps for entity views, sorted sets for ranked lists
- Elasticsearch as a read model: full-text search, faceting, aggregation read models
- Handling read model rebuilds: replaying events to reconstruct read models

**Resources to explore:**
- [ ] (to be filled)

---

### Eventual Consistency

**What it is:** A consistency model where distributed system replicas are guaranteed to converge to the same value eventually, but reads may return stale data in the interim.

**Why it matters:** Most distributed systems (Redis Cluster, Cosmos DB, DynamoDB) use eventual consistency by default — designing applications around it prevents subtle data bugs.

**Key concepts to understand:**
- BASE: Basically Available, Soft state, Eventual consistency — the alternative to ACID
- Convergence: all replicas will agree on the same value given no new writes
- Conflict resolution: last-write-wins (LWW), vector clocks, CRDTs for merge semantics
- Read-your-writes consistency: a client should see its own writes immediately
- Session consistency: within a session, reads are consistent with that session's writes
- Strong eventual consistency (CRDTs): mathematically guaranteed convergence without coordination

**Resources to explore:**
- [ ] (to be filled)

---

### Cosmos DB Partitioning

**What it is:** Cosmos DB's horizontal scaling mechanism where data is automatically distributed across physical partitions based on a partition key, with each logical partition mapped to a physical partition.

**Why it matters:** Partition key selection in Cosmos DB is the single most important design decision — a bad key creates hot partitions that limit throughput and cause throttling.

**Key concepts to understand:**
- Logical partition: all items with the same partition key value
- Physical partition: a managed unit of compute and storage, hosts multiple logical partitions
- Request Units (RU/s): Cosmos DB's abstraction of compute cost per operation
- Cross-partition queries: fan out to all partitions, expensive in RU cost and latency
- Hot partition: one logical partition receiving disproportionate traffic — limits max throughput
- Hierarchical partition keys (Cosmos DB 2023+): composite partition keys for fine-grained partitioning

**Resources to explore:**
- [ ] (to be filled)

---

### MongoDB Internals

**What it is:** A document-oriented database storing BSON (binary JSON) documents in collections, supporting rich queries, aggregation pipelines, and horizontal sharding.

**Why it matters:** MongoDB is widely used for its flexible schema and developer-friendly query model — understanding its indexing, aggregation, and replication internals enables correct and performant usage.

**Key concepts to understand:**
- WiredTiger storage engine: MVCC, B-Tree indexes, document-level locking, compression
- BSON: binary JSON with additional types (ObjectId, Date, Decimal128, Binary)
- Aggregation pipeline: `$match`, `$group`, `$lookup`, `$unwind`, `$project` stages
- Index types: single-field, compound, multikey (array), text, geospatial, hashed
- Replica sets: primary + secondaries, oplog replication, automatic failover
- Read preference: primary, primaryPreferred, secondary, secondaryPreferred, nearest

**Resources to explore:**
- [ ] (to be filled)

---

### DynamoDB Concepts

**What it is:** AWS's fully managed NoSQL key-value and document database with single-digit millisecond latency at any scale, using partition keys and sort keys for data organization.

**Why it matters:** DynamoDB is the go-to database for serverless and high-scale AWS workloads — its access pattern-driven design requires a fundamentally different modeling approach than relational databases.

**Key concepts to understand:**
- Table structure: partition key (hash key) + optional sort key (range key)
- Access pattern design: DynamoDB must be designed around access patterns, not normalized data
- Global Secondary Indexes (GSI): project attributes to a new partition key for different access patterns
- Local Secondary Indexes (LSI): same partition key, different sort key — must be defined at table creation
- DynamoDB Streams: change data capture for real-time downstream processing
- Single-table design: store multiple entity types in one table using partition key prefixes — reduces latency, simplifies operations

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [SQL Deep Dive](./01-sql-deep-dive.md)
- [Entity Framework Core](./02-entity-framework-core.md)
- [Core Distributed Systems](../Phase-04-Distributed-Systems/01-core-distributed-systems.md)
- [Azure Core](../Phase-06-Cloud-And-Azure/01-azure-core.md)
