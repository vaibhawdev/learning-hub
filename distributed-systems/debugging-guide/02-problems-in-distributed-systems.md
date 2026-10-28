# Problems We Face in Distributed Systems

A catalogue of what goes wrong, why it goes wrong, how it shows up, and the standard mitigation. Each entry is: **What** / **Why it happens** / **How it shows up** / **Mitigations**.

Related: [debugging process](./01-debugging-process.md), [terminology](./03-metrics-and-terminology.md), [CAP & consistency](../02-core-concepts-cap-theorem.md), [design patterns](../19-design-patterns.md).

---

## The Root Causes (Why Everything Below Exists)

Almost every problem is a consequence of three facts:

1. **The network is unreliable.** Messages can be lost, delayed, duplicated, reordered.
2. **There is no shared clock and no shared memory.** Nodes cannot instantly agree on "now" or on "the current state".
3. **Parts fail independently, and you often cannot tell failed from slow.**

These are summarized in the classic **Fallacies of Distributed Computing** (Peter Deutsch et al.): the network is reliable; latency is zero; bandwidth is infinite; the network is secure; topology doesn't change; there is one administrator; transport cost is zero; the network is homogeneous.

---

## Index

1. [Network problems](#1-network-problems)
2. [Partial failure & failure detection](#2-partial-failure--failure-detection)
3. [Time and ordering](#3-time-and-ordering)
4. [Consistency and replication](#4-consistency-and-replication)
5. [Consensus and coordination](#5-consensus-and-coordination)
6. [Transactions and data integrity](#6-transactions-and-data-integrity)
7. [Messaging and delivery](#7-messaging-and-delivery)
8. [Latency and performance](#8-latency-and-performance)
9. [Overload and cascading failures](#9-overload-and-cascading-failures)
10. [Data distribution and scaling](#10-data-distribution-and-scaling)
11. [Caching](#11-caching)
12. [Deployment and configuration](#12-deployment-and-configuration)
13. [Observability and debugging](#13-observability-and-debugging)
14. [Security](#14-security)
15. [Operational and organizational](#15-operational-and-organizational)
16. [Summary table](#16-summary-table)

---

## 1. Network problems

### 1.1 Packet loss, delay, duplication, reordering
- **Why:** congestion, faulty hardware, routing changes, retransmission.
- **Shows up as:** random timeouts, duplicated requests, out-of-order events.
- **Mitigate:** timeouts everywhere; idempotent operations; sequence numbers; don't assume ordering unless the protocol guarantees it.

### 1.2 Network partition
- **What:** the network splits; each side is alive but can't talk to the other.
- **Why:** switch/router failure, misconfigured firewall, cross-region link cut, overloaded link.
- **Shows up as:** two leaders (**split brain**), diverging data, one side rejecting writes.
- **Mitigate:** quorum-based decisions (majority side continues); fencing tokens; choose CP or AP per feature deliberately (see CAP); reconciliation strategy (CRDTs, last-writer-wins with caveats, manual merge).

### 1.3 DNS problems
- **Why:** TTL caching, stale records, resolver overload, search-domain amplification (`ndots`), negative caching.
- **Shows up as:** intermittent connection failures after a deploy/failover; some clients hit the old IP; slow first request.
- **Mitigate:** sane TTLs; client-side re-resolution; connection retry; monitor DNS latency; node-local DNS cache in Kubernetes.

### 1.4 Connection-level problems
- **Examples:** idle timeout of LB shorter than client keep-alive (random resets), ephemeral port exhaustion, conntrack table full, TLS handshake overhead, half-open connections, MTU mismatch (large payloads hang while small ones work).
- **Mitigate:** align keep-alive/idle timeouts (client < server < LB); connection pooling; monitor `TIME_WAIT`/conntrack; HTTP/2 multiplexing.

### 1.5 Bandwidth and egress limits
- **Shows up as:** replication lag, slow bulk jobs, cross-AZ cost surprises, throttled uploads.
- **Mitigate:** compress; batch; keep chatty services co-located; measure cross-zone traffic.

---

## 2. Partial failure & failure detection

### 2.1 Partial failure
- **What:** some components fail while others continue. A monolith mostly crashes or works; distributed systems are often **sort-of working**.
- **Shows up as:** a request touches 5 services; 4 succeed, 1 fails: now state is inconsistent.
- **Mitigate:** design every call as "may fail"; compensating actions (saga); graceful degradation; bulkheads.

### 2.2 You can't distinguish "slow" from "dead"
- **What:** a timeout tells you only "no answer yet". The remote operation **may or may not have executed**.
- **Consequence:** the **two generals problem**: no protocol over an unreliable channel can give certainty. A retry might double-execute; not retrying might lose the operation.
- **Mitigate:** idempotency keys; make operations safely repeatable; query-before-retry ("did my payment go through?"); at-least-once + dedupe.

### 2.3 Failure detector inaccuracy
- **What:** heartbeats and health checks give false positives (healthy node declared dead under GC pause or load) and false negatives (dead node still "healthy" because the check is shallow).
- **Mitigate:** phi-accrual detectors; multiple missed heartbeats before eviction; deep health checks distinguished from liveness; separate liveness vs. readiness.

### 2.4 Zombie / stale leader
- **What:** a node that was paused (GC, VM freeze) wakes up still believing it is the leader or holds a lock, while a new leader exists.
- **Shows up as:** double writes, corrupted state.
- **Mitigate:** **fencing tokens** (monotonic number checked by the resource), leases with safety margin, never trust a lock without checking it at the resource.

### 2.5 Gray failure
- **What:** a component is degraded, not down: 10% packet loss, slow disk, one thread pool stuck. Health checks pass; users suffer.
- **Mitigate:** measure from the *client's* perspective (synthetic probes, success-rate per instance); outlier ejection in load balancers/mesh.

### 2.6 Single points of failure (SPOF)
- **Examples:** one DB primary, one LB, one DNS provider, one region, one config service, one cert authority, one person who knows the system.
- **Mitigate:** redundancy at every tier; test the failover; map dependencies including the "hidden" ones (auth, DNS, secrets).

---

## 3. Time and ordering

### 3.1 Clock skew and drift
- **What:** each machine's clock differs. NTP gets you milliseconds, not nanoseconds; VMs can jump.
- **Shows up as:** logs out of order across hosts; "last write wins" picks the wrong write; token/cert "not yet valid"; TTL expiring early/late; negative durations.
- **Mitigate:** NTP/chrony monitored; never use wall-clock to **order** events across machines; use logical clocks; use monotonic clocks for durations.

### 3.2 No global ordering
- **What:** "A happened before B" can't be determined from timestamps alone.
- **Tools:** **Lamport clocks** (total order consistent with causality), **vector clocks** (detect concurrency), **hybrid logical clocks (HLC)**, **TrueTime** (Google Spanner: bounded clock uncertainty).
- **Mitigate:** carry causality explicitly (versions, sequence numbers, trace parents); single-writer per entity (partition by key) to get ordering per entity.

### 3.3 Concurrent updates
- **What:** two clients update the same record at once.
- **Shows up as:** lost updates, write skew.
- **Mitigate:** optimistic locking (version column / ETag), compare-and-set, single-writer per key, CRDTs, serializable transactions where required.

---

## 4. Consistency and replication

### 4.1 Replication lag
- **What:** replicas apply writes after the primary.
- **Shows up as:** *read-your-writes* violations (user saves a profile, reloads, sees old data); *monotonic read* violations (data goes "back in time" when requests hit different replicas); reports that don't match.
- **Mitigate:** read from primary after write (sticky session / "read-your-writes" token); monitor lag; route lag-sensitive reads to primary; bounded staleness.

### 4.2 Stale reads
- **Cause:** replicas, caches, CDN, eventual consistency.
- **Mitigate:** define the staleness tolerance per use case; version/ETag checks; cache invalidation strategy (see §11).

### 4.3 Split brain
- **What:** two nodes both believe they are primary.
- **Result:** divergent writes that can't be merged cleanly.
- **Mitigate:** quorum/majority elections; STONITH/fencing; odd number of voters; consensus (Raft/Paxos) for leader election.

### 4.4 Conflicting writes (multi-leader / multi-region)
- **Mitigate:** conflict-free data types (CRDTs), application-level merge, per-entity home region, last-writer-wins only where loss is acceptable (it silently drops data).

### 4.5 CAP / PACELC trade-offs
- During a **P**artition you must choose **C**onsistency or **A**vailability. Else (normal operation) you trade **L**atency vs **C**onsistency. There is no "free" option; the problem is choosing per-operation, not per-system.

---

## 5. Consensus and coordination

### 5.1 Getting nodes to agree
- **What:** leader election, config agreement, distributed locks, membership.
- **Why hard:** **FLP impossibility**: in an asynchronous system, no deterministic consensus algorithm can guarantee termination if even one node can fail.
- **Practical answer:** Paxos/Raft/ZAB with timeouts (gives up strict liveness guarantees to remain safe).
- **Mitigate:** use battle-tested systems (etcd, ZooKeeper, Consul); never roll your own consensus.

### 5.2 Distributed locks
- **Problems:** lock holder pauses and the lock expires while it still thinks it holds it; Redis-based locks under failover; lock service as SPOF.
- **Mitigate:** fencing tokens; lease + renewal; prefer designs that avoid locks (partition by key, idempotency, optimistic concurrency).

### 5.3 Leader election instability ("flapping")
- **Shows up as:** frequent leader changes, unavailability windows, elections during GC or load spikes.
- **Mitigate:** tune election timeouts above p99 of network/GC pause; isolate consensus nodes from noisy workloads.

### 5.4 Membership and rebalancing storms
- **Shows up as:** Kafka consumer-group rebalance loops, cluster nodes joining/leaving rapidly, shard rebalancing causing load spikes.
- **Mitigate:** static membership, cooperative rebalancing, session timeout tuning, throttle data movement.

---

## 6. Transactions and data integrity

### 6.1 No cross-service ACID
- **What:** a business operation spans services/databases; you can't `BEGIN ... COMMIT` across them cheaply.
- **Options:** 2PC (blocking, coordinator is a SPOF), **Saga** (sequence of local transactions with compensations), **Outbox pattern**, **Transactional messaging**.
- See [17-distributed-transactions.md](../17-distributed-transactions.md).

### 6.2 Dual-write problem
- **What:** write to DB **and** publish to a broker; one succeeds and the other fails.
- **Result:** DB says order exists, no event emitted (or the reverse).
- **Mitigate:** **transactional outbox** + relay/CDC; never dual-write without one.

### 6.3 Idempotency failures
- **Shows up as:** double charges, duplicate emails, duplicate orders after a retry.
- **Mitigate:** idempotency keys stored with the result; natural keys + upserts; dedupe tables with TTL.

### 6.4 Saga compensation failures
- **Problem:** the compensating step also fails, or can't truly undo (email already sent).
- **Mitigate:** make compensations idempotent & retriable; design for "semantic" undo (refund, not delete); dead-letter and human review path.

### 6.5 Lost updates / write skew / phantom reads
- Isolation-level anomalies appear faster at scale. Know the isolation level you actually have (read committed ≠ serializable).

---

## 7. Messaging and delivery

### 7.1 Delivery semantics
| Guarantee | Meaning | Risk |
|-----------|---------|------|
| **At-most-once** | Fire and forget | Message loss |
| **At-least-once** | Retry until ack | Duplicates |
| **Exactly-once** | Effectively once | Expensive/limited; usually = at-least-once + idempotent consumer (or transactional processing inside one system) |

### 7.2 Out-of-order delivery
- **Mitigate:** partition by entity key (ordering within partition); sequence numbers / versions; consumers tolerate reordering (ignore stale versions).

### 7.3 Poison messages
- **What:** one message always fails and blocks the queue or loops forever.
- **Mitigate:** bounded retries, **dead-letter queue**, alerts on DLQ depth, replay tooling.

### 7.4 Consumer lag / backpressure
- **Shows up as:** growing queue, delayed processing, stale data downstream.
- **Mitigate:** scale consumers (up to partition count), batching, rate limits, backpressure signals, autoscale on lag.

### 7.5 Message schema evolution
- **Problem:** producer deploys a new format; old consumers break (or vice versa).
- **Mitigate:** backward/forward compatible schemas, schema registry, expand/contract migrations (see [24-data-serialization.md](../24-data-serialization.md)).

---

## 8. Latency and performance

### 8.1 Tail latency amplification
- **What:** if a request fans out to *N* services in parallel and waits for all, the request is slow if **any** one is slow.
- **Math:** with per-call p99 = 1% slow, a request calling 100 services is slow with probability `1 - 0.99^100 ≈ 63%`.
- **Mitigate:** hedged requests, timeouts, reduce fan-out, caching, tied requests, degrade gracefully (return partial results).

### 8.2 Latency adds up in chains
- Synchronous call depth *d* means total latency ≈ sum of each hop, and availability ≈ product of availabilities. Five services at 99.9% each give about 99.5% overall.
- **Mitigate:** shorten chains; make non-critical calls async; set per-hop timeout budgets that fit within the overall deadline.

### 8.3 Chatty communication / N+1 calls
- **Shows up as:** page load triggers hundreds of internal calls.
- **Mitigate:** batch APIs, GraphQL/BFF aggregation, data denormalization where appropriate.

### 8.4 Serialization and payload size
- **Mitigate:** efficient formats (Protobuf), compression, pagination, field selection.

### 8.5 Garbage collection and runtime pauses
- Stop-the-world pauses cause latency spikes **and** false failure detection. Mitigate: tune GC, smaller heaps, off-heap, watch GC logs.

### 8.6 Cold starts and warm-up
- New instance / JIT / empty cache / lazy connection pool gives slow first requests. Mitigate: warm-up probes, readiness only after warm, pre-warmed pools.

### 8.7 Noisy neighbours / resource contention
- Shared CPU, disk, network in VMs/containers. Mitigate: resource requests/limits, dedicated nodes for critical workloads, watch CPU throttling.

---

## 9. Overload and cascading failures

### 9.1 Cascading failure
- **What:** one component slows, callers pile up waiting, exhaust their threads/connections, and **they** become slow, spreading upstream.
- **Mitigate:** **timeouts**, **circuit breakers**, **bulkheads** (isolated pools per dependency), load shedding, queue limits.

### 9.2 Retry storm / retry amplification
- **What:** failures trigger retries, which add load to an already struggling service. With 3 retries at each of 3 layers, one user request becomes up to `4^3 = 64` calls to the bottom service.
- **Mitigate:** exponential backoff **with jitter**; **retry budgets** (e.g., retries ≤ 10% of requests); retry at one layer only; respect `Retry-After`; fail fast when the breaker is open.

### 9.3 Thundering herd
- **What:** many clients act at the same moment: cache expiry, service restart, cron at :00, reconnect after outage.
- **Mitigate:** jitter on TTLs, schedules, and reconnects; request coalescing (single-flight); staggered restarts.

### 9.4 Metastable failures
- **What:** the system enters a self-sustaining bad state *even after the trigger is removed* (e.g., retry load keeps it overloaded; cold caches keep the DB saturated).
- **Mitigate:** load shedding, admission control, circuit breakers, ability to drop traffic and ramp back gradually; avoid relying on a cache to survive normal load.

### 9.5 Resource exhaustion
- Threads, connection pools, file descriptors, memory, disk, inodes, ephemeral ports, rate-limit quotas. Always bound queues and pools; alert on **saturation**, not just usage.

### 9.6 Lack of backpressure
- Fast producer + slow consumer means unbounded memory/queue growth. Mitigate: bounded queues, blocking or rejecting when full, propagate pressure upstream (HTTP 429/503, reactive streams).

---

## 10. Data distribution and scaling

### 10.1 Hot partitions / hot keys
- **What:** skewed access; one shard gets most traffic (celebrity user, sequential IDs, time-based keys).
- **Shows up as:** one node at 100% while others idle; p99 high but averages fine.
- **Mitigate:** better partition key; hash-based sharding; key salting; caching hot keys; split hot shards.

### 10.2 Resharding / rebalancing
- Moving data live is risky and expensive. Mitigate: consistent hashing, virtual nodes, over-provision partitions up front, throttle migrations.

### 10.3 Cross-shard queries and joins
- Scatter-gather is slow and fails if any shard fails. Mitigate: design access patterns around the shard key; denormalize; CQRS read models.

### 10.4 Global unique IDs
- Auto-increment doesn't work across nodes. Use UUIDs/ULIDs, Snowflake-style IDs (time + node + sequence), or ID allocation services. Beware clock-dependence of Snowflake.

### 10.5 Data locality and cross-region
- Speed of light: ~70-150 ms between continents; cross-region synchronous calls are costly. Keep data near users; replicate asynchronously; define a home region per entity.

### 10.6 Scaling stateful services
- Stateless scales trivially; state does not. Externalize session state; use managed stores.

---

## 11. Caching

| Problem | What happens | Mitigation |
|---------|--------------|------------|
| **Stale data** | Cache outlives the truth | TTL, event-based invalidation, versioned keys |
| **Cache stampede** | Hot key expires; thousands hit the DB at once | Single-flight/locking, jittered TTL, stale-while-revalidate, refresh-ahead |
| **Cache penetration** | Requests for non-existent keys always miss | Cache negative results, Bloom filter |
| **Cache avalanche** | Many keys expire together | Randomize TTLs |
| **Inconsistent invalidation** | Race between DB write and cache delete/set | Delete-after-write, short TTL as backstop, CDC-driven invalidation |
| **Cold cache after restart** | DB overwhelmed | Warm up, gradual traffic ramp |
| **Hot key** | One cache node saturated | Local in-process cache layer, key replication |

> "There are only two hard things in computer science: cache invalidation and naming things."

---

## 12. Deployment and configuration

### 12.1 Version skew
- **What:** during rolling deploys, v1 and v2 run simultaneously and exchange messages.
- **Mitigate:** backward & forward compatibility; expand/contract DB migrations; feature flags; never ship a breaking schema + code change in one step.

### 12.2 Bad deploys / config pushes
- Most outages follow a change. Mitigate: canary, progressive delivery, auto-rollback on SLO breach, config validation, staged config rollout (config is code).

### 12.3 Configuration drift and inconsistency
- Instances with different configs, secrets, or feature-flag states. Mitigate: GitOps, immutable infra, config versioning surfaced in metrics/logs.

### 12.4 Dependency / startup ordering
- Services that can't start without a dependency create circular start-up deadlocks after a full outage. Mitigate: retry with backoff on startup, lazy connections, documented cold-start order, test "full cold boot" in DR drills.

### 12.5 Expiring things
- TLS certificates, JWT signing keys, API tokens, licences, DB passwords. Mitigate: automated rotation, alert at 30/14/7 days, inventory.

### 12.6 Hidden dependencies
- Shared DB between "independent" services, shared library versions, shared quota, shared DNS. Maintain a real dependency map.

---

## 13. Observability and debugging

### 13.1 No single view of a request
- **Mitigate:** distributed tracing (OpenTelemetry), correlation IDs on every log line and message.

### 13.2 Log overload and sampling blind spots
- Too much: cost, noise, slow search. Too little/sampled: the failing request wasn't kept. Mitigate: structured logs, dynamic log levels, tail-based sampling, error-biased retention.

### 13.3 Heisenbugs
- Bugs that vanish when observed (timing-dependent: race conditions, deadlocks). Mitigate: deterministic simulation testing, fault injection/chaos, Jepsen-style testing, record/replay.

### 13.4 Monitoring the monitor
- If the observability stack shares fate with production, you go blind exactly when you need it. Keep it independent; alert on missing data (dead-man's switch).

### 13.5 Alert fatigue
- Too many noisy alerts means real ones ignored. Alert on symptoms (SLO burn) not causes; every alert actionable.

See [16-observability.md](../16-observability.md), [21-distributed-logging.md](../21-distributed-logging.md).

---

## 14. Security

- **Larger attack surface:** every service-to-service call is a trust boundary. Zero-trust and mTLS between services.
- **Secrets sprawl:** use a secrets manager, short-lived credentials.
- **Confused deputy / token propagation:** pass end-user identity safely; scope tokens.
- **Lateral movement:** network policies, least privilege.
- **DDoS and abuse:** rate limiting at the edge, per-tenant quotas.
- **Audit and compliance:** data residency, immutable audit trails across services.

See [18-security.md](../18-security.md).

---

## 15. Operational and organizational

- **Conway's Law:** architecture mirrors team structure; unclear ownership means orphaned services and slow incident response.
- **Operational complexity:** more moving parts means more to monitor, patch, upgrade, secure, and pay for.
- **Testing is harder:** integration/E2E across services is slow and flaky; use contract tests, staging that resembles production, chaos experiments.
- **Cost:** cross-AZ traffic, observability storage, over-provisioning for redundancy.
- **Knowledge silos:** only one team understands a critical path. Mitigate: runbooks, game days, on-call rotation across teams.
- **Distributed monolith:** services so coupled they must deploy together. You get all the costs of distribution with none of the benefits.
- **Premature distribution:** many systems that adopt microservices early would have been healthier as a modular monolith.

---

## 16. Summary table

| Category | Core problem | Primary defences |
|----------|--------------|------------------|
| Network | Unreliable, partitions | Timeouts, retries, quorum, idempotency |
| Failure | Partial; slow vs dead | Health checks, fencing, redundancy, bulkheads |
| Time | No shared clock | Logical/hybrid clocks, monotonic timers |
| Consistency | Replication lag, split brain | Quorums, read-your-writes, consensus |
| Coordination | Agreement is hard | Raft/Paxos via etcd/ZooKeeper, avoid locks |
| Transactions | No global ACID | Saga, outbox, idempotency |
| Messaging | Duplicates, reorder, poison | At-least-once + idempotent, DLQ, partition keys |
| Latency | Tail amplification, chains | Timeouts, hedging, caching, async |
| Overload | Cascades, retry storms | Circuit breaker, backoff+jitter, load shedding |
| Data | Hot keys, resharding | Good shard key, consistent hashing |
| Caching | Staleness, stampede | TTL jitter, single-flight, invalidation design |
| Deploy | Version skew, bad config | Canary, compatibility, feature flags |
| Observability | Blind spots | Tracing, structured logs, SLO alerts |
| Security | Large attack surface | mTLS, least privilege, secret rotation |
| Org | Ownership, complexity | Clear ownership, runbooks, game days |

---

## Key Takeaways

1. **Assume everything fails**, in the worst possible way, at the worst possible time.
2. A timeout means "unknown", never "failed". Make operations **idempotent**.
3. **Never trust clocks** for ordering across machines.
4. Retries are load. Use **backoff + jitter + budgets**.
5. Every remote call needs a **timeout**, and most need a **circuit breaker**.
6. Pick **consistency per use case**; strong consistency costs latency and availability.
7. Most outages are caused by **changes**; make rollback fast and rehearsed.
8. You can't fix what you can't see. Invest in **tracing, metrics, structured logs** before you need them.
