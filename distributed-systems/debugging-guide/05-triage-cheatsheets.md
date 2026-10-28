# Triage Cheat Sheets

Fast-lookup tables for the first hour of an incident. These tell you *where to look*; confirm with data. See [01-debugging-process.md](./01-debugging-process.md) for the full process.

---

## 1. HTTP Status Codes: What They Imply Operationally

| Code | Usually means | Look at |
|------|---------------|---------|
| **400** | Client sent bad request | Recent client/SDK release; schema change; validation rules |
| **401 / 403** | Auth failed / forbidden | Expired token or cert, key rotation, clock skew (JWT `nbf`/`exp`), policy change |
| **404** | Route/resource not found | Bad deploy of gateway routes, service discovery, wrong base path, deleted data |
| **408** | Server timed out waiting for the client | Slow clients, large uploads |
| **409** | Conflict | Optimistic-lock failures, duplicate idempotency key |
| **413 / 414** | Payload / URI too large | Gateway or proxy body limits |
| **429** | Rate limited | Who is limiting (gateway, dependency, cloud quota)? Retry storm? Missing `Retry-After` handling |
| **499** (nginx) | Client closed the connection before the response | Server too slow versus client timeout; client-side timeouts |
| **500** | Unhandled exception in the app | Stack traces by version; first occurrence time; new input shape |
| **502 Bad Gateway** | Proxy got an invalid/closed response from upstream | Upstream crashed/restarting, connection reset, keep-alive mismatch, protocol mismatch |
| **503 Service Unavailable** | No healthy upstream / overloaded / shedding | Readiness failures, zero endpoints, circuit open, deploy in progress, load shedding |
| **504 Gateway Timeout** | Upstream too slow for the proxy's timeout | Slow dependency/DB; proxy timeout lower than upstream's; queueing |
| **5xx only from LB, nothing in app logs** | Request never reached the app | LB target health, security groups/NetworkPolicy, connection limits, TLS |

> **Pattern:** 502/503/504 from the edge with a healthy-looking app often means a **timeout/keep-alive mismatch** or **readiness flapping**, not an app bug.

---

## 2. Timeouts: The Many Kinds (and the Ones People Forget)

| Timeout | Governs | Typical trap |
|---------|---------|--------------|
| **DNS** | Name resolution | Default resolver timeouts can be seconds; `ndots` multiplies lookups |
| **Connect** | TCP/TLS handshake | Keep short (100-500 ms in-DC). Retrying a dead host wastes the budget |
| **Read / response** | Waiting for response bytes | Too long: threads pile up. Too short: false failures at p99 |
| **Write** | Sending the request | Big uploads |
| **Total / deadline** | Whole operation | Missing deadline propagation: callee works after caller gave up |
| **Pool acquire / checkout** | Waiting for a connection from the pool | Often counted *inside* your "request timeout"; looks like a slow dependency |
| **Idle / keep-alive** | Reusing connections | LB idle timeout < client keep-alive gives random connection resets (502/ECONNRESET) |
| **Statement / query** | DB query | Missing, so one bad query holds locks and connections indefinitely |
| **Lock wait** | Waiting for DB/row locks | Hidden long waits |
| **Visibility timeout** (queues) | How long a message is hidden while processed | Shorter than processing time means duplicate processing |
| **Session / heartbeat** | Cluster/consumer liveness | Too tight, so false failure detection and rebalances during GC pauses |
| **Graceful shutdown / termination grace** | Time to drain on stop | Shorter than longest request, so in-flight requests are killed on deploy |

**Rule of thumb:** `client timeout > server work time > downstream timeouts` is *wrong*. The **callee's timeout must be shorter than the caller's**, within a propagated overall deadline. And the sum of (retries x timeouts) must fit within the user's patience.

---

## 3. Symptom -> Likely Layer

| Symptom | Network | Load balancer / proxy | App code | Runtime (GC/threads) | Database | Dependency | Infra (node/container) |
|---------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Connection refused | x | x | | | | | x (pod down) |
| Connection reset / EOF | x | x (idle timeout) | | | | | x |
| Timeouts to one service only | | | | | | x | |
| p99 spikes at regular intervals | | | x (cron) | x (GC) | x (checkpoint/vacuum) | | x |
| CPU low but latency high | | | | x (blocked threads) | x (waiting) | x (waiting) | |
| CPU high | | | x (hot loop, regex) | x (GC thrash) | | | x (throttling) |
| Errors only on one instance | | | | | | | x |
| Errors only after deploy | | | x | | x (migration) | | |
| Slow only for some tenants | | | x (data-dependent) | | x (query plan/hot rows) | | |
| 5xx under peak only | | | | x (pool/thread limits) | x (connections) | x (rate limits) | x (autoscale lag) |

---

## 4. Network and TCP Quick Reference

| Observation | Meaning | Action |
|-------------|---------|--------|
| Many `SYN_SENT` | Can't establish connections (blocked, down, dropped) | Check firewall/NetworkPolicy/route, target health |
| Many `TIME_WAIT` | Many short-lived connections | Use connection pooling/keep-alive; check ephemeral port exhaustion |
| Many `CLOSE_WAIT` | **Your app** isn't closing sockets after the peer closed | Connection leak in code |
| Many `ESTABLISHED` near pool/ulimit | Saturation / leak | Check pool sizes, FD limit (`ulimit -n`) |
| Retransmits rising | Packet loss/congestion | Network path, MTU, noisy neighbour, NIC |
| Small payloads work, large hang | MTU / fragmentation / proxy body limit | Check path MTU, proxy limits |
| TLS handshake failures | Expired/untrusted cert, protocol/cipher mismatch, SNI | Check cert dates, chain, SNI, mTLS config |
| Slow first byte, fast rest | DNS, TLS handshake, cold connection, or server queueing | Time each phase (`curl -w` timings) |
| `conntrack: table full` (kernel log) | NAT/conntrack exhaustion | Raise limit, reduce short connections |

```bash
# Break down where time goes for one request
curl -s -o /dev/null -w "dns:%{time_namelookup} connect:%{time_connect} tls:%{time_appconnect} ttfb:%{time_starttransfer} total:%{time_total}\n" https://svc.example.com/health

ss -s                       # socket summary by state
ss -tanp | awk '{print $1}' | sort | uniq -c    # counts per TCP state
```

---

## 5. Database Quick Reference

| Symptom | Likely cause | First check |
|---------|--------------|-------------|
| Query slow suddenly, no deploy | **Plan regression** (stats stale, parameter sniffing), data growth crossing a threshold | Execution plan now vs. before; update stats; index usage |
| Many queries slow simultaneously | Lock contention, I/O saturation, CPU, memory pressure, checkpoint/backup/maintenance | Blocking sessions, wait stats, disk latency |
| App says "pool exhausted" but DB idle | Connections leaked/held by long app work or slow downstream, not a DB problem | Pool metrics; transactions held open while calling other services (anti-pattern) |
| Deadlocks | Inconsistent lock ordering; long transactions | Deadlock graph; shorten transactions; consistent access order |
| Replica returns old data | Replication lag | Lag metric; route read-after-write to primary |
| Writes slow, reads fine | Log/WAL I/O, lock waits, triggers/indexes, sync replication | Log flush latency, write amplification |
| Failover caused errors | Connections to old primary; DNS TTL; app lacks reconnect/retry | Driver reconnect behaviour; connection string; retry on transient errors |
| Connection count at max | Too many app instances x pool size | `instances x pool_max` vs. DB `max_connections`; use a pooler (PgBouncer, etc.) |
| Timeouts on one table | Hot rows/missing index/lock escalation | Top waits on that table; blocking chain |

**Pool sizing (Little's Law):** `connections needed ≈ queries/sec x avg query time (s)`, with headroom. Bigger pools rarely fix slowness; they move the queue into the database.

**SQL Server pointers** (common in .NET stacks): `sys.dm_exec_requests` + `sys.dm_exec_sql_text` (running queries), `sys.dm_os_waiting_tasks` (waits/blocking), `sys.dm_exec_query_stats` (heavy queries), Query Store (plan regressions), `sp_whoisactive` (if installed), deadlock graphs via Extended Events / `system_health`. Use read-only sessions and `SET LOCK_TIMEOUT` for diagnostics.

---

## 6. Runtime Symptoms (.NET / JVM / Node / Go)

| Symptom | Likely cause | Tooling |
|---------|--------------|---------|
| Latency spikes, CPU fine | **GC pauses** (large heap, LOH/gen2 in .NET; stop-the-world in JVM) | GC logs, `dotnet-counters`, `dotnet-gcdump`, JFR, GC metrics |
| Memory grows steadily | Leak: static caches, event handlers, unbounded collections, `HttpClient` misuse | Heap snapshot diffs (`dotnet-gcdump`, MAT, `pprof`) |
| Requests hang, CPU low | **Thread pool starvation** / blocked threads; sync-over-async (`.Result`/`.Wait()`) in .NET; exhausted event loop in Node | `dotnet-counters` (ThreadPool queue length), thread dumps `dotnet-stack`, `jstack` |
| Intermittent socket errors (.NET) | `new HttpClient()` per request (socket exhaustion) or long-lived with stale DNS | Use `IHttpClientFactory`/`SocketsHttpHandler` with `PooledConnectionLifetime` |
| High CPU | Hot loop, regex backtracking, serialization, excessive logging, GC thrash | CPU profile, flame graph, `dotnet-trace`, `async-profiler`, `pprof` |
| OOMKilled (container) | Memory limit < real usage (heap + native + buffers) | Compare container limit with heap setting; `kubectl describe pod` |
| Throttled CPU (containers) | CPU limit too low; latency without high "usage" | `container_cpu_cfs_throttled_periods_total` |
| Startup slow / first requests slow | JIT, cold caches, lazy init, pool warm-up | Warm-up endpoint; readiness after warm |
| Too many open files | FD leak / low `ulimit` | `lsof -p <pid>`; count entries in `/proc/<pid>/fd` |
| Deadlock | Lock ordering, async/sync mixing | Thread dumps; look for threads waiting on each other |

**Take thread dumps x3, 5-10 s apart.** One dump is a photo; three are a movie. Threads in the same state and stack across dumps are your suspects.

---

## 7. Queues and Streams

| Symptom | Likely cause | Check |
|---------|--------------|-------|
| Lag growing | Consumers slower than producers; poison message; rebalance loop; downstream throttling | Consumer throughput, processing time p99, rebalance count, per-partition lag |
| One partition lagging | Hot key / stuck message on that partition | Per-partition lag, offsets, message sizes |
| Duplicates | At-least-once + non-idempotent consumer; visibility timeout < processing time; rebalance | Dedupe keys; processing duration vs. timeout |
| Lost messages | At-most-once config; ack before processing; retention expired; DLQ ignored | Ack mode, retention, DLQ depth |
| Out-of-order | Multiple partitions/consumers without key partitioning | Partition key choice; sequence numbers |
| DLQ filling | Schema change, bad data, downstream outage | DLQ message sample, first-failure time |
| Frequent rebalances | Session timeout < GC/processing pauses; flapping consumers | `max.poll.interval`, session timeout, consumer restarts |

---

## 8. Kubernetes / Container Triage

| Signal | Meaning | Command |
|--------|---------|---------|
| `CrashLoopBackOff` | App crashes at start | `kubectl logs <pod> --previous`; check config/secrets/dependencies |
| `OOMKilled` | Exceeded memory limit | `kubectl describe pod`; raise limit or fix leak |
| `ImagePullBackOff` | Wrong tag/registry auth | `kubectl describe pod` events |
| `Pending` | Unschedulable (resources, taints, PVC) | `kubectl describe pod`; node capacity |
| Readiness failing | Pod removed from endpoints | Probe config; startup time; dependency checks in readiness (anti-pattern: shared dependency flaps all pods) |
| Liveness restarts under load | Probe timeout too tight, restart makes it worse | Loosen liveness; keep it shallow |
| `Evicted` | Node pressure (memory/disk) | `kubectl describe node`; requests/limits |
| Slow rollouts / dropped requests on deploy | No preStop/drain; termination grace too short; maxUnavailable too high | `preStop` sleep, `terminationGracePeriodSeconds`, PodDisruptionBudget |
| Uneven load across pods | Long-lived connections (gRPC/HTTP2) pinned to a pod | L7/client-side LB, max connection age |

```bash
kubectl get events --sort-by=.lastTimestamp -n <ns> | tail -30
kubectl top pods -n <ns>; kubectl top nodes
kubectl get pods -o wide -n <ns>                  # which node? which restarts?
kubectl rollout history deploy/<name> -n <ns>     # what changed?
kubectl rollout undo deploy/<name> -n <ns>        # mitigation
```

---

## 9. "It Worked Yesterday" Checklist (What Silently Changes)

- [ ] Deploys (app, sidecar, mesh, gateway, base image, dependencies)
- [ ] Feature flags and config values
- [ ] **Certificates, tokens, keys, passwords expired/rotated**
- [ ] Data volume or one outlier tenant/record
- [ ] Traffic shape (new client, marketing event, bot, batch job, partner)
- [ ] Scheduled jobs (backups, index rebuilds, ETL, cron at :00)
- [ ] Cloud: quotas, throttling, maintenance events, instance retirements, DNS changes
- [ ] Daylight-saving / month-end / leap boundaries; time-zone-dependent logic
- [ ] Dependency's behaviour or rate limits changed (check their status page)
- [ ] Autoscaler behaviour; node pool upgrade; kernel/OS patching
- [ ] Disk filling up, log volume growth, inode exhaustion
- [ ] Cache TTL cohorts expiring together

---

## 10. First 15 Minutes: Command-Level Starter Kit

```
1. Dashboard: RED for the failing service + its direct dependencies (last 6h, compare to last week)
2. Annotations: deploys/flags/config in the 2h before start
3. Slice the error: by version, instance, region, endpoint, tenant
4. Top error messages (count by message) since start time
5. One failed trace + one slow trace + one healthy trace
6. Saturation: CPU throttle, memory, pools, queue depth, DB waits, FDs
7. Dependency health: their RED, their status page
8. Decide: rollback / flag-off / shed load / failover? Announce in channel
```
