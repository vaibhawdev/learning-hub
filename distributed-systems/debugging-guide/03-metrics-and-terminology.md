# Metrics and Terminology Glossary

Plain-language definitions of terms you will meet when measuring and debugging distributed systems, with worked examples. Related: [debugging process](./01-debugging-process.md), [problems catalogue](./02-problems-in-distributed-systems.md), [16-observability.md](../16-observability.md).

---

## 1. Latency Percentiles (p50, p90, p95, p99, p99.9)

### What a percentile is
Sort all request durations from fastest to slowest. The **pN** value is the duration below which **N%** of requests fall.

Example: 10 requests took (ms): `20, 22, 25, 25, 30, 32, 40, 55, 120, 900`

| Term | Meaning | Value here |
|------|---------|-----------|
| **p50 (median)** | Half of requests were faster than this | ~31 ms |
| **p90** | 90% were faster; the slowest 10% are above | ~120 ms |
| **p99** | 99% faster; the slowest 1% above | ~900 ms (with this tiny sample) |
| **Max** | Slowest observed | 900 ms |
| **Mean (average)** | Sum / count | 126.9 ms (misleading) |

### Why not just use the average?
The average (126.9 ms) describes **nobody**: most users saw ~30 ms and one saw 900 ms. Latency distributions are **long-tailed**, not bell-shaped, so averages hide the pain.

### Reading them together

| Pattern | Typical meaning |
|---------|-----------------|
| p50 low, p99 low | Healthy |
| p50 low, **p99 high** | Tail problem: GC pauses, lock contention, one slow node/shard, retries, cold cache, noisy neighbour |
| p50 high, p99 high (moving together) | Systemic slowness: saturation, slow dependency, DB, CPU |
| p50 ≈ p99 (suspiciously flat) | Maybe a fixed timeout or artificial limit is clipping the data |

### Why tail latency matters more at scale
- **Fan-out:** a request touching 100 services at p99 = 1% slow each is slow ~63% of the time (`1 - 0.99^100`).
- **Heavy users:** users make many requests per session, so almost every user eventually hits the p99.
- The slowest requests are often your **most valuable** (big carts, large accounts, complex queries).

### Practical rules
- **Percentiles cannot be averaged.** The average of two services' p99 is *not* the p99 of the combined traffic. Aggregate **histograms**, then compute the percentile.
- **Pick the percentile that matches the SLO.** Common: p50 (typical), p95/p99 (SLO), p99.9 (critical systems).
- **Sample size matters.** p99 of 50 requests is just the max. As a rule of thumb you want at least ~10 observations *above* the percentile, i.e. `10 / (1 - N/100)` samples: ~1,000 for p99, ~10,000 for p99.9, per time window.
- **Measure at the right place.** Server-side latency omits queueing before the app and network time. Prefer client-side/edge measurement for user experience.
- **Beware coordinated omission:** a load-test tool that waits for a slow response before sending the next request under-reports tail latency. Use open-loop load generators (e.g., wrk2, k6 arrival-rate executors).
- **Histograms vs summaries:** Prometheus *histograms* can be aggregated across instances; *summaries* cannot.

### Prometheus examples
```promql
# p99 latency over 5 minutes, per service (histogram)
histogram_quantile(0.99,
  sum by (le, service) (rate(http_request_duration_seconds_bucket[5m])))

# p50
histogram_quantile(0.50,
  sum by (le, service) (rate(http_request_duration_seconds_bucket[5m])))
```

---

## 2. The Core Metric Families

### Golden Signals (Google SRE)
| Signal | Question | Example |
|--------|----------|---------|
| **Latency** | How long do requests take? (track success and failed separately) | p99 `/checkout` |
| **Traffic** | How much demand? | Requests/sec |
| **Errors** | What fraction fail? (explicit 5xx, implicit wrong content, policy such as > 1s) | 5xx / total |
| **Saturation** | How "full" is the most constrained resource? | Queue depth, pool usage |

### RED method (request-driven services)
**R**ate, **E**rrors, **D**uration. Per service/endpoint.

### USE method (resources: CPU, disk, network, pools)
**U**tilization (% busy), **S**aturation (queued/waiting work), **E**rrors.

### Related terms
| Term | Meaning |
|------|---------|
| **Throughput** | Work completed per unit time (req/s, msg/s, MB/s) |
| **QPS / RPS / TPS** | Queries / Requests / Transactions per second |
| **Goodput** | Throughput of *useful* work (excludes retries, errors, duplicates) |
| **Latency** | Time for one operation to complete |
| **Response time** | Latency as seen by the client (service time + queueing + network) |
| **Service time** | Time actually spent processing (excludes waiting in queue) |
| **Queueing delay** | Time spent waiting before processing starts |
| **Time to first byte (TTFB)** | Time until the first response byte arrives |
| **RTT** | Round-trip time between two points |
| **Bandwidth** | Maximum data rate of a link |
| **Jitter** (network) | Variation in latency over time |
| **Saturation** | Resource at/near capacity; latency rises sharply (the "knee" of the curve) |
| **Concurrency** | Number of requests in flight at once |
| **Utilization** | Fraction of capacity in use |
| **Apdex** | Score (0-1) of satisfied/tolerating/frustrated users relative to a threshold |

### Little's Law
`L = λ × W`: the average number of items in a system (L) equals arrival rate (λ) times average time in system (W).
Example: 200 req/s × 0.5 s latency = **100 requests in flight**. If your thread/connection pool is 50, requests queue. This is why a latency increase (W↑) can exhaust a pool with *no* traffic increase.

### Why latency explodes near saturation
Queueing theory: as utilization → 100%, queue length and wait time grow non-linearly. At 70% utilization things look fine; at 95% latency can be 10-20x higher. **Keep headroom.**

---

## 3. Reliability: SLI, SLO, SLA, Error Budget

| Term | Definition | Example |
|------|-----------|---------|
| **SLI** (Indicator) | A measured quantity of service behaviour | % of requests with latency < 300 ms and non-5xx |
| **SLO** (Objective) | Internal target for an SLI over a window | 99.9% over 30 days |
| **SLA** (Agreement) | External, contractual promise with consequences (credits/penalties) | 99.5% monthly or refund |
| **Error budget** | Allowed unreliability = `1 - SLO` | 99.9% means 0.1% = ~43 min/month of failure allowed |
| **Burn rate** | How fast the budget is consumed (1x = exactly on budget) | 14x burn = a 30-day budget gone in ~2 days |

SLO < SLA (you want to fix problems before breaching the contract).

### Availability "nines"
| Availability | Downtime / year | Downtime / month |
|--------------|-----------------|------------------|
| 99% | 3.65 days | 7.3 hours |
| 99.9% ("three nines") | 8.76 hours | 43.8 min |
| 99.95% | 4.4 hours | 21.9 min |
| 99.99% ("four nines") | 52.6 min | 4.4 min |
| 99.999% ("five nines") | 5.3 min | 26 s |

**Serial dependencies multiply:** A (99.9%) → B (99.9%) → C (99.9%) gives ≈ 99.7% overall. **Parallel redundancy** raises availability: two independent 99% replicas gives 1 - 0.01² = 99.99% (if failures are truly independent).

### Recovery metrics
| Term | Meaning |
|------|---------|
| **MTTD** | Mean Time To Detect |
| **MTTA** | Mean Time To Acknowledge |
| **MTTR** | Mean Time To Recover/Repair/Resolve (be explicit which) |
| **MTBF** | Mean Time Between Failures |
| **MTTF** | Mean Time To Failure (non-repairable) |
| **RTO** | Recovery Time Objective: max acceptable downtime |
| **RPO** | Recovery Point Objective: max acceptable data loss (time) |

DORA delivery metrics: **deployment frequency**, **lead time for changes**, **change failure rate**, **time to restore service**.

---

## 4. Consistency, Replication & Consensus Terms

| Term | Meaning |
|------|---------|
| **CAP theorem** | Under a network **P**artition, choose **C**onsistency or **A**vailability |
| **PACELC** | If Partition: A vs C; **E**lse: **L**atency vs **C**onsistency |
| **ACID** | Atomicity, Consistency, Isolation, Durability (transactional guarantees) |
| **BASE** | Basically Available, Soft state, Eventually consistent |
| **Strong / linearizable consistency** | Every read sees the latest completed write; behaves like a single copy |
| **Sequential consistency** | All nodes see operations in the same order (not necessarily real-time) |
| **Causal consistency** | Causally-related operations seen in order everywhere |
| **Eventual consistency** | If writes stop, replicas converge, eventually |
| **Read-your-writes** | You always see your own prior writes |
| **Monotonic reads** | You never see data older than what you saw before |
| **Replication lag** | Delay between a write on primary and its visibility on a replica |
| **Quorum** | Majority needed for a decision. `W + R > N` guarantees read/write overlap |
| **Leader / follower (primary / replica)** | One node accepts writes; others copy |
| **Split brain** | Two nodes both think they are leader |
| **Consensus** | Nodes agreeing on a value despite failures (Paxos, Raft, ZAB) |
| **Fencing token** | Increasing number a resource checks, to reject stale leaders/lock holders |
| **Lease** | Time-limited lock/leadership that expires automatically |
| **CRDT** | Conflict-free Replicated Data Type; merges concurrent updates deterministically |
| **Vector clock / Lamport clock** | Logical clocks to capture causality without wall time |
| **Gossip protocol** | Nodes periodically exchange state with random peers to spread info |
| **Anti-entropy / read repair** | Background or on-read reconciliation of divergent replicas |
| **Tombstone** | Marker recording a deletion so it replicates |
| **Write-ahead log (WAL)** | Durable append-only log written before applying changes |
| **Idempotent** | Doing it N times has the same effect as once |
| **Exactly-once / at-least-once / at-most-once** | Message delivery guarantees (see problems doc §7) |

---

## 5. Resilience Patterns & Terms

| Term | Meaning |
|------|---------|
| **Timeout** | Max time to wait for a response. Every remote call needs one |
| **Deadline propagation** | Pass the remaining time budget downstream so callees can give up early |
| **Retry** | Re-attempt a failed call (safe only if idempotent) |
| **Exponential backoff** | Increase wait between retries (1s, 2s, 4s...) |
| **Jitter** | Randomness added to waits/TTLs to avoid synchronized spikes |
| **Retry budget** | Cap on retries as % of total traffic |
| **Circuit breaker** | Stops calling a failing dependency (closed → open → half-open) |
| **Bulkhead** | Isolate resources (pools) per dependency so one can't sink all |
| **Load shedding** | Deliberately rejecting excess work to protect the core |
| **Backpressure** | Signal from slow consumer to producer to slow down |
| **Rate limiting / throttling** | Cap request rate per client/tenant (token bucket, leaky bucket) |
| **Graceful degradation** | Reduced functionality instead of total failure |
| **Failover** | Switching to a standby on failure |
| **Failback** | Returning to the primary after recovery |
| **Hedged request** | Send a duplicate request after a delay; use the first reply (cuts tail latency) |
| **Health check: liveness / readiness / startup** | Alive? Ready for traffic? Finished booting? |
| **Canary / blue-green / rolling** | Progressive deployment strategies |
| **Feature flag** | Runtime switch to enable/disable code paths |
| **Chaos engineering** | Intentionally injecting failure to learn weaknesses |
| **Game day** | Rehearsed incident / failure drill |
| **Idempotency key** | Client-supplied ID letting the server dedupe retries |
| **Dead-letter queue (DLQ)** | Where messages that repeatedly fail are parked |
| **Saga** | Sequence of local transactions with compensating actions |
| **Outbox pattern** | Write event to DB in same transaction; relay publishes it later |
| **CQRS / Event sourcing** | Separate read/write models / store state as event log |

---

## 6. Failure & Incident Terms

| Term | Meaning |
|------|---------|
| **Partial failure** | Some parts fail, others continue |
| **Gray failure** | Degraded but not "down"; health checks pass, users hurt |
| **Cascading failure** | Failure spreads as load shifts to remaining components |
| **Retry storm** | Retries amplify load on a struggling service |
| **Thundering herd** | Many clients act simultaneously (expiry, restart) |
| **Cache stampede** | Many requests recompute/fetch the same expired entry at once |
| **Metastable failure** | Stays bad after the trigger is gone, sustained by feedback loops |
| **SPOF** | Single Point of Failure |
| **Blast radius** | How much is affected by a failure/change |
| **Heisenbug** | Bug that disappears or changes when observed |
| **Race condition** | Outcome depends on timing/interleaving |
| **Deadlock / livelock** | Mutual blocking / constant activity with no progress |
| **Hot spot / hot key / hot partition** | Disproportionate load on one node/key |
| **Noisy neighbour** | Co-tenant consuming shared resources |
| **Flapping** | Rapid up/down state changes |
| **Brownout** | Partial degradation (also: intentionally disabling features under load) |
| **SEV1-SEV4** | Incident severity levels |
| **RCA / postmortem** | Root-cause analysis / blameless review |
| **Runbook / playbook** | Documented response procedure |
| **On-call** | Rotating duty to respond to alerts |
| **Toil** | Repetitive manual operational work that should be automated |

---

## 7. Observability Terms

| Term | Meaning |
|------|---------|
| **Observability** | Ability to infer internal state from outputs (metrics, logs, traces) |
| **Metric** | Numeric time series (counter, gauge, histogram) |
| **Counter / Gauge / Histogram / Summary** | Only-increasing value / up-and-down value / bucketed distribution / client-computed quantiles |
| **Cardinality** | Number of unique label combinations; high cardinality explodes metric storage |
| **Log** | Timestamped event record (prefer structured JSON) |
| **Trace** | End-to-end record of one request across services |
| **Span** | One timed operation within a trace |
| **Trace ID / Span ID / Parent ID** | Identifiers linking spans into a tree |
| **Correlation / Request ID** | ID attached to everything about one request |
| **Context propagation** | Passing trace/baggage headers across hops (W3C `traceparent`) |
| **Sampling (head / tail)** | Deciding to keep traces at start / after completion (tail can keep errors & slow ones) |
| **OpenTelemetry (OTel)** | Vendor-neutral standard for instrumenting telemetry |
| **Instrumentation** | Code/agents that emit telemetry |
| **Flame graph** | Visualization of where CPU time is spent |
| **Dashboard / Alert** | Visual summary / automated notification on condition |
| **Synthetic monitoring / probing** | Scripted fake users testing from outside |
| **RUM** | Real User Monitoring (data from real clients) |
| **Dead-man's switch** | Alert that fires when expected data *stops* arriving |
| **Alert fatigue** | Ignoring alerts due to noise |
| **Burn-rate alert** | Alert on how quickly the error budget is being consumed |

---

## 8. Scaling & Architecture Terms

| Term | Meaning |
|------|---------|
| **Horizontal / vertical scaling** | Add more machines / make machines bigger |
| **Stateless / stateful** | No local state between requests / holds data locally |
| **Sharding / partitioning** | Splitting data across nodes by key |
| **Consistent hashing** | Hashing scheme that minimizes data movement when nodes change |
| **Replication factor** | Number of copies of each datum |
| **Load balancer (L4/L7)** | Distributes traffic at transport / application layer |
| **Reverse proxy / API gateway** | Front door handling routing, auth, rate limiting |
| **Service mesh / sidecar** | Infrastructure layer (proxies beside each service) for traffic, security, telemetry |
| **Service discovery** | How services find each other's addresses |
| **East-west / north-south traffic** | Service-to-service / client-to-system traffic |
| **Region / Availability Zone (AZ)** | Geographic area / isolated datacenter within a region |
| **Active-active / active-passive** | All sites serve traffic / standby waits |
| **CDN** | Content Delivery Network, edge caching |
| **Cold / warm / hot standby** | Standby readiness levels |
| **Data locality / affinity** | Keeping compute near data / sticky routing |
| **Fan-out / fan-in** | One request triggers many / many results aggregated |
| **Pub/sub** | Publishers send to topics; subscribers receive |
| **Consumer group / partition / offset / lag** | Kafka-style parallel consumption terms; **lag** = how far behind consumers are |
| **Monolith / microservices / distributed monolith** | One deployable / many small services / many services tightly coupled |
| **Conway's Law** | System design mirrors the organization's communication structure |
| **Amdahl's Law** | Speedup limited by the serial fraction of work |
| **Backward / forward compatibility** | New code reads old data / old code reads new data |
| **Expand-contract migration** | Safely changing schemas via add → dual-use → remove |

---

## 9. Quick Reference: Rules of Thumb

| Situation | Rule |
|-----------|------|
| Setting a timeout | About 2-3x the dependency's **p99**, within the caller's own deadline |
| Alerting on latency | Use **p95/p99**, never the mean |
| Combining percentiles | Merge histograms; never average percentiles |
| Retries | Backoff + jitter + budget; only for idempotent calls |
| Capacity target | Keep utilization ≲ 60-70% for latency-sensitive services |
| Pool size | From Little's Law: `concurrency ≈ rate × latency` |
| Availability of chain | Multiply availabilities of serial dependencies |
| Sample size for pN | At least ~10 / (1 - N/100) samples per window (1,000 for p99, 10,000 for p99.9) |
| Quorum | `W + R > N` for overlap; majority = `⌊N/2⌋ + 1` |
| Cluster size for consensus | Odd numbers (3, 5, 7); tolerates `⌊(N-1)/2⌋` failures |

---

## 10. Common Confusions

| Confusion | Clarification |
|-----------|--------------|
| **Latency vs throughput** | Latency = time per request; throughput = requests per time. You can have high throughput *and* high latency (a deep pipeline) |
| **SLA vs SLO** | SLA is a contract with penalties; SLO is your internal goal (stricter) |
| **Availability vs reliability** | Availability: is it up? Reliability: does it work correctly over time? |
| **Consistency (CAP) vs consistency (ACID)** | CAP-C is linearizability across replicas; ACID-C is satisfying data invariants |
| **Replication vs backup** | Replication copies mistakes (a `DELETE` replicates). Backups give point-in-time recovery |
| **Liveness vs readiness probe** | Liveness: restart me if failing. Readiness: don't send me traffic yet. Mixing them causes restart loops |
| **Exactly-once delivery vs processing** | Delivery is impossible in general; effectively-once processing is achieved with idempotency/transactions |
| **Mean vs median vs p99** | Mean is skewed by outliers; median = typical; p99 = near-worst case |
