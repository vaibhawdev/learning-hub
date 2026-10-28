# Core Distributed Systems

> **Phase:** 4 — Distributed Systems  
> **Status:** Not Started

## Overview

Distributed systems are collections of independent computers that appear to users as a single coherent system — and the gap between "appear to" and the underlying reality is where most complexity lives. This section covers the fundamental theorems, algorithms, and patterns that govern distributed system design and reliability.

## Topics

### CAP Theorem

**What it is:** A theorem stating that a distributed system can guarantee at most two of three properties: Consistency (all nodes see the same data), Availability (every request gets a response), and Partition tolerance (system operates despite network splits).

**Why it matters:** CAP theorem frames the fundamental tradeoff every distributed database makes — understanding it explains why Cosmos DB, Cassandra, and Zookeeper make different choices.

**Key concepts to understand:**
- Partition tolerance is not optional in real networks: network splits happen, so the real choice is C vs A
- CP systems (Zookeeper, HBase, etcd): sacrifice availability during partitions to maintain consistency
- AP systems (Cassandra, DynamoDB, CouchDB): sacrifice consistency during partitions to remain available
- PACELC: extends CAP — even without partitions, systems tradeoff between latency and consistency
- "Consistency" in CAP means linearizability — not the same as ACID consistency
- Why most systems choose AP: in practice, partitions are brief and eventual consistency is acceptable

**Resources to explore:**
- [ ] (to be filled)

---

### Consistency Models

**What it is:** A spectrum of guarantees about the ordering and visibility of reads and writes in a distributed system, from strong linearizability to weak eventual consistency.

**Why it matters:** Choosing the right consistency model is a correctness decision — too weak and users see stale or conflicting data; too strong and latency and availability suffer.

**Key concepts to understand:**
- Linearizability (strong consistency): operations appear to happen instantaneously at a single point in time
- Sequential consistency: all processes see operations in the same order, but not necessarily real-time
- Causal consistency: causally related operations are seen in causal order by all nodes
- Eventual consistency: given no new writes, all replicas will converge to the same value
- Read-your-writes: a client always sees its own writes
- Monotonic reads: if you read a value, you never see an older value in subsequent reads

**Resources to explore:**
- [ ] (to be filled)

---

### Consensus Algorithms

**What it is:** Distributed algorithms that allow a group of processes to agree on a single value despite failures — foundational to leader election, log replication, and distributed locking.

**Why it matters:** Consensus is what makes distributed databases reliable — without it, no two nodes could agree on committed data, and failures would cause split-brain scenarios.

**Key concepts to understand:**
- FLP impossibility: no deterministic algorithm can achieve consensus in an asynchronous system with even one crash failure — explains why real algorithms use timeouts
- Requirements: agreement (all decide same value), validity (decided value was proposed), termination (all decide eventually)
- Failure models: crash-stop, crash-recovery, Byzantine — most systems assume crash-stop
- Real-world consensus users: etcd, ZooKeeper, CockroachDB, Kafka (KRaft), Spanner
- Two-phase commit (2PC): not consensus but related — coordinator failure leaves participants blocked
- Safety vs liveness: safety = nothing bad happens, liveness = something good eventually happens

**Resources to explore:**
- [ ] (to be filled)

---

### Raft

**What it is:** A consensus algorithm designed for understandability, using leader election, log replication, and safety invariants to maintain a replicated state machine across a cluster.

**Why it matters:** Raft is the most widely implemented consensus algorithm today (etcd, CockroachDB, TiKV, Consul) — understanding it demystifies how distributed databases maintain consistency.

**Key concepts to understand:**
- Roles: leader (handles all writes), followers (replicate from leader), candidates (during election)
- Leader election: randomized election timeouts trigger candidacy; majority vote required to become leader
- Log replication: leader appends entry to log, sends AppendEntries to followers, commits when majority acknowledges
- Safety invariant: a leader has all committed entries — ensured by election restriction
- Term numbers: logical clock that prevents stale leaders from causing split-brain
- Log compaction (snapshots): periodic replacement of log prefix with a snapshot to bound log size

**Resources to explore:**
- [ ] (to be filled)

---

### Paxos Basics

**What it is:** The original consensus algorithm by Leslie Lamport — mathematically rigorous but notoriously difficult to understand and implement correctly, inspiring Raft's design.

**Why it matters:** Paxos underlies Google's Chubby and Spanner — understanding it provides historical context and insight into why Raft was created as a more understandable alternative.

**Key concepts to understand:**
- Two phases: Prepare/Promise (phase 1) and Accept/Accepted (phase 2)
- Quorum: majority of nodes must respond for progress — tolerates (n-1)/2 failures
- Ballot numbers: proposers use increasing ballot numbers to supersede stale proposals
- Multi-Paxos: optimization where a stable leader skips phase 1 for subsequent proposals
- Livelock in basic Paxos: two proposers can continuously interrupt each other — leader helps avoid
- Differences from Raft: Paxos allows any node to propose (no strict leader), harder to implement

**Resources to explore:**
- [ ] (to be filled)

---

### Leader Election

**What it is:** The process by which distributed nodes agree on a single coordinator (leader) responsible for sequencing operations, with automatic re-election on leader failure.

**Why it matters:** Leader election is a core component of Kafka, Redis Sentinel, Zookeeper, and any system needing a single authoritative coordinator — getting it wrong causes split-brain.

**Key concepts to understand:**
- Bully algorithm: highest-ID node wins — simple but high message complexity
- Ring-based election: token passed around a ring until highest-ID is determined
- ZooKeeper-based election: ephemeral sequential nodes — lowest sequence number wins
- etcd-based election: lease/heartbeat pattern using distributed lock
- Split-brain: two nodes both believe they are leader — data divergence results
- Fencing tokens: monotonically increasing tokens issued by the leader that prevent stale leaders from acting

**Resources to explore:**
- [ ] (to be filled)

---

### Distributed Locking

**What it is:** A mechanism to ensure mutual exclusion across multiple processes on different machines — typically implemented using a strongly consistent key-value store (etcd, ZooKeeper, Redis Redlock).

**Why it matters:** Distributed locks are used for leader election, resource reservation, and preventing double-processing of events — incorrectly implemented, they provide false safety.

**Key concepts to understand:**
- Properties: mutual exclusion, deadlock freedom (lock released on failure), fault tolerance
- etcd-based locks: lease with TTL, watch for lock release, fencing tokens
- Redis Redlock: acquire lock on majority of Redis nodes — controversial correctness claims (Martin Kleppmann critique)
- Why distributed locks are hard: network partitions can cause a lock holder to believe it still holds the lock
- Fencing tokens: storage service rejects writes with tokens older than the last seen token
- When not to use distributed locks: prefer idempotent operations or optimistic concurrency instead

**Resources to explore:**
- [ ] (to be filled)

---

### Idempotency

**What it is:** The property of an operation that produces the same result whether executed once or multiple times — essential for safe retries in distributed systems.

**Why it matters:** In distributed systems, "at-least-once" delivery is the default — without idempotent operations, retried messages cause duplicate processing, duplicate charges, or data corruption.

**Key concepts to understand:**
- Idempotency keys: client-provided unique identifier that the server uses to deduplicate
- Idempotency store: persist processed idempotency keys (Redis, database) to detect duplicates
- Natural idempotency: upserts, conditional writes (`IF NOT EXISTS`), set operations
- Non-idempotent operations: increments, appends, emails — require explicit deduplication
- HTTP idempotency: GET, PUT, DELETE are idempotent by definition; POST is not
- Idempotent consumer pattern: message handler checks for duplicate message ID before processing

**Resources to explore:**
- [ ] (to be filled)

---

### Retry Strategies

**What it is:** Policies for automatically retrying failed operations — balancing success probability against resource consumption and downstream load.

**Why it matters:** Retries are essential for resilience against transient failures, but naive retries during an outage can amplify load and delay recovery — the strategy matters.

**Key concepts to understand:**
- Immediate retry: only useful for non-deterministic failures (random packet drop)
- Fixed delay: introduces wait between retries, reduces immediate re-hammering
- Exponential backoff: doubles delay with each retry — standard pattern for transient failures
- Jitter: randomized delay to prevent retry storms (multiple clients retrying in sync)
- Maximum retry count: prevent infinite retries that block threads
- Circuit breaker integration: stop retrying when the circuit is open

**Resources to explore:**
- [ ] (to be filled)

---

### Exponential Backoff

**What it is:** A retry delay strategy where the wait time grows exponentially (1s, 2s, 4s, 8s...) with each failed attempt, typically combined with jitter and a maximum cap.

**Why it matters:** Exponential backoff prevents retry storms during outages by spreading retry load over time — jitter prevents synchronized retries from many clients.

**Key concepts to understand:**
- Base formula: `min(cap, base × 2^attempt)`
- Full jitter: `random(0, min(cap, base × 2^attempt))` — best for avoiding synchronized retries
- Equal jitter: `cap/2 + random(0, cap/2)` — some minimum guaranteed wait
- AWS retry documentation as the canonical reference for backoff + jitter patterns
- Polly library in .NET: `WaitAndRetryAsync` with exponential backoff and jitter
- Context propagation: don't retry if the cancellation token has expired (client has given up)

**Resources to explore:**
- [ ] (to be filled)

---

### Circuit Breakers

**What it is:** A stability pattern that monitors failure rates to a dependency and temporarily stops sending requests when failures exceed a threshold — preventing cascading failures.

**Why it matters:** Without circuit breakers, failures in one service cascade into thread pool exhaustion and timeouts throughout the call chain — circuit breakers contain failures at the source.

**Key concepts to understand:**
- Three states: Closed (normal), Open (blocking requests), Half-Open (testing recovery)
- Failure threshold: percentage or count of failures in a sliding window that opens the circuit
- Recovery timeout: how long the circuit stays open before trying again (half-open probe)
- Bulkhead isolation: combining circuit breakers with thread pool bulkheads prevents cross-contamination
- Polly in .NET: `CircuitBreakerAsync(exceptionsAllowedBeforeBreaking, durationOfBreak)`
- Half-open probe: single request allowed through; success closes circuit, failure extends open period

**Resources to explore:**
- [ ] (to be filled)

---

### Bulkheads

**What it is:** A pattern that isolates different parts of a system into separate resource pools (thread pools, connection pools, semaphores) so that failures in one area don't exhaust resources needed by others.

**Why it matters:** Bulkheads prevent a single slow dependency from consuming all available threads/connections and bringing down unrelated functionality — critical for multi-tenant or multi-upstream services.

**Key concepts to understand:**
- Named bulkheads: separate `SemaphoreSlim` or thread pool per dependency
- Bulkhead + timeout: limit both concurrency and wait time for each dependency
- Thread pool isolation: separate thread pools per dependency (heavyweight but strongest isolation)
- Semaphore isolation: same thread pool but limited concurrency per dependency (lighter weight)
- Polly `BulkheadAsync(maxParallelization, maxQueuingActions)`
- Metrics: track bulkhead rejection rate as a leading indicator of downstream issues

**Resources to explore:**
- [ ] (to be filled)

---

### Sagas

**What it is:** A pattern for managing distributed transactions as a sequence of local transactions with compensating actions that undo partial work on failure — an alternative to 2PC.

**Why it matters:** Distributed transactions (2PC) are fragile and lock resources across services — sagas enable long-running multi-service workflows without holding distributed locks.

**Key concepts to understand:**
- Choreography-based saga: services emit and react to events — decentralized, harder to trace
- Orchestration-based saga: a central saga orchestrator coordinates steps and compensations
- Compensating transactions: the inverse operation of each step — must be idempotent and eventually succeed
- Rollback complexity: compensations may fail — design for eventual consistency with notification
- Saga state machine: explicit states and transitions for tracking saga progress
- NServiceBus and MassTransit Saga support for .NET

**Resources to explore:**
- [ ] (to be filled)

---

### CQRS

**What it is:** Command Query Responsibility Segregation — separating the write model (commands that change state) from the read model (queries that return data), potentially using different storage and scaling each independently.

**Why it matters:** CQRS solves the impedance mismatch between write-optimized normalized schemas and read-optimized denormalized views — enabling each to be scaled and optimized independently.

**Key concepts to understand:**
- Command: an intent to change state — validated, processed by domain logic, produces events
- Query: a request for data — should not change state, can use optimized read models
- Separate models: commands update aggregate state; events update read model projections
- Eventual consistency in CQRS: read models lag behind write model — applications must handle this
- Simple CQRS without event sourcing: just separate read/write handlers, same database
- Complex CQRS with ES: events as source of truth, read models as projections

**Resources to explore:**
- [ ] (to be filled)

---

### Event Sourcing

**What it is:** A persistence pattern where state changes are stored as an immutable sequence of events rather than updating current state — the current state is derived by replaying events.

**Why it matters:** Event sourcing provides a complete audit log, enables temporal queries (state at any point in time), and makes event-driven integration trivial — at the cost of query complexity.

**Key concepts to understand:**
- Event store: append-only log of domain events (EventStoreDB, Cosmos DB, PostgreSQL + streaming)
- Aggregate: the consistency boundary; events are applied to derive current state
- Snapshots: periodic materialized state to avoid replaying the full event stream
- Event versioning: how to evolve event schemas without breaking existing data
- Eventual consistency: projections update asynchronously after events are stored
- Tradeoffs: querying is complex (requires projections), debugging is excellent (full history)

**Resources to explore:**
- [ ] (to be filled)

---

### Backpressure

**What it is:** A mechanism where a downstream consumer signals to upstream producers to slow down when the consumer cannot keep up — preventing buffer exhaustion and system collapse.

**Why it matters:** Without backpressure, producers that outpace consumers cause memory exhaustion, cascading delays, and eventually service failure — Reactive Streams and System.IO.Pipelines implement backpressure.

**Key concepts to understand:**
- Push vs pull model: push systems have no natural backpressure; pull systems naturally throttle
- Reactive Streams: `IPublisher`, `ISubscriber`, `ISubscription` — the standard backpressure API
- `System.IO.Pipelines` pause/resume: `PauseWriterThreshold` stops producer until consumer catches up
- `System.Threading.Channels` bounded channel: `WaitToWriteAsync` blocks producer when channel is full
- TCP flow control: window size as the original network-level backpressure mechanism
- Detecting backpressure pressure: queue depth metrics, write wait times

**Resources to explore:**
- [ ] (to be filled)

---

### Stream Processing

**What it is:** Continuous processing of unbounded data streams — applying transformations, aggregations, and joins to data as it arrives rather than in batches.

**Why it matters:** Stream processing enables real-time analytics, fraud detection, recommendation updates, and operational monitoring with sub-second latency instead of batch-hours.

**Key concepts to understand:**
- Event time vs processing time: when did the event happen vs when did we receive it
- Watermarks: estimates of event time progress — used to close windows for late events
- Windowing: tumbling (fixed, non-overlapping), sliding (overlapping), session (activity-based) windows
- State in stream processing: managing stateful aggregations across events (Kafka Streams state stores, Flink state backends)
- Exactly-once semantics in streams: Kafka transactions, Flink checkpointing
- Lambda architecture: combining speed layer (streaming) with batch layer (historical)

**Resources to explore:**
- [ ] (to be filled)

---

### Exactly-Once Delivery Myths

**What it is:** The often misunderstood claim that a messaging system can deliver each message exactly once — in practice, "exactly-once processing" requires both the messaging system and the consumer to cooperate.

**Why it matters:** Believing in automatic exactly-once delivery leads to non-idempotent consumers that corrupt data when they receive a duplicate message — understanding the reality drives correct design.

**Key concepts to understand:**
- At-most-once: fire and forget — messages may be lost, never duplicated
- At-least-once: retry until acknowledged — messages may be duplicated, never lost
- Effectively-once: at-least-once delivery + idempotent consumer = the practical "exactly-once"
- Kafka transactions: producer transactions + consumer read_committed isolation achieve exactly-once between Kafka topics
- Outbox pattern: achieve exactly-once effect across database write + message publish
- Why "exactly-once" is impossible in a fully general distributed setting (two generals problem)

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Messaging Systems](./02-messaging-systems.md)
- [High-Scale Architecture](./03-high-scale-architecture.md)
- [Core Distributed Systems Performance](../Phase-08-Performance-Engineering/03-distributed-performance.md)
