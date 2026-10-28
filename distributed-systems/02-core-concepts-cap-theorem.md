# Core Concepts & CAP Theorem

## The CAP Theorem

The CAP theorem, proven by Eric Brewer in 2000, states that a distributed data store can guarantee at most **two** of the following three properties simultaneously:

```
         Consistency
              /\
             /  \
            /    \
           /      \
          /  PICK  \
         /    TWO   \
        ──────────────
  Availability    Partition
                  Tolerance
```

| Property | Definition |
|----------|-----------|
| **Consistency (C)** | Every read receives the most recent write or an error. All nodes see the same data at the same time. |
| **Availability (A)** | Every request receives a (non-error) response, even if it may not contain the most recent write. |
| **Partition Tolerance (P)** | The system continues to operate even when network messages between nodes are lost or delayed. |

### The Catch: P is Not Optional

In any real distributed system running on a network, **partition tolerance is mandatory**. Networks fail. You cannot build a system that stops when the network has a problem — you must choose how to behave. This means the real trade-off is:

**When a partition occurs, do you sacrifice Consistency or Availability?**

- **CP systems** (Consistency + Partition Tolerance): Return an error or wait until consistency is restored. HBase, Zookeeper, etcd, MongoDB (in strong consistency mode).
- **AP systems** (Availability + Partition Tolerance): Return a potentially stale response. Cassandra, CouchDB, DynamoDB.
- **CA systems**: Only possible without partitions — a single-node database. MySQL, PostgreSQL when not distributed.

### Real-World Implications

```
User places an order:
┌─────────────────────────────────────────────────────┐
│  CP choice: "Sorry, service temporarily unavailable" │
│  AP choice: "Order received" (may duplicate later)   │
└─────────────────────────────────────────────────────┘
```

For financial transactions, CP is usually required. For shopping cart or social media feeds, AP is acceptable.

---

## PACELC: The More Complete Model

CAP only describes behavior during partitions. PACELC extends it to cover the trade-off when the network is working normally:

**If there is a Partition (P), trade off Availability (A) vs Consistency (C);**
**Else (E), when the system is running normally, trade off Latency (L) vs Consistency (C).**

| System | Partition | Normal | Notes |
|--------|-----------|--------|-------|
| DynamoDB | PA | EL | Eventual consistency, low latency |
| Cassandra | PA | EL | Tunable consistency |
| MongoDB | PC | EC | Strong consistency by default |
| HBase | PC | EC | Strong consistency |
| PostgreSQL | PC | EC | Strong consistency, single node |
| Spanner | PC | EC | Globally consistent, higher latency |

---

## ACID vs BASE

### ACID (Traditional Databases)

| Property | Meaning |
|----------|---------|
| **Atomicity** | A transaction either completes fully or not at all. No partial updates. |
| **Consistency** | A transaction brings the database from one valid state to another. |
| **Isolation** | Concurrent transactions appear to execute serially. |
| **Durability** | Once committed, data survives crashes. |

ACID is easy to reason about but hard to achieve at scale across multiple nodes.

### BASE (Distributed Systems)

| Property | Meaning |
|----------|---------|
| **Basically Available** | The system guarantees availability (per CAP). |
| **Soft state** | State may change over time even without input (due to eventual consistency). |
| **Eventually consistent** | The system will become consistent over time, given no new updates. |

BASE trades strict correctness for availability and performance. Most distributed systems operate on BASE principles.

### Choosing Between Them

| Use ACID when | Use BASE when |
|---------------|---------------|
| Financial transactions | User activity feeds |
| Inventory management | Shopping cart (mergeable) |
| Order placement | Analytics and counters |
| Any operation where partial state is catastrophic | Read-heavy operations where stale data is acceptable |

---

## Consistency Models

Consistency exists on a spectrum. From strongest to weakest:

### 1. Linearizability (Strict Consistency)

Every operation appears to take effect instantaneously at a single point in time. Once a write is acknowledged, every subsequent read — from any node, anywhere — sees that write.

**Cost**: High latency (requires coordination between nodes), low availability under partition.

**Used in**: etcd, Zookeeper, Spanner.

### 2. Sequential Consistency

All operations appear in a consistent order to all nodes, but not necessarily in real-time order. A write does not need to be immediately visible, but all nodes must agree on the same sequence.

### 3. Causal Consistency

Causally related operations are seen in order by all nodes. If A causes B (A happened before B, and some node saw both), every node sees A before B. Unrelated operations may be seen in any order.

**Example**: If you post a comment and then delete it, no one should see the deletion before the comment.

### 4. Eventual Consistency

If no new updates are made, all replicas will eventually converge to the same value. No guarantee on when — could be milliseconds or seconds.

**Used in**: Cassandra, DynamoDB, DNS.

### 5. Read-Your-Writes Consistency

A user always sees their own writes, even if other users still see stale data. A common requirement for user-facing systems.

### 6. Monotonic Read Consistency

A user never sees older data after reading newer data. Once you read a value at time T, you will not read a value from before T.

---

## Failure Modes in Distributed Systems

### Types of Node Failures

| Failure Type | Description | Detectability |
|-------------|-------------|---------------|
| **Crash-stop** | Node stops, does not recover | Easy — no more messages |
| **Crash-recovery** | Node crashes but recovers with stable storage | Moderate — times out then returns |
| **Omission** | Node fails to send/receive some messages | Hard — indistinguishable from slow node |
| **Byzantine** | Node sends incorrect/malicious messages | Very hard — requires BFT protocols |

Most commercial distributed systems assume crash-stop or crash-recovery failures. Byzantine fault tolerance is used in blockchain/consensus systems.

### Network Failure Modes

- **Packet loss**: Messages are dropped silently
- **Partition**: Network is split; two groups cannot communicate
- **Asymmetric partition**: A can reach B, but B cannot reach A
- **Slow network**: Messages arrive eventually but with high latency
- **Message reordering**: Messages arrive out of order

### The Problem of Timeouts

When you send a request and get no response, you cannot distinguish:
1. The request was lost (safe to retry)
2. The request was processed and the response was lost (retrying would duplicate the action)
3. The server is slow but will eventually respond

This is why **idempotency keys** exist — allowing safe retries without double-processing.

---

## The Fallacies of Distributed Computing

These are common incorrect assumptions engineers make:

1. **The network is reliable** — It is not. Packets are dropped, routers crash.
2. **Latency is zero** — It is not. Cross-DC calls can take 100ms+.
3. **Bandwidth is infinite** — It is not. Saturating a link is common.
4. **The network is secure** — It is not. Assume a hostile network.
5. **Topology doesn't change** — It does. Nodes are added, removed, replaced.
6. **There is one administrator** — There are many, with different configs.
7. **Transport cost is zero** — Serialization, encryption, and routing all cost CPU.
8. **The network is homogeneous** — Different paths have different latency/bandwidth.

---

## Replication

Replication means keeping copies of data on multiple nodes for fault tolerance and performance.

### Leader-Follower (Primary-Replica)

```
Writes → Leader → Follower 1
                → Follower 2
Reads  → Any node (or leader only for strong consistency)
```

- Simple to reason about
- Leader is a single write bottleneck
- Used by: PostgreSQL streaming replication, MySQL replication, Redis Sentinel

### Multi-Leader (Active-Active)

```
Client A → Leader 1 ←→ Leader 2 ← Client B
```

- Multiple nodes accept writes
- Conflicts must be resolved (last-write-wins, CRDTs, custom logic)
- Used by: CouchDB, Cassandra (multi-DC), some MySQL configs

### Leaderless (Quorum)

```
Client → writes to W nodes out of N
Client → reads from R nodes out of N
(W + R > N guarantees reading latest write)
```

- No single leader failure
- W and R are tunable (consistency vs performance)
- Used by: Cassandra, DynamoDB, Riak

### Replication Lag

Even in synchronous replication, there is always some lag. This creates:
- **Read-your-writes violations**: You write to leader, read from follower before lag clears
- **Monotonic read violations**: Two reads from different replicas show different versions
- **Causality violations**: Events appear out of order

---

## Consensus Algorithms

When multiple nodes must agree on a single value (leader election, distributed locks, configuration), you need a consensus protocol.

### Raft

The most widely used modern consensus algorithm. Key properties:

- One **leader** handles all writes; **followers** replicate
- Leaders are elected via voting; a candidate needs a majority
- A log entry is committed once a majority acknowledges it
- Leader cannot be overridden — it always has the most up-to-date log

Used by: etcd (Kubernetes backbone), CockroachDB, TiKV, Consul.

### Paxos

The original consensus algorithm. Correct but notoriously difficult to understand and implement. Most real implementations use variants (Multi-Paxos, Fast Paxos).

### ZAB (Zookeeper Atomic Broadcast)

Used internally by Apache Zookeeper. Similar to Raft — leader-based, majority quorum.

### Two-Phase Commit (2PC)

Used for distributed transactions, not pure consensus:
1. **Prepare phase**: Coordinator asks all participants if they can commit
2. **Commit phase**: If all say yes, coordinator tells all to commit

Weakness: coordinator crash between phases leaves participants stuck. See [Distributed Transactions](./17-distributed-transactions.md).

---

## Time in Distributed Systems

Clocks across machines are not synchronized. This causes problems:

- **Clock skew**: Two machines show different times for "now"
- **Clock drift**: A clock runs slightly fast or slow over time
- NTP corrects drift but cannot eliminate skew below ~100ms

### Logical Clocks

Replace wall-clock time with logical ordering:

**Lamport Timestamps**: An integer that increments on each event. If event A causes event B, then timestamp(A) < timestamp(B). Does not track concurrent events.

**Vector Clocks**: A vector of integers, one per node. Tracks causality precisely — can determine if two events are concurrent or if one caused the other.

**Hybrid Logical Clocks (HLC)**: Combines wall-clock time with logical counters. Used in CockroachDB and Yugabyte.

---

## Key Takeaways

1. CAP forces a choice between consistency and availability during partitions — pick based on your use case, not defaults.
2. Most distributed databases are AP — plan for eventual consistency in your application code.
3. ACID transactions are not free in a distributed setting — they require coordination and add latency.
4. Timeouts are ambiguous — design all mutations to be idempotent.
5. Clocks are unreliable — use logical ordering for event sequencing, not wall-clock timestamps.

---

## Next Steps

- [Database Sharding](./03-database-sharding.md) — applying these concepts to data partitioning
- [Distributed Transactions](./17-distributed-transactions.md) — working around the limits of ACID in distributed systems
- [Design Patterns](./19-design-patterns.md) — patterns that manage consistency trade-offs
