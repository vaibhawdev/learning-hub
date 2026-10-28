# Data Partitioning & Sharding — Deep Dive

> **Phase:** 3 — Database Engineering
> **Status:** Not Started
> **Level:** Beginner → Advanced (read top to bottom; each section builds on the last)

## Overview

Every database eventually hits a wall: too much data for one disk, too many writes for one CPU, or too many concurrent users for one connection pool. Partitioning and sharding are the two core techniques for breaking a dataset into smaller pieces so it can scale past what a single machine (or single table) can handle.

They sound similar and are constantly used interchangeably — that's the first source of confusion this document clears up. Then it goes through the actual algorithms used to decide "which piece does this row belong to," how real systems (Postgres, Citus, MongoDB, Cosmos DB, DynamoDB, Vitess, Cassandra) implement them, how to pick a strategy for a given access pattern, and the long list of things that quietly go wrong in production.

This is a reference document, not a checklist — read it in order the first time.

---

## Part 1 — Foundations (Beginner)

### 1.1 The core problem

A single database node has finite:
- **CPU/IOPS** — how many queries per second it can execute
- **RAM** — how much working set can be cached before every read hits disk
- **Disk** — how much data can be stored at all
- **Connections** — how many concurrent clients it can serve

When you outgrow any of these, you have two broad options:

1. **Scale up (vertical scaling):** bigger machine. Simple, but has a ceiling (biggest cloud instance) and gets expensive fast, and doesn't fix availability (still one box, one failure domain).
2. **Scale out (horizontal scaling):** split the data across multiple machines/tables. Unlimited in principle, but every piece of infrastructure (queries, transactions, backups, migrations) becomes harder because "the data" is no longer in one place.

Partitioning and sharding are both forms of scale-out. The difference is *where* the split happens.

### 1.2 Partitioning vs. Sharding vs. Replication — don't confuse these

This is the single most common source of confusion for people new to this space. Pin these definitions down before reading further.

| Term | What's split | Where it lives | Solves |
|---|---|---|---|
| **Vertical partitioning** | *Columns* of a table | Usually same DB, sometimes separate tables/stores | Reduce row width, isolate hot columns, separate by access pattern |
| **Horizontal partitioning** | *Rows* of a table, by a key | **Same database instance**, multiple physical structures | Manageability, partition pruning, faster maintenance — NOT more total capacity |
| **Sharding** | *Rows* of a table, by a key | **Multiple independent database instances/nodes** | More total CPU/RAM/disk/IOPS — true horizontal scale |
| **Replication** | Nothing — full copies | Multiple nodes, each with the *same* full dataset | Read scaling, availability, durability — not write scaling or storage scaling |

**[Guessing at your mental model, so let's be explicit]:** partitioning splits *within* a box (or within a logically-managed multi-node cluster where you don't manage placement yourself); sharding splits *across* boxes that you (or your ORM/middleware) are explicitly responsible for routing to. In practice the line blurs — Postgres "partitioning" is single-instance, but Citus/Vitess "shard" a partitioned table across many Postgres/MySQL instances using almost identical key-selection logic. The *algorithms* for choosing a key are shared between both; what differs is the blast radius of getting it wrong.

Many managed NoSQL systems (Cosmos DB, DynamoDB, MongoDB, Cassandra) call their unit "partitions" even though the partitions are physically distributed across separate machines — i.e., what a relational DBA would call "shards." Vocabulary is not consistent across the industry. Read for the *concept*, not the label.

### 1.3 Why you should try hard to avoid needing this

**[Certain]** Every technique in this document adds complexity: cross-shard queries, distributed transactions, rebalancing, uneven load, operational tooling. Before reaching for sharding, exhaust cheaper options:

1. Better indexing / query tuning (see [SQL Deep Dive](./01-sql-deep-dive.md))
2. Read replicas (solves read scaling, not write scaling or storage)
3. Caching (see [NoSQL and Caching](./03-nosql-and-caching.md))
4. Vertical scaling (bigger box — cloud instances now go very large)
5. Archiving cold data out of the hot table (often solves the "table is huge" problem without sharding at all)

Sharding is usually the *last* resort, adopted only when write throughput or dataset size genuinely exceeds one node's capacity — not because a table "feels big."

---

## Part 2 — Partitioning (Intermediate)

### 2.1 Vertical partitioning

Split a wide table by column groups, based on access pattern:

```
users(id, email, password_hash, name, bio, profile_photo_url,
      last_login_at, preferences_json, marketing_opt_in)

→ users_core(id, email, password_hash, name)          -- hot, small rows
→ users_profile(id, bio, profile_photo_url, prefs)    -- warm, large rows
→ users_activity(id, last_login_at)                    -- write-heavy, hot
```

**Why:** keeps frequently-read/small columns cache-resident and out of the way of large BLOB/TEXT columns; isolates high-write columns (like `last_login_at`) so they don't cause lock contention or page churn on rarely-updated columns.

**Gotcha:** every split adds a join to reconstruct the full entity. If most queries need the full row anyway, you've traded one cheap read for two expensive ones.

### 2.2 Horizontal partitioning strategies

These are the core **algorithms** for deciding which partition a row belongs to. They apply identically whether the partitions live in one Postgres instance or across 200 shards — this is the part that transfers directly to sharding in Part 3.

#### A. Range partitioning

Partition boundaries are defined by ranges of the partition key.

```
Partition 1: order_date < '2024-01-01'
Partition 2: '2024-01-01' <= order_date < '2024-04-01'
Partition 3: '2024-04-01' <= order_date < '2024-07-01'
Partition 4: order_date >= '2024-07-01'
```

**Algorithm to route a row:** binary search the sorted list of boundary values for the partition key → O(log n) where n = number of partitions.

```
function findPartition(key, boundaries):       # boundaries sorted ascending
    lo, hi = 0, len(boundaries) - 1
    while lo < hi:
        mid = (lo + hi) // 2
        if key < boundaries[mid]:
            hi = mid
        else:
            lo = mid + 1
    return lo
```

**Strengths:**
- Range scans (`WHERE order_date BETWEEN ...`) touch only the relevant partitions — **partition pruning/elimination**.
- Natural fit for time-series data; old partitions can be dropped/archived in O(1) (`DETACH PARTITION` / partition switch) instead of a row-by-row `DELETE`.
- Easy to reason about, easy to add new partitions at the tail (e.g., a new month).

**Gotchas:**
- **Hot partition problem**: if the key is monotonically increasing (timestamps, auto-increment IDs), *all* current writes land in the single newest partition — you get zero write parallelism even though you have N partitions. This is the #1 mistake beginners make: "I partitioned by date, why is only one partition busy?" — because all *today's* traffic goes there, always.
- Uneven partition sizes if data volume isn't uniform over the ranges (e.g., a viral event causes one day's partition to dwarf the others).
- Choosing boundaries requires either foreknowledge of data distribution or an ongoing maintenance job to add new ranges (e.g., a scheduled job to pre-create next month's partition — forgetting this causes silent overflow into a default/error partition, or insert failures).

#### B. Hash partitioning

Apply a hash function to the partition key, then map the hash to a partition, typically via modulo.

```
partition = hash(key) mod N
```

**Strengths:**
- Distributes rows uniformly regardless of key distribution — no hot partition from monotonic keys.
- Simple, deterministic, no boundary maintenance.

**Gotchas:**
- **No range queries**: `WHERE id BETWEEN 100 AND 200` must scan *every* partition, because hash destroys ordering. If your dominant query pattern is range scans, hash partitioning is often the wrong choice.
- **Changing N is catastrophic** with plain modulo: going from 4 to 5 partitions reshuffles ~80% of all keys (see §4.2 — this is *the* reason consistent hashing exists).
- Still susceptible to hot partitions if the key has few distinct values (e.g., partitioning by `status` with only 3 possible values) or if one value is disproportionately common (a "celebrity" tenant/user — the **hot key problem**, distinct from the hot *partition* problem above, and hash partitioning does *not* fix it).

#### C. List / Directory-based partitioning

Explicitly map discrete key values to partitions.

```
Partition US: region IN ('us-east', 'us-west')
Partition EU: region IN ('eu-west', 'eu-central')
Partition APAC: region IN ('ap-south', 'ap-southeast')
```

**Strengths:** full control — useful for compliance/data-residency (EU customer data must stay in EU), or grouping by known categorical values.

**Gotchas:** manual maintenance as new category values appear (new region added → must update the mapping); imbalance if categories aren't equally sized (one region has 90% of users).

#### D. Composite / hybrid partitioning

Combine two strategies — e.g., range-partition by month, then hash-subpartition within each month by `tenant_id`. Gets partition pruning on time *and* write distribution across tenants within a given time window. Most large-scale time-series systems (Cassandra, Cosmos DB, Citus) end up here in practice.

**Gotcha:** doubles the operational surface — two boundary/hash schemes to reason about, two places rebalancing can go wrong.

#### E. Round-robin partitioning

Row *i* goes to partition `i mod N`, with no regard to key value at all — pure load-balancing, no ability to route a query to a specific partition without scanning all of them.

**Use case:** rare in OLTP; occasionally used for write-distribution in systems where every query is a full scan anyway (some analytics/columnar stores use round-robin distribution deliberately, e.g., Postgres's `CREATE TABLE ... DISTRIBUTED RANDOMLY` equivalents in some MPP systems) — the workload is scan-heavy analytics, not point lookups, so losing point-routing costs nothing.

### 2.3 Decision table — partitioning strategy by access pattern

| Your dominant query is... | Choose | Because |
|---|---|---|
| `WHERE date BETWEEN x AND y`, and old data is archived/deleted in bulk | **Range** (on date) | Partition pruning + O(1) archival via partition switch |
| `WHERE id = ?` point lookups, evenly distributed keys | **Hash** | Even write/storage distribution, no hot partition |
| `WHERE id = ?` but id is auto-increment/sequential/timestamp-based | **Hash**, not range | Range on a monotonic key = hot partition; hash breaks the correlation |
| `WHERE tenant_id = ? AND date BETWEEN x AND y` | **Composite**: hash(tenant) + range(date), or vice versa depending on which predicate is more selective | Get pruning on both axes |
| `WHERE region = 'EU'` for compliance | **List** | Explicit control over physical/legal placement |
| Full-table analytical scans, no point lookups | **Round-robin** or range aligned to scan order | Point-routing has no value if every query scans everything anyway |

---

## Part 3 — Sharding (Intermediate → Advanced)

Sharding takes the same key-routing algorithms from Part 2 and applies them across **independent database instances** rather than partitions within one instance. Everything about *choosing* a shard key is identical to choosing a partition key — the new problems are about *routing*, *rebalancing*, and *cross-shard operations*, because now the pieces are physically and often geographically separate.

### 3.1 Shard key selection — the single most important decision

**[Certain]** Get the shard key wrong and every other decision in this document is fighting a losing battle; it is very expensive to change later (effectively a full data migration). Criteria, in priority order:

1. **High cardinality** — enough distinct values to spread across all shards (don't shard by `country` if you have 500 shards and 190 countries).
2. **Even distribution** — no value should dominate. Watch for power-law distributions (one enterprise tenant with 40% of all data — the "whale tenant" problem).
3. **Aligns with the dominant query pattern** — the *majority* of queries should be satisfiable using the shard key alone (single-shard queries), because cross-shard queries are expensive (§3.4).
4. **Aligns with transaction boundaries** — operations that must be transactional/atomic together should live on the same shard. This is why `tenant_id`, `user_id`, `account_id` are extremely common shard keys: most transactional work naturally scopes to one tenant/user/account.
5. **Stable over the entity's lifetime** — if the shard key value can change (e.g., a user changes their "home region"), you need a cross-shard move procedure; prefer immutable keys (e.g., the user's original signup ID rather than a mutable attribute).

**Common good shard keys:** `tenant_id` (B2B SaaS), `user_id` (consumer apps), `account_id`, `customer_id`, geographic region (when data residency matters and access is regional).

**Common bad shard keys:** auto-increment ID (hot shard — all new writes go to the highest-ID shard, same monotonic problem as range partitioning above), `created_at`/timestamp, low-cardinality enums (`status`, `plan_tier`), anything mutable that changes after creation.

### 3.2 Sharding architectures — who does the routing?

#### Algorithmic (client-side) sharding

The application (or a thin client library) computes `shard = f(key)` itself and connects directly to the right database.

- **Pros:** no extra network hop, no extra infrastructure component, simple mental model.
- **Cons:** every application/service must embed and agree on the same routing logic; resharding requires coordinated redeploys or a routing-logic version bump everywhere.

#### Lookup/directory-based sharding

A separate service or table maps key → shard, consulted on every request (with caching).

```
shard_map: { tenant_id → shard_id }   -- lives in a small, fast, highly-available store
```

- **Pros:** rebalancing is just updating map entries — no client-side logic changes, moves can be granular (single key at a time), enables "put this one whale tenant on its own dedicated shard."
- **Cons:** the directory itself is a critical dependency — must be fast, cached aggressively, and highly available, or it becomes a single point of failure/bottleneck for *every* query in the system.

#### Middleware / proxy sharding

A proxy layer sits between application and databases and handles routing transparently, so the application mostly writes normal SQL. Examples: **Vitess** (MySQL, built for YouTube's scale, now CNCF), **Citus** (Postgres extension, distributes tables transparently), **ProxySQL** (routing rules on top of MySQL).

- **Pros:** application code barely needs to know sharding exists; centralizes rebalancing, connection pooling, query routing, and often cross-shard query fan-out.
- **Cons:** another moving piece to operate; proxy becomes a potential bottleneck/SPOF unless deployed redundantly; not every query pattern is transparently supported (cross-shard joins/aggregates are still limited or slow).

#### Fully managed (the database does it for you)

Cosmos DB, DynamoDB, MongoDB (sharded clusters), Cassandra — you declare a partition/shard key, the system handles physical placement, rebalancing, and routing internally. You still must choose the key correctly (§3.1) — the system automates *mechanics*, not *strategy*.

### 3.3 How real systems implement this (worked examples)

- **MongoDB sharded cluster:** `mongos` routers hold no data themselves; **config servers** store the chunk-to-shard map; data is split into **chunks** (contiguous ranges of the shard key) that get migrated between shards by the **balancer** when uneven. Shard key choice directly determines chunk splittability — a low-cardinality key can create a chunk that can never be split further (a **jumbo chunk**), which cannot be migrated and permanently overloads its shard.
- **Cosmos DB:** logical partitions (by partition key value) are transparently packed into **physical partitions** (the actual scale unit, each with a fixed throughput/storage ceiling ~20GB in Core SQL API at time of writing). Choosing a low-cardinality or skewed partition key causes one physical partition to hit its storage ceiling or throughput (RU) ceiling while others sit idle — this shows up as `429` throttling on the hot partition specifically, while total provisioned RU/s looks unused in aggregate.
- **DynamoDB:** partition key hash determines which physical partition; historically partitions had hard-divided provisioned throughput per partition (pre-adaptive-capacity), so a hot key could throttle even with spare capacity elsewhere in the table — DynamoDB's **adaptive capacity** and **on-demand mode** mitigate but don't eliminate this.
- **Vitess:** shards a MySQL table using **VSchema**; supports resharding via **VReplication**, which streams changes from old shards to new shards, allowing an online, low-downtime cutover.
- **Citus (Postgres):** distributes tables by a **distribution column**; co-locates tables sharing the same distribution column on the same physical node so joins on that column stay local; `citus_rebalance_start()` moves shards between nodes online.
- **Cassandra:** uses **consistent hashing** on a token ring (§4.1) natively — this is the textbook implementation to study if you want to understand the algorithm end-to-end.

### 3.4 Cross-shard operations — the tax you pay

Once data is spread across shards, anything that isn't a single-shard-key lookup gets expensive:

- **Scatter-gather queries:** a query without the shard key in its predicate must fan out to *every* shard, wait for the slowest one (tail latency = max of N shards, not average), then merge results in the application/middleware layer. `ORDER BY` + `LIMIT` across shards is particularly nasty — you must over-fetch from each shard before you can correctly determine the true top-K.
- **Cross-shard transactions:** ACID across shards requires distributed transaction protocols — two-phase commit (2PC) or the Saga pattern (compensating actions instead of true atomicity). Both add latency and failure-mode complexity; most systems avoid cross-shard transactions structurally (via shard key choice, §3.1.4) rather than solving them technically.
- **Cross-shard secondary indexes / uniqueness constraints:** "email must be unique" is trivial on one node; across shards it either requires a global index (itself a scaling bottleneck/single point of contention) or a separate lookup service, or you accept eventual/best-effort enforcement.
- **Joins across shards:** either denormalize to avoid them, co-locate related data on the same shard (Citus's colocation), or accept an application-level join (fetch from shard A, then fetch from shard B using IDs from A — N+1 across network hops).
- **Aggregate queries** (`COUNT(*)`, `SUM(...)`, global leaderboards): require scatter-gather plus a merge/reduce step; often better served by a separate analytical/OLAP pipeline (see [NoSQL and Caching](./03-nosql-and-caching.md)) rather than live cross-shard aggregation.

---

## Part 4 — Core Algorithms (Advanced)

This section is the algorithmic heart of sharding: **how do you map keys to nodes such that adding/removing a node moves the minimum possible amount of data?**

### 4.1 Naive hash mod N — and why it fails

```
shard = hash(key) mod N
```

Works great — until N changes. Adding one node (N → N+1) changes the modulus for *every* key, so on average **(N-1)/N of all keys remap** to a different shard. For N=4 → 5, that's 80% of your data needing to move just to add 20% more capacity. This is unacceptable at scale — hence consistent hashing.

### 4.2 Consistent hashing

**Core idea:** instead of hashing keys onto `[0, N)`, hash both keys *and* nodes onto a fixed, large circular space (a **ring**), e.g., `[0, 2^32)`. A key belongs to the first node encountered walking clockwise from the key's hash position.

```
Ring: 0 ────────────────────────────────── 2^32-1  (wraps around to 0)

Nodes placed at hash(nodeA), hash(nodeB), hash(nodeC) on the ring.
Key K belongs to the next node clockwise from hash(K).
```

**Algorithm:**

```
function assignNode(key, ring):              # ring = sorted list of (position, nodeId)
    h = hash(key)
    idx = binarySearchCeiling(ring, h)        # first position >= h
    if idx == len(ring): idx = 0              # wrap around
    return ring[idx].nodeId

function addNode(newNode, ring):
    pos = hash(newNode)
    insertSorted(ring, (pos, newNode))
    # only keys between the *previous* node and `pos` move to newNode
    # every other key's owner is unaffected

function removeNode(node, ring):
    remove (hash(node), node) from ring
    # keys that belonged to `node` now belong to the *next* node clockwise
    # every other key's owner is unaffected
```

**Why it works:** adding/removing one node only affects the keys in the ring-arc adjacent to that node — on average **only 1/N of keys move**, not (N-1)/N. This is the property that makes online rebalancing feasible at all.

**The virtual nodes (vnodes) refinement:** with only one ring-position per physical node, load is uneven — some nodes cover a much larger arc than others just by hash luck (which gets worse with fewer nodes). Fix: place each physical node at **many** positions on the ring (e.g., 100–256 virtual nodes each, using `hash(nodeId + ":" + i)` for `i in 0..V`).

**Gotcha with vnodes:** more vnodes = smoother load distribution but larger routing metadata and slower ring updates; too few vnodes (or too few physical nodes overall) = visible imbalance even though the algorithm is "correct." Cassandra defaults changed historically (from 256 vnodes/node down to lower defaults) precisely because very high vnode counts caused their own operational overhead (larger gossip payloads, slower repair).

**Real-world users:** Cassandra, Amazon Dynamo (the paper, not DynamoDB-the-product specifically, though DynamoDB's lineage traces here), Riak, many CDN/load-balancer hash-routing layers, Discord's message sharding.

### 4.3 Rendezvous hashing (Highest Random Weight — HRW)

An alternative to ring-based consistent hashing that needs no ring data structure at all.

```
function assignNode(key, nodes):
    best_node, best_score = None, -infinity
    for node in nodes:
        score = hash(key, node)          # combined hash of key+node identity
        if score > best_score:
            best_node, best_score = node, score
    return best_node
```

Every node computes a score for the key; the node with the highest score "wins." When a node is removed, only the keys that had picked it need a new home (recompute among survivors) — everyone else's winner is unchanged, since their scores against remaining nodes didn't change. No ring, no vnodes, O(N) per lookup (fine for modest N; for very large N, a ring with O(log N) lookup wins).

**Used by:** CoreDNS/Kubernetes' internal service load-balancing in some configurations, some CDN request routing.

### 4.4 Jump Consistent Hash

A 2014 Google algorithm — no ring, no storage, pure computation, O(log N) time, and guarantees keys are *perfectly* evenly distributed (unlike ring hashing, which needs vnodes to get close to even). The catch: it only supports adding/removing nodes at the *end* (i.e., nodes are numbered `0..N-1`, and you can only grow/shrink N) — it can't handle arbitrary node removal from the middle without treating it as "add a replacement" bookkeeping externally.

```
function jumpConsistentHash(key, numBuckets):
    b, j = -1, 0
    k = hash(key)   # a 64-bit hash
    while j < numBuckets:
        b = j
        k = k * 2862933555777941757 + 1        # LCG-style mix
        j = floor((b + 1) * (2^31) / ((k >> 33) + 1))
    return b
```

**Used by:** Google Cloud internal systems, some sharding layers where node identities are simple ordinal indices rather than arbitrary hosts (works less naturally when nodes have real identities like hostnames/IPs, since those don't map cleanly to sequential ordinals).

### 4.5 Range-based splitting (the alternative to hash rings)

Systems that need range queries (HBase regions, Cosmos DB physical partitions, CockroachDB/Spanner ranges, Postgres/Citus partitions) don't use hash rings at all — they use dynamically-split contiguous ranges.

**Algorithm (region/range splitting):**

```
1. Start with one range covering the entire keyspace, on one node.
2. Monitor range size/load continuously.
3. When a range exceeds a size/load threshold:
   a. Pick a split point (commonly the median key currently in the range).
   b. Split into two ranges at that point.
   c. Optionally migrate one of the two halves to a less-loaded node.
4. When two adjacent ranges are both small/cold, merge them back into one (compaction).
```

This is how HBase regions, Cosmos DB physical partitions, and CockroachDB/Spanner ranges scale automatically — the system handles §4.5 for you, but the size/load threshold and split-point heuristics are exactly why a bad partition *key* (not just bad range boundaries) still causes trouble: if all writes carry keys that sort into the same range, that range splits over and over but the *newest* sub-range is still the only one taking writes (the hot-partition problem from §2.2.A, reappearing here as "unbounded region splitting doesn't help if 100% of writes land in the newest split").

### 4.6 Resharding / rebalancing in practice

Moving from N to N+k shards live, without downtime, generally follows this pattern (used in some form by Vitess VReplication, MongoDB balancer, Citus rebalancer):

```
1. Provision new shard(s).
2. Begin dual-write or CDC-based streaming: every write to the source shard
   is also applied (synchronously or via change-data-capture) to the destination.
3. Backfill: bulk-copy existing data for the key range being moved.
4. Catch up: drain the replication lag between backfill snapshot and live stream.
5. Cut over: atomically flip the routing map (directory/config server) so new
   reads/writes go to the destination shard.
6. Dual-write a short grace period (safety net for in-flight requests using stale routing).
7. Decommission the source copy of the moved range.
```

**Gotchas in this process:**
- **Split-brain during cutover:** if the routing map update isn't atomic/consistent across all clients, some requests hit the old shard and some hit the new one simultaneously — classic source of duplicate/lost writes. This is why directory-based routing (§3.2) is much easier to reshard than pure client-side algorithmic sharding — there's one place to flip.
- **Backfill lag never converging:** if the source shard's write rate is high enough, a naive backfill can never catch up to the live stream. Real implementations checkpoint and iterate (copy, then replay the delta, then replay the smaller delta, until the remaining delta is small enough to apply within a brief cutover pause).
- **Resource contention during migration:** the backfill itself consumes I/O/CPU on the *already-loaded* source shard — resharding to relieve load can temporarily make things worse before better.

---

## Part 5 — Decision Guide

### 5.1 "Do I even need this?"

```
Is a single, well-tuned, vertically-scaled instance (with read replicas
and caching) actually insufficient for your write throughput / storage / IOPS?
        │
        ├── No  → Don't shard. Fix indexes, add caching, add read replicas,
        │          scale up the box. (See Part 1.3.)
        │
        └── Yes → Continue.
```

### 5.2 Choosing a shard key — checklist

Ask, in order:

1. What's the entity most transactions/queries are scoped to? (tenant, account, user) → strong candidate.
2. Is it high-cardinality and evenly distributed? If a few values dominate (whale tenants), plan for shard-per-whale exceptions up front.
3. Is it immutable for the life of the record? If not, plan the cross-shard move procedure now, not after the first customer needs one.
4. Does it let the majority of real queries hit a single shard? If your most common query can't include the shard key, reconsider the key or plan to denormalize/duplicate data so it can.
5. Do you need range scans on some other attribute? If so, prefer composite (hash-shard by tenant, range-partition by date *within* each shard) over pure hashing.

### 5.3 Strategy selection matrix

| Situation | Recommended approach |
|---|---|
| Multi-tenant SaaS, per-tenant data mostly isolated | Hash/directory shard by `tenant_id`; dedicated shard for outlier whale tenants |
| Consumer social app, per-user timelines/feeds | Hash shard by `user_id`; accept fan-out for cross-user queries (feeds) via a separate service |
| Time-series/IoT/logs, mostly appended and range-queried, old data expires | Range partition by time (single instance or per-shard); combine with hash sub-partition by device/source ID for write parallelism |
| Global app with data-residency/compliance requirements | List/geo-shard by region first, then hash within region |
| Need strict cross-entity transactions (e.g., transferring money between two arbitrary accounts) | Keep the transactional scope on one shard by design (e.g., shard by a shared "ledger" or "organization" key), or accept a Saga/2PC cost — don't shard the transactional core arbitrarily |
| Read-heavy, write-light, dataset fits growing but manageable | Don't shard yet — read replicas + caching first |
| Need range queries *and* even write distribution | Composite key: hash(high-cardinality-id) as the primary shard dimension, range(date) as the sub-partition within it |

---

## Part 6 — Gotchas & Pitfalls (Consolidated)

A concentrated list — many of these are mentioned inline above but worth having in one place as a pre-launch checklist.

- **Hot partition from monotonic keys** — range-partitioning (or naively sharding) by timestamp/auto-increment ID puts 100% of *current* writes on one partition regardless of how many partitions exist.
- **Hot key ("celebrity") problem** — even with perfectly uniform hash distribution, a single disproportionately popular key (viral post, whale tenant) overloads whichever one shard it lands on; hashing does not fix uneven *per-key* load, only uneven *key-count* distribution.
- **Resharding cost with naive `hash mod N`** — changing shard count reshuffles ~(N-1)/N of all data; always use consistent hashing, rendezvous hashing, or dynamic range splitting instead of raw modulo if resharding is ever expected.
- **Jumbo/unsplittable chunks** — a shard key with too few distinct values (or one dominant value) can produce a chunk/range that physically cannot be split further, permanently pinning excess load to one node (seen literally as "jumbo chunks" in MongoDB).
- **Cross-shard joins and secondary indexes** — anything not keyed by the shard key becomes scatter-gather or requires a separate global index service; plan for this before launch, not after the first "just add a `WHERE email = ?` query" request.
- **Cross-shard uniqueness constraints** — "unique email" is trivial on one node, hard across shards; typically solved with a separate lookup table/service, not a database constraint.
- **Distributed transactions are slow and fragile** — 2PC adds latency and introduces new failure modes (coordinator crash mid-commit); Sagas trade atomicity for compensating actions that must themselves be idempotent and carefully designed. Prefer shard-key design that avoids needing cross-shard transactions altogether.
- **`ORDER BY ... LIMIT` across shards is deceptively expensive** — correct pagination requires over-fetching top-K from *every* shard before merging, not just K total; naive implementations return wrong results under concurrent writes or shard skew.
- **Tail latency amplification** — a scatter-gather query's latency is bounded by the *slowest* shard, not the average; more shards touched per query = higher probability of hitting a slow/GC-paused/network-blipped node.
- **Split-brain during rebalancing cutover** — if the shard map update isn't atomic across all routing clients, concurrent requests can be served by both old and new locations, causing lost or duplicated writes.
- **Backfill-never-converges during resharding** — a high enough write rate on the source can outpace a naive one-shot backfill; production resharding tools iterate the copy-and-replay-delta loop until the remaining gap is small enough for a brief stop-the-world cutover.
- **Migrating shard key values** — if the shard key can change after creation (region reassignment, account merge/split), you need an explicit cross-shard move procedure; don't discover this requirement in an incident.
- **Backup/restore complexity** — a consistent point-in-time backup across independently-operating shards requires either a globally synchronized snapshot mechanism or accepting some cross-shard inconsistency in the backup; test your restore procedure, not just your backup job.
- **Operational tooling assumes one database** — schema migrations, analytics queries, ad-hoc debugging queries, and admin dashboards all need to become "shard-aware" — this tooling gap is consistently underestimated in sharding project timelines.
- **Uneven growth over time** — a shard key that's evenly distributed *today* may not stay that way; monitor per-shard size/load continuously, don't assume a good initial choice stays good forever.
- **Vnode/virtual-node count tuning** — too few virtual nodes per physical node in a consistent-hash ring causes visible load imbalance even though the algorithm is theoretically correct; too many inflates cluster metadata and slows gossip/membership operations.
- **Confusing "partitioning" across vendors** — remember Cosmos DB/DynamoDB/Cassandra "partitions" are shards in the classic sense (separate physical nodes), while Postgres/SQL Server "partitions" are usually single-instance — don't assume operational characteristics (e.g., failure isolation) transfer between the two just because the word is the same.

---

## Summary

- **Partitioning** splits rows within a manageably-scoped system (often one instance); **sharding** splits rows across independent instances for true horizontal scale; **replication** copies the whole dataset and solves a different problem (reads/availability, not capacity).
- The **key-selection algorithms are shared** between partitioning and sharding: range, hash, list/directory, and composite/hybrid — pick based on whether your dominant access pattern is range scans (favor range) or point lookups (favor hash), and always check for hot-key/hot-partition risk from skewed or monotonic keys.
- **Consistent hashing** (and its variants — rendezvous hashing, jump consistent hash) exists specifically to solve the "changing node count reshuffles almost everything" problem of naive `hash mod N`; range-based systems solve the same rebalancing problem via dynamic split/merge of contiguous ranges instead.
- The shard key is the highest-leverage, hardest-to-reverse decision in this whole area — optimize it for cardinality, even distribution, transactional scope, and alignment with your dominant query pattern, in that order.
- Cross-shard operations (joins, transactions, unique constraints, pagination, aggregation) are the real tax of sharding — design the shard key so the vast majority of real traffic never needs them, rather than trying to make them fast after the fact.
- Don't shard until cheaper options (indexing, caching, read replicas, vertical scaling) are genuinely exhausted — every technique here adds operational surface area that a single well-tuned instance doesn't have.

## Related Sections

- [SQL Deep Dive](./01-sql-deep-dive.md) — see the "Partitioning" and "Sharding" outline entries there for the single-instance/relational angle this document expands on
- [NoSQL and Caching](./03-nosql-and-caching.md) — Redis Cluster hash slots, Cosmos DB, MongoDB, and DynamoDB partitioning specifics
- [Entity Framework Core](./02-entity-framework-core.md)
- [Distributed Systems](../Phase-04-Distributed-Systems/) — consistent hashing, consensus, and cross-node coordination in more general distributed contexts
- [Database Performance](../Phase-08-Performance-Engineering/02-database-performance.md)
