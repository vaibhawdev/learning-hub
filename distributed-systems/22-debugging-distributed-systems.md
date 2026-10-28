# Debugging Distributed Systems

## Why Debugging Is Different

In a monolith you attach a debugger, set a breakpoint, and inspect state. In a distributed system:

- The bug may live in service 5 of 12 in a request chain
- The failure may only occur under load, never in isolation
- The bug may be timing-dependent (race condition across services)
- By the time you investigate, the faulty pod may have been restarted
- The problem could be in the network, not in your code at all
- Logs from 8 services must be correlated to understand one failed request

**Distributed debugging is an inference problem, not a step-through problem.** You build a mental model from evidence.

---

## The Debugging Toolkit

| Tool Type | What It Answers | Examples |
|-----------|----------------|---------|
| Distributed tracing | Where did this request spend time? | Jaeger, Zipkin, Tempo |
| Centralized logging | What happened, in what order? | ELK, Loki, CloudWatch |
| Metrics + dashboards | Is a component performing normally? | Prometheus, Grafana |
| Service mesh telemetry | Which service-to-service call is failing? | Istio/Kiali, Linkerd |
| APM | Deep-dive per-process profiling | Datadog APM, New Relic, Pyroscope |
| Network tools | Is the packet reaching the destination? | tcpdump, Wireshark, Cilium Hubble |
| Container tools | What is this pod doing right now? | kubectl debug, nsenter, ephemeral containers |

---

## Problem 1: "The request is slow but I don't know where"

### Symptoms
- P99 latency spike
- Users complaining about slow page loads
- No individual service metrics look alarming

### Approach: Distributed Tracing

```
Step 1: Find a slow trace_id from logs or APM
Step 2: Open in Jaeger/Tempo
Step 3: Look for the widest span (longest duration)

Example trace:
checkout-api       [====================] 1200ms
  ├─ auth-service  [=] 12ms
  ├─ cart-service  [==] 22ms
  └─ payment-api   [================] 1100ms   ← root cause
       ├─ db-query  [===] 30ms
       └─ stripe-api [=============] 1050ms    ← external call
```

**Finding**: Stripe API is slow. Solution: Check Stripe status page; add timeout + circuit breaker; add caching for idempotent calls.

### Useful Commands

```bash
# Get all slow traces from Jaeger (last 1 hour, >1s)
curl "http://jaeger:16686/api/traces?service=checkout-api&minDuration=1000ms&limit=20"

# Find trace by request ID from logs
grep "trace_id=abc123" /var/log/checkout-api/*.log
# Then look up abc123 in Jaeger UI
```

---

## Problem 2: "Intermittent errors — hard to reproduce"

### Symptoms
- Error rate is 0.1%–0.5% — happens but rarely
- Can't reproduce in dev or staging
- Seems random

### Approach: Log + Trace Correlation

```bash
# Step 1: Find error traces in logs
{app="checkout-api"} |= "error" | json | level="error"
  | line_format "{{.trace_id}} {{.error}} {{.user_id}}"

# Step 2: Collect all trace_ids from errors in last hour
# Step 3: For each trace_id, look at the full trace in Jaeger
# Step 4: Find what all the failures have in common (same node? same service? same time?)
```

### Common Root Causes for Intermittent Errors

| Pattern | Cause | Fix |
|---------|-------|-----|
| Errors clustered on one pod | Memory leak, bad state in one instance | Rolling restart; find leak with heap dump |
| Errors spike every N minutes | Batch job runs; GC pause; cache TTL expiry stampede | Profile GC; add jitter to TTLs |
| Errors on specific user IDs | Specific data triggers a code path | Reproduce with that user in staging |
| Errors only under load | Race condition; connection pool exhaustion | Load test; add connection pool monitoring |
| Errors at deploy time | Rolling update — old + new version incompatible | Backward-compat check; feature flags |
| Errors at specific times | Cron job interfering; rate limit window reset | Correlate error time with scheduled tasks |

---

## Problem 3: "Service A can't reach Service B"

### Approach: Systematic Network Debugging

#### Step 1: DNS Resolution

```bash
# Exec into the failing pod
kubectl exec -it pod/service-a-xyz -- sh

# Test DNS
nslookup service-b.namespace.svc.cluster.local
# Expected: responds with ClusterIP

# If fails: check CoreDNS
kubectl logs -n kube-system -l k8s-app=kube-dns
kubectl get pods -n kube-system -l k8s-app=kube-dns
```

#### Step 2: Service Exists and Has Endpoints

```bash
# Check the service exists
kubectl get service service-b -n namespace

# Check it has endpoints (pods selected)
kubectl get endpoints service-b -n namespace
# Empty endpoints = pod selector doesn't match pod labels
# → Compare: kubectl describe svc service-b vs kubectl get pods --show-labels

# Check pod labels match service selector
kubectl describe svc service-b | grep Selector
kubectl get pods -l app=service-b -n namespace
```

#### Step 3: Pod Is Healthy

```bash
# Is the pod running and passing readiness?
kubectl get pods -n namespace -l app=service-b
# CrashLoopBackOff → check logs: kubectl logs pod/service-b-xyz --previous

# Readiness probe failing → pod not in service endpoints
kubectl describe pod service-b-xyz | grep -A5 "Readiness"
```

#### Step 4: Network Policy Blocking

```bash
# List all NetworkPolicies in namespace
kubectl get networkpolicy -n namespace

# Describe to see what is allowed/denied
kubectl describe networkpolicy deny-all -n namespace

# Test connectivity directly (bypass service)
kubectl exec -it pod/service-a-xyz -- wget -T 5 http://<pod-IP>:8080/health
```

#### Step 5: Port Forward to Test Service Directly

```bash
kubectl port-forward svc/service-b 8888:80 -n namespace
curl localhost:8888/health
```

#### Step 6: Packet Capture

```bash
# Install tcpdump in ephemeral debug container
kubectl debug -it pod/service-a-xyz --image=nicolaka/netshoot -- tcpdump -i eth0 -n host <service-b-ip>

# Or via Cilium Hubble (if using Cilium)
hubble observe --namespace production --pod service-a-xyz
```

---

## Problem 4: "Database is slow — is it the query or the connection?"

### Step 1: Identify Slow Queries

```sql
-- PostgreSQL: currently running slow queries
SELECT pid, now() - query_start AS duration, query, state
FROM pg_stat_activity
WHERE state != 'idle'
  AND now() - query_start > interval '5 seconds'
ORDER BY duration DESC;

-- PostgreSQL: historical slow queries (pg_stat_statements)
SELECT query, calls, mean_exec_time, total_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 20;
```

### Step 2: Check for Lock Contention

```sql
-- PostgreSQL: who is blocking whom
SELECT
    blocked.pid AS blocked_pid,
    blocked.query AS blocked_query,
    blocking.pid AS blocking_pid,
    blocking.query AS blocking_query
FROM pg_stat_activity blocked
JOIN pg_stat_activity blocking
  ON blocking.pid = ANY(pg_blocking_pids(blocked.pid));
```

### Step 3: Check Connection Pool

```bash
# In application logs: "connection pool exhausted" / "no connections available"
# In PostgreSQL:
SELECT count(*), state FROM pg_stat_activity GROUP BY state;
-- If "idle" count >> max_connections limit → pool not returning connections

# Check connection pool metrics
# HikariCP (Java): exposed via JMX or Micrometer
# pgBouncer: SHOW STATS; SHOW POOLS;
```

### Step 4: EXPLAIN ANALYZE

```sql
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT * FROM orders WHERE customer_id = 456 AND status = 'pending';

-- Look for:
-- Seq Scan on large table → missing index
-- Hash Join with millions of rows → consider denormalization
-- Buffers: hit=low, read=high → working set not in memory (need more RAM)
-- Actual Rows >> Estimated Rows → stale statistics → ANALYZE table
```

---

## Problem 5: "Pod keeps restarting (CrashLoopBackOff)"

```bash
# Step 1: Get the crash reason
kubectl describe pod service-xyz | grep -A10 "Last State"
# Exit code 137 = OOM Killed
# Exit code 1   = Application crash
# Exit code 139 = Segfault (SIGSEGV)

# Step 2: Read logs from previous (crashed) container
kubectl logs pod/service-xyz --previous

# Step 3: OOM? Check memory limits
kubectl describe pod service-xyz | grep -A5 "Limits"
kubectl top pod service-xyz  # current usage

# Step 4: Application crash? Check for startup config errors
# Look for: missing env var, DB connection refused at startup, port already in use

# Step 5: Startup probe too aggressive?
kubectl describe pod service-xyz | grep -A10 "Startup"
```

### Common Exit Codes

| Exit Code | Meaning | Debug Approach |
|-----------|---------|---------------|
| 0 | Clean exit | Check if it was supposed to stay running |
| 1 | General error | Check application logs |
| 137 (128+9) | OOM Kill (SIGKILL) | Increase memory limit or find memory leak |
| 139 (128+11) | Segfault | Core dump analysis; JVM crash log |
| 143 (128+15) | SIGTERM — graceful shutdown | Check if shutdown hook ran correctly |

---

## Problem 6: "Message queue is backed up — consumers are behind"

```bash
# Kafka: check consumer group lag
kafka-consumer-groups.sh --bootstrap-server kafka:9092 \
  --describe --group my-consumer-group

# Output:
# TOPIC     PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG
# orders    0          1000            5000            4000  ← 4000 messages behind

# Diagnosis:
# Lag growing → consumers slower than producers → scale consumers
# Lag static → consumers keeping up
# All partitions lagging equally → consumer processing is slow
# One partition lagging → that consumer instance is slow/stuck
```

```bash
# RabbitMQ: check queue depths
rabbitmq-diagnostics queues -q

# Or via management API
curl -u admin:password http://rabbitmq:15672/api/queues/%2F/orders
```

### Root Causes of Consumer Lag

| Root Cause | How to Tell | Fix |
|------------|-------------|-----|
| Consumer is too slow | lag grows continuously | Scale consumer replicas; optimize processing |
| Consumer is stuck/dead | lag grows, 0 consumers listed | Restart consumer; check for deadlock |
| Message processing fails → DLQ | lag stable, DLQ growing | Fix consumer error handling |
| Single partition hotspot | one partition has all the lag | Check partition key distribution |
| Consumer group rebalancing | lag spikes periodically | Reduce rebalance frequency (sticky assignor) |

---

## Problem 7: "Memory leak — pod grows until OOM"

### Step 1: Confirm the Leak

```bash
# Watch memory over time
watch kubectl top pod service-xyz

# Or Grafana: plot container_memory_usage_bytes for the pod
# Pattern: linearly increasing memory that never drops → likely leak
```

### Step 2: Java Heap Analysis

```bash
# Get heap dump from running pod (JVM)
kubectl exec pod/service-xyz -- jmap -dump:format=b,file=/tmp/heap.hprof 1
kubectl cp pod/service-xyz:/tmp/heap.hprof ./heap.hprof

# Analyze with Eclipse MAT or VisualVM
# Look for: largest object holders, shortest path to GC root
```

### Step 3: Go / Python / Node.js Profiling

```bash
# Go: pprof endpoint
curl http://pod:6060/debug/pprof/heap > heap.pprof
go tool pprof heap.pprof

# Python: memory_profiler or tracemalloc
# Node.js: --inspect flag + Chrome DevTools heap snapshot

# Continuous profiling in production: Pyroscope (open-source) or Datadog Continuous Profiler
```

### Step 4: Container-Level Memory

```bash
# Not JVM? Check for:
# - Leaked file handles: /proc/PID/fd count
# - Leaked connections: ss -tp | grep ESTABLISHED | wc -l
# - Growing caches: check if application has unbounded in-memory caches
kubectl exec pod/service-xyz -- cat /proc/1/status | grep VmRSS
```

---

## Problem 8: "Distributed deadlock / stuck requests"

### Symptoms
- Requests hang indefinitely
- Thread/goroutine count grows
- No errors — just no responses

### Approach

```bash
# Java: get thread dump
kubectl exec pod/service-xyz -- kill -3 1    # sends SIGQUIT → thread dump to stdout
kubectl logs pod/service-xyz | grep -A50 "BLOCKED"

# Go: goroutine dump
curl http://pod:6060/debug/pprof/goroutine?debug=1

# Look for: BLOCKED threads waiting for a lock/monitor that another BLOCKED thread holds
# That pattern = deadlock

# Node.js: single-threaded, deadlocks manifest as event loop blocking
# Use clinic.js or 0x for flame graphs
```

### Common Deadlock Causes in Distributed Systems

| Cause | Example | Fix |
|-------|---------|-----|
| Circular service calls | A calls B which calls A | Break cycle; add timeout |
| Database lock ordering | Transaction 1: lock table A then B; Transaction 2: lock B then A | Consistent lock ordering |
| Connection pool + nested calls | Hold connection, make another DB call that needs a connection | Don't hold connections across calls |
| Distributed lock not released | Process crashes holding Redis lock | Always set TTL on locks; use Lua atomic release |

---

## Problem 9: "Works in dev, fails in production"

This is the most frustrating class of problem. Common causes:

| Cause | Detection | Fix |
|-------|-----------|-----|
| Environment variable missing | Startup error in logs | Validate required env vars at startup |
| Different config values | Log config at startup | Make config explicit in logs |
| Production has TLS, dev doesn't | Connection refused with SSL errors | Test with TLS in staging |
| Production volume permissions | Permission denied errors | Match prod volume setup in dev |
| Timezone difference | Timestamp calculation errors | Always use UTC |
| Locale/charset difference | Character encoding errors | Explicit charset in DB connections |
| Different resource constraints | OOM only in prod | Test with prod-equivalent resource limits |
| Clock skew in production | JWT token "not valid yet" errors | Use NTP; add clock skew tolerance |

---

## Problem 10: "Cascading failures — everything is down"

### Symptoms
- Multiple services alarming simultaneously
- Error rates spike across the board
- Looks like a total outage but started as one service

### Approach: Find the Origin

```
Tip: In a cascading failure, the FIRST alert is the closest to root cause.
Everything after that is a symptom.

Look for:
1. What alarmed first? (Grafana annotation timeline)
2. Which service's error rate started rising first?
3. Did a deploy happen just before? (deployment timeline in Grafana)
4. Did a certificate expire? (cert-manager events)
5. Did a node go down? (kubectl get nodes; node exporter metrics)
```

```bash
# Check recent events across the cluster
kubectl get events --all-namespaces --sort-by='.metadata.creationTimestamp' | tail -50

# Check recent deploys
kubectl rollout history deployment --all-namespaces

# Check node health
kubectl describe nodes | grep -A5 "Conditions"

# Did a config change happen?
kubectl get configmaps -n production -o yaml | grep -i "last-applied"
```

### Mitigation Checklist During Cascading Failure

1. **Stop the bleeding**: Roll back the last deployment if recent
2. **Shed load**: Enable maintenance page; drop non-critical traffic
3. **Isolate**: Disable the failing downstream service so upstream can degrade gracefully
4. **Scale**: Add capacity if traffic is the cause
5. **Restore**: Fix root cause, then restore in reverse cascade order

---

## Ephemeral Debug Containers (Kubernetes 1.23+)

Inject a debug container into a running pod without restarting it:

```bash
# Add a debug container with network tools
kubectl debug -it pod/service-xyz \
  --image=nicolaka/netshoot \
  --target=service-container \
  -- bash

# Inside: curl, tcpdump, nslookup, nc, ss, strace all available

# Copy process namespace from running container to debug container
kubectl debug pod/service-xyz -it \
  --image=busybox \
  --share-processes \
  -- sh
# Can now see files and process of the main container
```

---

## Useful kubectl Debugging Commands

```bash
# Get all resources in a namespace at once
kubectl get all -n production

# Watch pod events in real time
kubectl get events -n production -w

# Follow logs across all pods of a deployment
kubectl logs -l app=payment-service -n production --all-containers -f

# Port-forward to access service locally
kubectl port-forward svc/payment-service 8080:80 -n production

# Check pod resource usage
kubectl top pods -n production --sort-by=memory

# Check node pressure
kubectl describe nodes | grep -E "Pressure|Allocatable|cpu|memory"

# Force delete stuck pod
kubectl delete pod service-xyz --grace-period=0 --force

# Get pod YAML with current state
kubectl get pod service-xyz -o yaml | grep -A20 "status:"

# Check rollout status
kubectl rollout status deployment/payment-service -n production

# Rollback last deployment
kubectl rollout undo deployment/payment-service -n production
```

---

## Debugging Without Log Access

Sometimes you have limited access (production restrictions). Alternatives:

1. **Metrics tell the story**: Look at Prometheus/Grafana — error rate, latency, saturation tells you what is wrong even without logs.
2. **Synthetic tests**: Run a known-good request and compare the trace to a known-bad trace.
3. **Canary analysis**: Compare metrics of the new version vs old version.
4. **Distributed tracing sampling**: Even at 1% sampling, you will find traces for the problematic paths.
5. **Audit logs**: Kubernetes audit logs capture API server interactions; immutable and available even if app logs are gone.

---

## Production Debugging Checklist

When a production incident occurs:

- [ ] Note exact start time (check Grafana timeline for deployment annotations)
- [ ] Find the first alerted service (root cause is usually upstream)
- [ ] Collect 5–10 trace_ids from the error window
- [ ] Correlate logs across services using trace_id
- [ ] Check: recent deployment? config change? infrastructure event?
- [ ] Check: is the problem isolated to one pod/node or cluster-wide?
- [ ] Check: did a dependency (DB, external API, cache) degrade first?
- [ ] Capture: thread dumps, heap dumps, or pprof before restart if possible
- [ ] Mitigate first (rollback/scale) before diagnosing deeply
- [ ] Write a postmortem with timeline, root cause, and follow-up actions

---

## Next Steps

- [Distributed Logging](./21-distributed-logging.md) — structured logs and correlation IDs
- [Observability](./16-observability.md) — metrics and traces for proactive debugging
- [Design Patterns](./19-design-patterns.md) — circuit breaker, timeout, bulkhead to prevent the need for debugging cascades
