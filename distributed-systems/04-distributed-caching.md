# Distributed Caching

## What Is a Cache?

A cache is a fast, in-memory data store that holds frequently accessed data so that it does not need to be fetched from a slower data source (database, external API) on every request.

```
Without cache:
Request → App → Database (10ms) → Response (10ms)

With cache hit:
Request → App → Cache (0.1ms) → Response (0.1ms)

With cache miss:
Request → App → Cache miss → Database (10ms) → Cache write → Response (10ms)
```

At scale, a cache can reduce database read load by 90%+, allowing a smaller or slower database tier to serve much higher traffic.

---

## Redis vs Memcached

The two dominant in-memory caching solutions.

| Feature | Redis | Memcached |
|---------|-------|-----------|
| Data structures | String, Hash, List, Set, Sorted Set, Stream, Bitmap, HyperLogLog, JSON (module) | String only (binary-safe) |
| Persistence | RDB snapshots + AOF log | None — memory only |
| Replication | Leader-follower, Sentinel, Cluster | Third-party only |
| Clustering | Built-in (Redis Cluster) | Client-side (ketama) |
| Pub/Sub | Yes | No |
| Scripting | Lua scripts, Redis Functions | No |
| Transactions | MULTI/EXEC (optimistic) | No |
| Memory efficiency | Slightly higher overhead per key | Slightly more efficient for pure strings |
| Multi-threading | Single-threaded command processing (I/O threads in v6+) | Multi-threaded |
| Use cases | Cache + session + leaderboard + queue + pub/sub + rate limiting | Pure high-throughput cache |

**Choose Redis** if you need: rich data structures, persistence, pub/sub, distributed locks, sorted sets for leaderboards, or rate limiting.

**Choose Memcached** if you need: maximum throughput for simple string key-value caching, and nothing else.

In practice, Redis has become the default choice for nearly all new projects.

---

## Cache-Aside (Lazy Loading)

The application manages the cache explicitly. The database is the source of truth.

```
Read:
1. Check cache for key
2. Cache hit → return value
3. Cache miss → query database → store in cache → return value

Write:
1. Write to database
2. Invalidate (delete) or update the cache key
```

**Pros:**
- Only requested data is cached (no wasted memory)
- Cache can tolerate failures — falls back to database
- Data model in cache can differ from database (flexible)

**Cons:**
- First request after cache miss is slow (cache cold start)
- Risk of stale data if invalidation is missed
- Cache stampede: many requests miss simultaneously and all hit the database

**Best for:** General-purpose read caching, most web application data.

---

## Write-Through Cache

Every write goes to the cache and database simultaneously. The cache is always up to date.

```
Write:
1. Write to cache
2. Write to database (synchronously)

Read:
1. Check cache (always up to date)
2. Cache miss is rare (only on first access or eviction)
```

**Pros:**
- Cache always consistent with database
- No stale reads

**Cons:**
- Every write has latency of both cache and database
- Cache fills with data that may never be read (write-heavy, read-cold data)
- Cache is required for writes — failure means write fails

**Best for:** Data that is read frequently after every write (user profiles, settings).

---

## Write-Behind (Write-Back) Cache

Writes go to the cache first. The cache asynchronously flushes to the database later.

```
Write:
1. Write to cache
2. Return success immediately
3. Cache flushes to database asynchronously (batched)

Read:
1. Read from cache (always fresh for recent writes)
```

**Pros:**
- Lowest write latency (only cache write is synchronous)
- Batched database writes improve throughput

**Cons:**
- Risk of data loss if cache crashes before flush
- Eventual consistency between cache and database
- Complex failure handling

**Best for:** High write throughput, loss-tolerant data (counters, analytics, game state).

---

## Read-Through Cache

The cache itself is responsible for loading data from the database on a miss. The application only talks to the cache.

```
Read:
1. Application requests key from cache
2. Cache hit → return
3. Cache miss → cache loads from database internally → return to application
```

**Pros:**
- Application code is simplified (no manual cache management)
- Consistent loading logic centralized in cache layer

**Cons:**
- First access is always slow (cold start)
- Less flexible than cache-aside (loading logic fixed)

**Used in**: Managed caches with auto-population plugins.

---

## Cache Eviction Policies

When the cache is full, old entries must be removed to make room for new ones.

| Policy | Description | Best For |
|--------|-------------|---------|
| **LRU** (Least Recently Used) | Evict the key not accessed for the longest time | General purpose, recency matters |
| **LFU** (Least Frequently Used) | Evict the key with fewest accesses | Popular content stays cached longer |
| **TTL** (Time-To-Live) | Keys expire automatically after a set duration | Data with known staleness window |
| **Random** | Evict a random key | Low overhead, unpredictable |
| **FIFO** | Evict oldest inserted key | Rarely useful |
| **allkeys-lru** | LRU across all keys, not just those with TTL (Redis) | General cache without explicit TTLs |

**Recommended default**: LRU with TTLs on sensitive or frequently changing data.

---

## Cache Stampede (Thundering Herd)

When a cache key expires, many concurrent requests simultaneously miss and all query the database for the same data.

```
10,000 req/sec to product page
Cache key expires at t=100ms
All 10,000 requests simultaneously hit the database
Database is overwhelmed → cascading failure
```

### Solutions

**1. Mutex Lock / Probabilistic Early Expiration**
Only one request rebuilds the cache; others wait or serve slightly stale data.

```
if cache_miss:
    if acquire_lock(key):
        value = db.query()
        cache.set(key, value, ttl)
        release_lock(key)
    else:
        wait and retry (or serve stale)
```

**2. Background Refresh**
Refresh the cache before it expires (at e.g. 80% of TTL remaining). No requests ever see a miss.

**3. Jitter on TTL**
Add random offset to TTL so keys do not expire simultaneously.

```
ttl = base_ttl + random(0, base_ttl * 0.2)
```

**4. XFetch (Probabilistic Early Expiration)**
Each request has a probability of refreshing the cache before it expires, proportional to how close to expiry it is. Spreads refresh load.

---

## Cache Invalidation

"There are only two hard things in Computer Science: cache invalidation and naming things." — Phil Karlton

### Strategies

| Strategy | How It Works | Risk |
|----------|-------------|------|
| **TTL expiry** | Keys auto-expire after N seconds | Stale window between update and expiry |
| **Event-driven invalidation** | Application deletes/updates cache on every database write | Cache and DB writes must be atomic |
| **Write-through** | Cache is updated alongside DB write | Tight coupling between cache and write path |
| **Versioned keys** | `user:123:v5` — bump version on change | Old versions remain in cache until evicted |
| **Cache tags / groups** | Invalidate all keys tagged with a group (e.g., `product:456`) | Tag index adds overhead |

### The Cache Invalidation Race Condition

```
T1: Read DB → 5          T2: Write DB → 6
T1: Cache miss           T2: Delete cache key
                         T2: Write DB complete
T1: Reads DB → 5         
T1: Sets cache → 5    ← stale value written AFTER invalidation!
```

**Solution**: Use optimistic locking or CAS (Compare-And-Set) when updating the cache after a database read.

---

## Distributed Cache Architecture

### Standalone Redis

Single Redis instance. Simple, fast, but single point of failure.

### Redis Sentinel

```
          ┌─────────────┐
          │  Sentinel 1  │
          └─────────────┘
               │
       ┌───────┼───────┐
       │       │       │
  ┌────┴─┐ ┌──┴───┐ ┌──┴───┐
  │Master│ │Slave1│ │Slave2│
  └──────┘ └──────┘ └──────┘
```

Sentinel monitors the master. On failure, Sentinel promotes a replica to master and reconfigures clients. Provides HA but not horizontal scaling.

### Redis Cluster

Automatic sharding across multiple nodes. Data is partitioned using consistent hashing into 16,384 hash slots.

```
Nodes: 3 primaries + 3 replicas
Slots: 0–5460 → Node 1
       5461–10922 → Node 2
       10923–16383 → Node 3
```

Each primary has a replica for HA. Supports up to 1000 nodes. Multi-key operations only work if all keys map to the same slot (use hash tags `{user:123}` to colocate).

---

## Redis Data Structures and Use Cases

| Structure | Use Case | Example |
|-----------|----------|---------|
| **String** | Cache any value, counters | `SET user:123:profile <json>` |
| **Hash** | Object with fields | `HSET user:123 name "Alice" age 30` |
| **List** | Queue, recent history | `LPUSH feed:123 post_id` |
| **Set** | Unique items, membership | `SADD online_users user:123` |
| **Sorted Set** | Leaderboards, range queries | `ZADD leaderboard 1500 user:123` |
| **Stream** | Append-only log, event bus | `XADD events * type order_placed` |
| **HyperLogLog** | Approximate cardinality | `PFADD visitors ip:1.2.3.4` |
| **Bitmap** | User activity tracking | `SETBIT activity:20260101 user_id 1` |

---

## Distributed Locks with Redis

Redis is commonly used for distributed locking (preventing concurrent execution of the same code path).

### Simple Lock (SET NX EX)

```
SET lock:resource_id <unique_token> NX EX 30
```

- `NX`: only set if key does not exist
- `EX 30`: auto-expire in 30 seconds (prevents deadlock if holder crashes)
- Value is a unique token to prevent lock release by a different client

**Release:**
```lua
-- Lua script for atomic check-and-delete
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("del", KEYS[1])
else
    return 0
end
```

### Redlock (Multi-Node Lock)

For higher reliability, acquire the lock on N/2+1 Redis nodes. Lock is valid only if a majority grant it within a time window. More complex — use a library (Redlock.js, Redisson).

---

## Session Caching

Redis is the standard session store for horizontally scaled applications:

```
User login → Generate session token → Store in Redis with TTL
Subsequent requests → Extract token → Redis lookup → User context
```

**Configuration tips:**
- Set TTL to match session expiry policy
- Use `EXPIRE` refresh on activity for sliding sessions
- Prefix keys: `session:<token>` for easy inspection/debugging

---

## Rate Limiting with Redis

```lua
-- Fixed window rate limit
local key = "ratelimit:" .. user_id .. ":" .. window
local count = redis.call("INCR", key)
if count == 1 then
    redis.call("EXPIRE", key, window_size_seconds)
end
return count <= limit
```

Sliding window, token bucket, and leaky bucket patterns are also implementable with Sorted Sets.

---

## Production Checklist

- [ ] Cache-aside implemented with explicit invalidation on writes
- [ ] TTLs set on all keys — no infinite-lived cache entries
- [ ] Jitter added to TTLs to prevent stampede
- [ ] Cache stampede protection (mutex or probabilistic expiry) on high-traffic keys
- [ ] Redis Cluster or Sentinel deployed (no standalone in production)
- [ ] Memory maxmemory-policy configured (`allkeys-lru` for pure cache)
- [ ] Keyspace notifications or monitoring for unexpected eviction rate
- [ ] Alert on cache hit rate dropping below threshold (e.g., <80%)
- [ ] Persistence configured appropriately (RDB for snapshots, AOF for durability)
- [ ] Secrets / sensitive data encrypted or not cached at all
- [ ] Connection pooling configured in application (not one connection per request)

---

## Next Steps

- [Kafka](./messaging/kafka.md) — Redis Streams vs Kafka for lightweight event queuing
- [Database Sharding](./03-database-sharding.md) — cache as the layer in front of shards
- [Design Patterns](./19-design-patterns.md) — CQRS read model often backed by a cache
