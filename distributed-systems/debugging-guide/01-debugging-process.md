# Debugging Distributed Systems: Steps and Process

**Audience:** senior software engineers (SSE) and tech leads who own or are on call for production systems.

This is the **methodology**: the order in which to think and act during an incident. For concrete scenarios and commands (DNS, DB locks, network policy, etc.) see [22-debugging-distributed-systems.md](../22-debugging-distributed-systems.md). For vocabulary see [03-metrics-and-terminology.md](./03-metrics-and-terminology.md). For the catalogue of what goes wrong see [02-problems-in-distributed-systems.md](./02-problems-in-distributed-systems.md). For the lead's side (roles, communication, escalation, safe prod access, change control, postmortem quality) see [04-tech-lead-playbook.md](./04-tech-lead-playbook.md). For quick-lookup tables (HTTP 5xx meanings, timeouts, TCP, DB, runtime symptoms) see [05-triage-cheatsheets.md](./05-triage-cheatsheets.md).

---

## Why This Is Harder Than Debugging a Monolith

| Monolith | Distributed system |
|----------|--------------------|
| One process, one log, one stack trace | N processes, N logs, no single stack trace |
| Failures are mostly total (crash) | Failures are mostly **partial** (slow, wrong, intermittent) |
| One clock | Many clocks that disagree |
| Reproduce locally | Often only reproducible under production load/timing |
| Call is success or exception | Call can **time out**: you don't know whether it happened |

Consequence: you cannot reason from the code alone. You must reason from **evidence** (metrics, logs, traces) and keep competing hypotheses alive until the data kills them.

---

## The Process at a Glance

```
1. TRIAGE        Is it real? How bad? Who is affected?
2. STABILIZE     Stop the bleeding (rollback, shed load, failover)
3. SCOPE         Narrow: which service / region / customer / version / time?
4. HYPOTHESIZE   List causes, ranked by likelihood x cheapness to test
5. TEST          Use metrics -> traces -> logs -> infra; change ONE thing
6. FIX           Smallest safe change; verify against the same signal
7. VERIFY        Did the symptom metric recover? Any new side effects?
8. LEARN         Blameless postmortem, add detection + guardrails
```

Steps 2 and 3-5 run **in parallel** in a real incident. Mitigation first, root cause later. Do not hold a rollback hostage to understanding.

---

## Step 1: Triage

Answer these in the first five minutes, in writing (incident channel):

- **What is the symptom, in user terms?** "Checkout returns 500" beats "payment-svc is weird".
- **How is it measured?** Which SLI is violated: error rate, latency (p99), availability, freshness?
- **Blast radius:** all users or a subset? One region? One tenant? One app version? One endpoint?
- **Start time:** to the minute. This is your most valuable clue.
- **Severity:** use your org's scale (SEV1-4). Declare an incident commander if SEV1/2.

> Rule: if you cannot state the symptom as a measurable query, you are not debugging yet; you are guessing.

---

## Step 2: Stabilize (Mitigate Before You Understand)

Ordered from cheapest/safest to most disruptive:

1. **Roll back the last change.** Deploy, config, feature flag, schema migration, infra change. Most incidents correlate with a recent change.
2. **Turn off the feature flag** or shed the non-critical feature.
3. **Shed load / rate-limit** to protect the core path (return 429 or degrade gracefully).
4. **Fail over** to another region/replica/AZ.
5. **Scale out** (only if the bottleneck is truly capacity; scaling a bottleneck on a shared DB makes it worse).
6. **Restart** as a last resort. It destroys evidence (heap, thread dumps, in-memory state). Capture it first: thread dump, heap dump, `kubectl logs --previous`.

Capture evidence **before** you destroy it.

---

## Step 3: Scope: Narrow the Search Space

Slice the failing metric along every dimension available. The dimension where the problem is concentrated is your lead.

| Slice by | A skew here suggests |
|----------|----------------------|
| **Time** | Deploy, cron job, traffic spike, cert expiry, GC cycle, daily batch |
| **Version / build** | Bad release; use canary comparison |
| **Region / AZ / zone** | Network, cloud-provider issue, regional dependency |
| **Host / pod / instance** | Bad node, noisy neighbour, disk, memory leak, stale cache |
| **Endpoint / route** | Specific code path or downstream dependency |
| **Customer / tenant / key** | Hot partition, data-dependent bug, large payload |
| **Client type** | Mobile vs web: SDK bug, retry storm from one client |
| **Dependency** | Which downstream's latency or error rate moved first? |

Technique: **"What changed?" and "What is different?"**
- *What changed* near the start time: deploys, flags, config, infra, dependency versions, traffic mix, upstream partners, certificates, quotas, DNS.
- *What is different* between healthy and unhealthy instances/requests: version, node, zone, payload size, header, user attribute.

---

## Step 4: Hypothesize

Write hypotheses down. Rank by **probability x cost-to-test**. Test cheap ones first.

Start with the common causes (roughly in order of how often they bite):

1. A recent **deploy or config change**
2. A **downstream dependency** is slow or failing (and you have no timeout/circuit breaker)
3. **Resource exhaustion**: connection pool, threads, file descriptors, memory, CPU throttling, disk, ephemeral ports
4. **Database**: slow query, lock contention, replica lag, failover
5. **Network / DNS**: packet loss, MTU, DNS TTL, NetworkPolicy, LB health checks
6. **Retry amplification / thundering herd**
7. **Cache**: cold cache, mass expiry, eviction, stampede
8. **Expiry**: TLS certs, tokens, credentials, licences
9. **Data-dependent bug**: one malformed record, one huge tenant
10. **Clock skew**, GC pauses, noisy neighbours

Keep **more than one** hypothesis open. The most common failure of a debugger is falling in love with the first theory.

---

## Step 5: Test: Follow the Signal Funnel

Move from broad and cheap to narrow and expensive:

```
Metrics   ->   Traces   ->   Logs   ->   Profiles / dumps   ->   Packet capture
(what/where)   (which hop)  (why)        (code-level)           (wire-level)
```

### 5.1 Metrics: "Where is it broken?"

Use the **RED** method for services and **USE** method for resources.

- **RED** (per service): **R**ate, **E**rrors, **D**uration (p50/p95/p99)
- **USE** (per resource): **U**tilization, **S**aturation, **E**rrors
- Google's **Four Golden Signals**: latency, traffic, errors, saturation

Walk the dependency graph from the user-facing edge inward. The **deepest service whose metrics are already unhealthy** is usually closest to the root cause. Callers upstream of it look bad only because they are waiting on it.

```
User -> Gateway -> Orders -> Payments -> Bank API
                              ^ first unhealthy hop = suspect
```

Compare against: same time yesterday, same time last week (seasonality), and the pre-incident baseline.

### 5.2 Traces: "Which hop, and what is it waiting on?"

- Pull a **slow** trace and a **failed** trace (and a healthy one for contrast).
- Look for the span with the largest **self time** (duration minus children).
- Look for: sequential calls that could be parallel, N+1 call patterns, retries visible as repeated spans, gaps (time not covered by any span: queueing, GC, thread starvation, missing instrumentation).
- If traces are sampled, use tail-based/error-biased sampling so failures are kept.

### 5.3 Logs: "Why?"

- Search by **trace ID / request ID / correlation ID**, not by timestamp alone.
- Look at the **first** error, not the loudest. Later errors are usually cascade noise.
- Check the log **volume** itself: a sudden drop (process hung, logging pipeline broken) is as informative as a spike.
- Mind **clock skew**: order by causality (trace parent/child) not by wall-clock across hosts.
- Count, don't read: group by error message/class and compare counts before vs. after the start time.

### 5.4 Profiles and dumps: "Which code?"

| Symptom | Tool |
|---------|------|
| High CPU | CPU profile / flame graph (pprof, async-profiler, `perf`, dotnet-trace) |
| Hangs / low CPU but slow | **Thread dump** x3, 5-10s apart. Look for threads all blocked on the same lock or I/O |
| Memory growth | Heap dump / allocation profile, GC logs |
| Latency spikes at regular intervals | GC logs, cron jobs, compaction, log rotation |
| Connection errors | `ss -s`, `netstat`, FD count (`lsof`), pool metrics |

### 5.5 Infrastructure and network

- Containers: restarts, OOMKilled, CPU throttling (`container_cpu_cfs_throttled_*`), node pressure, evictions.
- Network: DNS resolution time, retransmits, connection resets, TLS handshake failures, LB target health, NAT/conntrack table full, ephemeral port exhaustion.
- Cloud: provider status page, quota/throttling errors (429s from the cloud API), disk IOPS burst credits exhausted.

### The Discipline of Testing

- **Change one variable at a time**, or you won't know what worked.
- **Write down the prediction** before looking at data: "If it's the DB, p99 of `orders.query` rises first." A hypothesis that can't be falsified isn't useful.
- **Prefer read-only investigation** in production; make changes deliberately and reversibly.
- **Correlation is not causation.** The metric that moved first is more suspect than the one that moved with it.

---

## Step 6: Fix

- Choose the **smallest, most reversible** change that addresses the verified cause.
- Prefer: config/flag change > rollback > targeted patch > redesign. Redesign is a follow-up, not an incident action.
- Use a **canary / staged rollout** even for the fix. A hurried fix is a common cause of the second outage.

---

## Step 7: Verify

- Watch the **same SLI** you used to declare the incident. It must return to baseline.
- Check for **recovery side effects**: retry backlogs draining, cache warm-up, DB catch-up, queue backlog, replica lag, duplicate processing after a replay.
- Keep the incident open until metrics are stable for a defined period (e.g., 30-60 min) and the backlog is drained.

---

## Step 8: Learn (Blameless Postmortem)

Minimum contents:

1. **Timeline** (detection, escalation, mitigation, resolution) with timestamps
2. **Impact** (users, duration, revenue/SLO burn)
3. **Root cause(s)** and **contributing factors** (there is almost never a single cause)
4. **What went well / what didn't**: detection delay? missing dashboard? unclear ownership?
5. **Action items** with owners and dates, in three categories: **detect** faster, **mitigate** faster, **prevent** recurrence

Ask "5 whys", but stop at systemic causes (no timeout, no alert, no load test), not at "human error".

---

## Common Debugging Patterns by Symptom

| Symptom | First checks |
|---------|--------------|
| **p99 high, p50 normal** | Tail latency: GC, lock contention, one slow node/shard, retries, cold cache, queueing, noisy neighbour. Compare per-instance p99 |
| **Latency up everywhere, gradually** | Saturation: pool exhaustion, queue buildup, memory leak, DB growth/missing index |
| **Sudden 5xx spike at exact time** | Deploy/config/cert/flag change; dependency outage |
| **Intermittent errors (1-5%)** | One bad instance, retries masking failures, partial deploy, DNS round-robin to a dead host |
| **Timeouts, but dependency looks healthy** | Timeout shorter than dependency p99; connection pool wait counted in timeout; network/DNS; LB idle timeout < keep-alive |
| **Error rate climbs then everything dies** | Retry storm / cascading failure: look for amplification factor (retries x layers) |
| **Works for some users only** | Data-dependent bug, hot shard, stale cache, feature flag cohort, region |
| **Data inconsistent between services** | Lost/duplicate event, non-idempotent consumer, out-of-order delivery, replica lag, failed saga compensation |
| **Duplicate side effects (double charge/email)** | Retry without idempotency key, at-least-once delivery, client timeout while server succeeded |
| **Everything slow after deploy, CPU normal** | New dependency call (N+1), blocked threads, connection pool too small, DNS |
| **Queue/lag growing** | Consumer slower than producer, poison message, rebalance loop, downstream throttling |
| **Service restarts repeatedly** | OOMKilled, failing liveness probe (too aggressive), crash on startup dependency |

---

## Debugging Anti-Patterns

| Anti-pattern | Why it hurts | Instead |
|--------------|--------------|---------|
| Restart first, ask later | Destroys evidence; masks the leak/bug that returns | Capture dumps/logs, then restart |
| Scaling out blindly | Shared bottleneck (DB, lock) gets worse; costs money | Prove the bottleneck is capacity first |
| Debugging with averages | Averages hide tails where users suffer | Use p95/p99 and histograms |
| Trusting one data source | Logs lie by omission; metrics hide detail | Triangulate metrics + traces + logs |
| Changing several things at once | Can't attribute the fix | One change, observe, repeat |
| Blaming the last person to touch it | Wrong lessons; hides systemic causes | Blameless, system-focused |
| Chasing the loudest error | Cascade noise drowns the origin | Find the **first** error in time/causality |
| "Can't reproduce, closing" | Distributed bugs are timing-dependent | Add instrumentation; use chaos/fault injection to reproduce |
| Too many people in the room | Duplicate effort, conflicting changes | One incident commander; assign investigation threads |

---

## Pre-Incident Preparation (What Makes Debugging Possible)

You cannot debug what you cannot see. Before the incident:

- [ ] **Correlation IDs / trace context** propagated across every hop, including async messages and logs
- [ ] **Structured logs** (JSON) with service, version, instance, trace_id, level
- [ ] **RED metrics** per service and per endpoint, with histograms (not just averages)
- [ ] **Dashboards** per service: golden signals + dependency health + saturation
- [ ] **Deploy markers** on every graph (annotations for deploys, flags, config)
- [ ] **Alerts on SLO burn rate**, not only on raw thresholds
- [ ] **Timeouts, retries with backoff + jitter, circuit breakers** on all remote calls
- [ ] **Runbooks** linked from alerts, tested in game days
- [ ] **Ability to roll back** in minutes, rehearsed
- [ ] **Chaos / fault-injection tests** to learn failure modes in daylight
- [ ] **Time sync** (NTP/chrony) monitored across hosts
- [ ] **Debug tooling** pre-approved: ephemeral debug containers, profiler access, log search

---

## One-Page Incident Checklist

```
[ ] State symptom as a query/SLI.  Declare severity. Name incident commander.
[ ] Note exact start time.  What changed around then?
[ ] Mitigate: rollback / flag off / shed load / failover.
[ ] Scope: slice by time, version, region, host, endpoint, tenant.
[ ] Walk dependency graph: find the deepest unhealthy hop.
[ ] Hypotheses written down, ranked; test cheapest first.
[ ] Metrics -> traces -> logs -> profiles -> packets.
[ ] Capture evidence BEFORE restarting anything.
[ ] One change at a time; verify on the same SLI.
[ ] Watch for recovery side effects (retries, backlogs, lag).
[ ] Postmortem: timeline, causes, detect/mitigate/prevent actions.
```
