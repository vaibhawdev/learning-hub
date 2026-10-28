# Database Sharding & Partitioning

## What Is Sharding?

Sharding (also called horizontal partitioning) is the practice of splitting a large database across multiple machines (shards), where each shard holds a distinct subset of the data. Requests are routed to the shard that contains the relevant data.

```
Before Sharding:
┌─────────────────────┐
│    Single DB        │
│   Users 1–10M       │  ← Too large, too slow, single failure domain
└─────────────────────┘

After Sharding (by user ID):
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Shard 1    │  │   Shard 2    │  │   Shard 3    │
│  Users 1–3M  │  │ Users 3M–6M  │  │ Users 6M–9M  │
└──────────────┘  └──────────────┘  └──────────────┘
```

This is different from **replication** (keeping copies of the same data). Sharding splits data; replication duplicates it. Most production systems do both.

---

## Why Shard?

| Driver | Explanation |
|--------|-------------|
| **Write throughput** | A single node processes writes sequentially. Multiple shards multiply write capacity. |
| **Storage limits** | A single machine has finite disk. Shards let you exceed that. |
| **Query performance** | Smaller dataset per shard = faster table scans and index lookups. |
| **Reduced contention** | Locking a row on Shard 1 does not block reads on Shard 2. |
| **Fault isolation** | A shard failure impacts only the data subset it holds. |

---

## Vertical Partitioning vs Horizontal Partitioning

| | Vertical Partitioning | Horizontal Partitioning (Sharding) |
|-|----------------------|-------------------------------------|
| What it splits | Columns (split a table into multiple tables) | Rows (spread rows across multiple databases) |
| Example | Move `user_preferences` to a separate table from `users` | User IDs 1–1M on DB1, 1M–2M on DB2 |
| When to use | Large columns rarely accessed together, normalize schema | Table is too large for one machine |
| Complexity | Low — single database | High — cross-shard queries, routing layer |

---

## Sharding Strategies

### 1. Range-Based Sharding

Rows are assigned to shards based on a range of the shard key value.

```
Shard 1: user_id  1 – 1,000,000
Shard 2: user_id  1,000,001 – 2,000,000
Shard 3: user_id  2,000,001 – 3,000,000
```

**Pros:**
- Range queries are efficient (all rows in a range live on one shard)
- Simple to reason about
- Easy to add new shards at the top of the range

**Cons:**
- **Hotspots**: If most active users have high IDs, Shard 3 is overloaded
- Uneven distribution if data is not uniformly distributed
- Rebalancing requires moving large contiguous ranges

**Good for**: Time-series data (shard by time range), log archives, append-only datasets.

---

### 2. Hash-Based Sharding

A hash function is applied to the shard key, and the result determines the shard.

```
shard_id = hash(user_id) % num_shards

hash(user_123) % 4 = 2  → Shard 2
hash(user_456) % 4 = 0  → Shard 0
```

**Pros:**
- Even data distribution (hashes spread uniformly)
- No hotspots for randomly accessed keys
- Simple routing logic

**Cons:**
- Range queries require querying all shards (scatter-gather)
- Adding or removing shards requires rehashing most data (**resharding problem**)

**Good for**: Key-value lookups, user data accessed by ID, session stores.

---

### 3. Consistent Hashing

A clever extension of hash-based sharding that minimizes data movement when adding/removing shards.

Nodes and data keys are placed on a virtual ring using a hash function. Each key is assigned to the nearest node clockwise on the ring.

```
         0°
         │
    K3   │   K4
     ╲   │   ╱
      ╲  │  ╱
  N3   ╲ │ ╱   N1
────────(ring)────────
  N2  ╱ │ ╲   N4
     ╱  │  ╲
    ╱   │   ╲
   K2   │    K1
        │
       180°
```

When a node is added:
- Only keys between the new node and its predecessor are reassigned
- All other keys remain in place

**Virtual nodes**: Each physical node is placed at multiple positions on the ring to ensure even distribution even with a small number of nodes.

**Used in**: Cassandra, DynamoDB, Redis Cluster, Memcached (ketama).

---

### 4. Directory-Based Sharding

A lookup table (shard map) maps each key (or key range) to a specific shard. The routing layer consults this table.

```
┌─────────────────────┐
│    Shard Map        │
│  user_id → shard_id │
│  1000    → Shard 2  │
│  2000    → Shard 1  │
│  ...                │
└─────────────────────┘
```

**Pros:**
- Maximum flexibility — can move individual rows between shards
- Easy rebalancing — just update the map
- No algorithmic constraints on data placement

**Cons:**
- Shard map is a single point of failure (must be replicated)
- Extra network hop for every query (map lookup + data lookup)
- Map can become a bottleneck at very high read rates

**Used in**: MongoDB (mongos router), Vitess (MySQL sharding), Citus (PostgreSQL).

---

### 5. Geo-Based Sharding

Data is partitioned by geographic region. European users go to the EU shard, US users to the US shard.

**Pros:**
- Meets data residency / GDPR requirements
- Reduces latency (users routed to nearby shard)

**Cons:**
- User counts per region may be uneven
- Cross-region queries are expensive

---

## Choosing a Shard Key

The shard key determines data distribution and query efficiency. Choosing a bad shard key is the most common sharding mistake.

### Good Shard Key Properties

| Property | Why It Matters |
|----------|----------------|
| **High cardinality** | More distinct values → more shards, better distribution |
| **Even distribution** | Prevents hot shards |
| **Aligns with access patterns** | Most queries touch only one shard |
| **Immutable** | If the key changes, the row must move to a new shard |
| **Present in most queries** | Without it, you scatter-gather across all shards |

### Common Shard Keys

| Use Case | Good Shard Key | Bad Shard Key |
|----------|----------------|---------------|
| Multi-tenant SaaS | `tenant_id` | `created_at` (time-based hotspot) |
| E-commerce | `customer_id` | `product_category` (uneven) |
| Social media | `user_id` | `country` (low cardinality) |
| IoT / time-series | `device_id + time bucket` | `status` (very low cardinality) |

### Compound Shard Keys

Sometimes a single key causes imbalance. Compound keys improve distribution:

```
shard_key = (tenant_id, date_bucket)
```

This distributes by tenant but also by time within each tenant.

---

## Problems Sharding Introduces

### Cross-Shard Queries (Scatter-Gather)

A query that does not include the shard key must be sent to all shards and results merged:

```sql
SELECT * FROM orders WHERE status = 'pending'  -- no shard key → hits all shards
SELECT * FROM orders WHERE customer_id = 123   -- shard key present → hits one shard
```

Minimize scatter-gather by:
- Always including the shard key in frequently run queries
- Denormalizing data so common joins live on the same shard
- Using a secondary index service (e.g., Elasticsearch) for non-key queries

### Cross-Shard Transactions

ACID transactions spanning multiple shards require distributed transactions (2PC or Saga pattern). These are slow and complex. The best solution is to design your data model so transactions stay within a single shard.

### Rebalancing (Resharding)

When a shard grows too large or is underperforming, you must move data to a new shard. This is called resharding and is operationally complex:

- Data must be migrated without downtime
- The routing layer must be updated atomically
- Replication lag during migration can cause inconsistency

**Strategies:**
- **Pre-split**: Create more shards than you need from the start (Cassandra, MongoDB)
- **Consistent hashing**: Minimizes movement when adding nodes
- **Double-write + backfill**: Write to old and new shard simultaneously, backfill, then switch

### Hotspot Shards

A single shard receiving disproportionate traffic (celebrity user, popular product) becomes a bottleneck despite sharding. Solutions:

- Subdivide the hotspot key (split the shard further)
- Add a read replica for that shard
- Cache the hot data in front of the shard

---

## Sharding vs Alternatives

### Alternative 1: Read Replicas

Add read replicas to offload read traffic. The primary handles all writes; replicas serve reads.

**When to prefer**: Read-heavy workload (>80% reads), writes are manageable on a single primary.

**Limitation**: Does not help with write throughput or storage limits.

### Alternative 2: Vertical Scaling

Upgrade to a larger machine (more CPU, RAM, SSD).

**When to prefer**: You haven't hit the vertical scaling ceiling yet. A 10x larger machine is cheaper and simpler than sharding.

**Limitation**: Has a hard ceiling; single point of failure.

### Alternative 3: Caching Layer

Put Redis or Memcached in front of the database. Most reads never hit the database.

**When to prefer**: Read-heavy, cacheable data (product catalog, user profile).

**Limitation**: Does not help with write throughput; cache invalidation is hard.

### Alternative 4: NewSQL Databases

Databases like CockroachDB, Spanner, Yugabyte, and TiDB provide horizontal scaling with ACID guarantees — they handle sharding internally.

| Database | Sharding | Consistency | Best For |
|----------|----------|-------------|----------|
| CockroachDB | Automatic | Serializable | SQL at scale, multi-region |
| Google Spanner | Automatic | External consistency | Global, financial-grade |
| Yugabyte | Automatic | Serializable | PostgreSQL-compatible, cloud-native |
| TiDB | Automatic | Snapshot isolation | MySQL-compatible, HTAP |

**When to prefer**: You need ACID + horizontal scale and are willing to pay the operational cost of a distributed SQL system.

### Alternative 5: Polyglot Persistence

Use different databases for different access patterns:
- PostgreSQL for transactional data
- Elasticsearch for full-text search
- Redis for session cache
- ClickHouse for analytics

**When to prefer**: Different query patterns genuinely need different storage engines.

---

## Implementing Sharding: Step-by-Step

### Step 1: Identify the bottleneck

Is it read throughput? → Try replicas or caching first.
Is it write throughput or storage? → Sharding is likely needed.

### Step 2: Choose the sharding approach

- **Use a managed database** (CockroachDB, Vitess, PlanetScale) if you want to avoid building the routing layer.
- **Use consistent hashing** if you expect frequent node additions.
- **Use range-based** if range queries dominate.

### Step 3: Choose the shard key

- Analyze your top 10 most frequent query patterns
- Identify which key appears in all of them
- Verify the key has high cardinality and even distribution
- Verify the key will not need to change (immutability)

### Step 4: Plan for cross-shard operations

- Identify which queries will become scatter-gather
- Add secondary indexes or a search layer for them
- Identify cross-shard transactions and refactor to single-shard

### Step 5: Plan resharding from day one

- Use pre-split shards (start with 128 logical shards on 4 physical nodes)
- Document the migration procedure before you need it
- Test resharding in a staging environment

---

## Production Checklist

- [ ] Shard key is immutable and present in all hot queries
- [ ] No cross-shard transactions in the critical path
- [ ] Scatter-gather queries have explicit acknowledgment and timeouts
- [ ] Each shard has a read replica
- [ ] Resharding runbook documented and tested
- [ ] Monitoring per-shard: QPS, latency, error rate, storage
- [ ] Hotspot detection alerting in place
- [ ] Backup and recovery procedure tested per-shard

---

## Tools & Products

| Tool | Type | Notes |
|------|------|-------|
| Vitess | MySQL sharding | Used by YouTube, Slack |
| Citus | PostgreSQL sharding | Extension; keeps SQL compatibility |
| PlanetScale | MySQL-compatible | Managed, schema changes without locks |
| CockroachDB | Distributed SQL | Auto-sharding, ACID |
| Cassandra | Distributed NoSQL | Consistent hashing, AP |
| MongoDB Atlas | Document DB | Auto-sharding, managed |
| Amazon Aurora | MySQL/Postgres | Shared storage, read replicas |
| TiDB | MySQL-compatible | HTAP, auto-sharding |

---

## Next Steps

- [Distributed Caching](./04-distributed-caching.md) — reduce load before it hits your shards
- [Distributed Transactions](./17-distributed-transactions.md) — handle cross-shard consistency
- [Core Concepts](./02-core-concepts-cap-theorem.md) — consistency models that govern shard behavior
