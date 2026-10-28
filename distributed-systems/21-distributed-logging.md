# Distributed Logging

> **License quick-ref** — ELK Stack (Elasticsearch/Kibana): Elastic License 2.0 (source-available, not OSI-approved since 7.11); Logstash/Beats: Apache-2. OpenSearch (AWS fork): Apache-2 (truly open-source). Grafana Loki: AGPL-3 (open-source). Fluentd/Fluent Bit: Apache-2. Datadog/Splunk/Sumo Logic: commercial SaaS. Vector (Datadog): MPL-2 (open-source).

---

## Why Logging Is Different in Distributed Systems

In a monolith, a single log file captures everything. In a distributed system:

- A user request touches 10–20 services and each writes its own logs
- Logs live on different machines and containers that are constantly recreated
- A pod crash destroys its logs if they are not shipped first
- Correlating "why did this user's checkout fail?" requires stitching logs from API gateway, auth, cart, payment, and inventory services

**The three requirements that don't exist in monoliths:**
1. **Centralized aggregation** — all logs in one searchable place
2. **Correlation** — a trace/request ID that links all logs for one request
3. **Structured format** — machine-readable JSON so logs are queryable, not just grep-able

---

## The Logging Pipeline

```
Application
    │ writes logs (stdout/stderr)
    ▼
Log Shipper (per node/pod)
    │ Filebeat / Fluent Bit / Promtail / Vector
    ▼
Log Aggregator (optional, for buffering/enrichment)
    │ Logstash / Fluentd
    ▼
Log Store + Index
    │ Elasticsearch / OpenSearch / Loki / CloudWatch
    ▼
Visualization + Query + Alert
    │ Kibana / OpenSearch Dashboards / Grafana / Splunk
```

---

## Structured Logging

Never write free-text logs in production. Always emit JSON.

### Bad (unstructured)
```
2026-06-06 14:35:02 ERROR Payment failed for order 123 - card declined
```
You cannot filter by `order_id` or `error_code` across millions of lines.

### Good (structured JSON)
```json
{
  "timestamp": "2026-06-06T14:35:02.123Z",
  "level": "error",
  "service": "payment-service",
  "version": "2.3.1",
  "environment": "production",
  "trace_id": "abc123def456789",
  "span_id": "xyz789",
  "user_id": "user-456",
  "order_id": "order-123",
  "message": "Payment failed",
  "error_code": "card_declined",
  "card_last4": "4242",
  "amount_cents": 9999,
  "duration_ms": 234,
  "upstream_service": "stripe",
  "upstream_status": 402
}
```

Every field is independently queryable and aggregatable.

### Standard Fields (Always Include)

| Field | Why |
|-------|-----|
| `timestamp` | ISO 8601 UTC — enables time-range queries |
| `level` | error/warn/info/debug — filter by severity |
| `service` | Which service emitted this |
| `version` | Which deployment version (correlate with deploys) |
| `trace_id` | Links all logs for one distributed request |
| `span_id` | Links logs within one service call |
| `environment` | production/staging/dev |
| `message` | Human-readable description |
| `error` | Error type/code (structured, not stack trace in message) |
| `duration_ms` | For any operation that has a measurable duration |

### What NOT to Log

```
NEVER log:
- Passwords, tokens, API keys
- Full credit card numbers (only last 4)
- PII that violates GDPR/HIPAA (SSN, DOB, full name in some contexts)
- Large binary payloads (use a reference ID instead)
- Secrets from environment variables
```

---

## Correlation IDs — The Most Important Practice

A correlation ID (also called trace ID or request ID) is a unique identifier assigned at the entry point and propagated through every service call for that request.

### How It Works

```
User Request → API Gateway
  → Generates: X-Request-ID: abc-123
  → Passes header to all downstream calls

Order Service (receives X-Request-ID: abc-123)
  → Logs: {"trace_id": "abc-123", "message": "order created"}
  → Passes X-Request-ID to Payment Service

Payment Service (receives X-Request-ID: abc-123)
  → Logs: {"trace_id": "abc-123", "message": "payment charged"}

Now in your log system:
  FILTER trace_id = "abc-123" → ALL logs for this request, in order
```

### Implementation

```python
# FastAPI middleware
import uuid
from fastapi import Request

@app.middleware("http")
async def add_correlation_id(request: Request, call_next):
    correlation_id = request.headers.get("X-Request-ID", str(uuid.uuid4()))
    # Store in context var so all log statements in this request include it
    token = correlation_id_var.set(correlation_id)
    response = await call_next(request)
    response.headers["X-Request-ID"] = correlation_id
    correlation_id_var.reset(token)
    return response

# Logger picks up correlation ID from context
import structlog
log = structlog.get_logger()

def process_order(order_id):
    log.info("processing order", order_id=order_id, trace_id=get_correlation_id())
```

```java
// Spring Boot MDC (Mapped Diagnostic Context)
@Component
public class CorrelationFilter implements Filter {
    public void doFilter(ServletRequest req, ServletResponse res, FilterChain chain) {
        String correlationId = ((HttpServletRequest) req).getHeader("X-Request-ID");
        if (correlationId == null) correlationId = UUID.randomUUID().toString();
        MDC.put("trace_id", correlationId);
        ((HttpServletResponse) res).setHeader("X-Request-ID", correlationId);
        try {
            chain.doFilter(req, res);
        } finally {
            MDC.clear();
        }
    }
}
// logback-spring.xml includes %X{trace_id} in pattern
```

### OpenTelemetry Auto-Injection

If you use OpenTelemetry, trace/span IDs are automatically injected into logs when using a compatible logging library (logback with OTel appender, structlog, etc.):

```json
{
  "trace_id": "abc123",
  "span_id": "def456",
  "trace_flags": "01"
}
```

These IDs link directly to distributed traces in Jaeger/Tempo — click a log line, jump to the trace.

---

## Log Levels: When to Use Each

| Level | When to Use | Example |
|-------|-------------|---------|
| `ERROR` | Something failed that requires human attention or retry | Payment processing failed, DB connection lost |
| `WARN` | Something unexpected happened but the system recovered | Cache miss fallback used, deprecated API called, retry succeeded |
| `INFO` | Normal significant business events | Order placed, user logged in, job completed |
| `DEBUG` | Developer-useful detail for understanding flow | Entering method, computed value, cache hit/miss |
| `TRACE` | Very fine-grained (usually disabled in production) | SQL queries, HTTP headers, serialized payloads |

**Production default**: `INFO` level. Enable `DEBUG` per-service via runtime config reload, not redeploy.

**Never log ERROR for recoverable situations** — it pollutes alert thresholds. A retry that eventually succeeds should be WARN, not ERROR.

---

## Log Shippers Comparison

| Tool | Language | CPU/Memory | Protocol | Best For |
|------|----------|-----------|---------|---------|
| **Fluent Bit** | C | Very low (~1MB RAM) | Multiple | K8s DaemonSet, resource-constrained |
| **Filebeat** | Go | Low (~30MB RAM) | Elasticsearch native | ELK stack |
| **Promtail** | Go | Low | Loki native | Grafana Loki |
| **Vector** | Rust | Low, high throughput | Multiple | Flexible transformation pipelines |
| **Fluentd** | Ruby | Higher (~40MB) | Multiple | Complex transformation, many plugins |
| **Logstash** | Java (JVM) | High (~500MB) | Multiple | Heavy transformation, grok parsing |

**Recommended for Kubernetes**: **Fluent Bit** as DaemonSet (lightweight) shipping to a Loki/Elasticsearch aggregator. Use Vector for complex enrichment.

### Fluent Bit Kubernetes Config

```yaml
# fluent-bit configmap
[INPUT]
    Name tail
    Path /var/log/containers/*.log
    Parser docker
    Tag kube.*
    Refresh_Interval 5

[FILTER]
    Name kubernetes
    Match kube.*
    Kube_URL https://kubernetes.default.svc:443
    Merge_Log On           # parse JSON logs from containers
    K8s-Logging.Parser On

[FILTER]
    Name record_modifier
    Match *
    Record environment production
    Record cluster my-cluster

[OUTPUT]
    Name loki
    Match *
    Host loki.monitoring.svc.cluster.local
    Port 3100
    Labels job=fluentbit, pod=$kubernetes['pod_name'], namespace=$kubernetes['namespace_name']
```

---

## ELK Stack Deep Dive

**E**lasticsearch + **L**ogstash + **K**ibana (+ Beats)

### Elasticsearch

Distributed search and analytics engine. Stores logs as JSON documents in indices.

```
Index: logs-2026.06.06      (daily index rotation)
Shard 1 ─ Replica 1
Shard 2 ─ Replica 2
Shard 3 ─ Replica 3
```

**Index Lifecycle Management (ILM)**: Automatically roll over and delete old indices:
- Hot: fast SSD, full indexing — recent logs (7 days)
- Warm: slower disk, read-only — less recent (30 days)
- Cold: object storage — historical (1 year)
- Delete: purge

**Queries (Elasticsearch DSL):**
```json
GET /logs-*/_search
{
  "query": {
    "bool": {
      "must": [
        { "term": { "level": "error" } },
        { "term": { "service": "payment-service" } },
        { "range": { "@timestamp": { "gte": "now-1h" } } }
      ]
    }
  },
  "aggs": {
    "errors_per_minute": {
      "date_histogram": { "field": "@timestamp", "calendar_interval": "1m" }
    }
  }
}
```

### Logstash Pipeline

```ruby
# logstash.conf
input {
  beats { port => 5044 }
}

filter {
  if [message] =~ /^\{/ {
    json { source => "message" }
  }

  # Parse timestamp
  date {
    match => ["timestamp", "ISO8601"]
    target => "@timestamp"
    remove_field => ["timestamp"]
  }

  # Enrich: add geo from IP
  geoip { source => "client_ip" }

  # Drop health check logs
  if [request_path] == "/healthz" { drop {} }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "logs-%{service}-%{+YYYY.MM.dd}"
  }
}
```

### Kibana Queries (KQL)

```
level: error AND service: payment-service
level: error AND NOT kubernetes.namespace: kube-system
trace_id: "abc-123"
duration_ms > 1000 AND level: warn
message: *timeout* AND service: (order-service OR payment-service)
```

---

## Grafana Loki — Lightweight Alternative

Loki stores only **labels** (metadata), not log content. This makes it ~10x cheaper than Elasticsearch but full-text search is slower.

```
Elasticsearch: indexes every word in every log line → expensive, fast search
Loki: indexes only labels (service, pod, namespace) → cheap, scan-on-read
```

### LogQL Queries

```logql
# Basic label filter
{namespace="production", app="payment-service"}

# String filter
{app="payment-service"} |= "error"
{app="payment-service"} != "health"

# JSON parsing + filter
{app="payment-service"} | json | level="error" | duration_ms > 1000

# Pattern extraction
{app="nginx"} | pattern `<ip> - - [<_>] "<method> <uri> HTTP/<_>" <status> <_>`
              | status >= 500

# Rate of error lines per minute
rate({app="payment-service"} |= "error" [1m])

# Top services by error count
sum by (app) (
  count_over_time({namespace="production"} |= "error" [5m])
)
```

### Loki vs Elasticsearch Decision

| | Loki | Elasticsearch |
|-|------|---------------|
| **Cost** | ~10x cheaper at scale | Expensive (index everything) |
| **Full-text search** | Slower (scan on read) | Fast (pre-indexed) |
| **Setup complexity** | Low | High |
| **Schema** | Schema-free, labels | Mapping/index template |
| **Query language** | LogQL | KQL / Elasticsearch DSL |
| **Alerting** | Grafana built-in | ElastAlert or Kibana rules |
| **Best for** | K8s logs with Grafana stack | Complex search, compliance, legacy ELK users |

---

## Log Aggregation Patterns

### Fan-In Architecture (Kubernetes)

```
Node 1: [Pod A, Pod B] → Fluent Bit DaemonSet → Loki
Node 2: [Pod C, Pod D] → Fluent Bit DaemonSet → Loki
Node 3: [Pod E, Pod F] → Fluent Bit DaemonSet → Loki
```

One Fluent Bit pod per node collects all container logs from that node.

### Buffering and Backpressure

Log shippers must handle backpressure when the aggregator is slow:

```ini
# Fluent Bit memory buffer
[SERVICE]
    storage.type     filesystem    # spill to disk when memory full
    storage.path     /var/log/flb-storage/
    storage.max_chunks_up  128

[OUTPUT]
    storage.total_limit_size 10G   # max disk usage for buffered logs
```

---

## Alerting on Logs

### Log-Based Alerts (Loki / Grafana)

```yaml
# Alert when error rate spikes
- name: high-error-rate
  expr: |
    sum(rate({namespace="production"} |= "error" [5m])) by (app)
    /
    sum(rate({namespace="production"} [5m])) by (app)
    > 0.05
  for: 5m
  annotations:
    summary: "High error rate in {{ $labels.app }}"
```

### Log-Based Alerts (ElastAlert — for ELK)

```yaml
name: Payment Service Errors
type: frequency
index: logs-payment-*
num_events: 10
timeframe:
  minutes: 5
filter:
  - term:
      level: "error"
alert:
  - slack
slack_webhook_url: "https://hooks.slack.com/..."
```

---

## Log Retention and Compliance

| Regulation | Minimum Retention | Notes |
|------------|------------------|-------|
| GDPR | N/A (minimize PII in logs) | Must be able to delete PII on request |
| PCI-DSS | 12 months (3 months accessible) | Payment-related logs |
| HIPAA | 6 years | Healthcare logs |
| SOC 2 | 12 months | Audit trail |

**Index Lifecycle Management** automates retention:
```json
{
  "policy": {
    "phases": {
      "hot":    { "actions": { "rollover": { "max_age": "1d" } } },
      "warm":   { "min_age": "7d",  "actions": { "readonly": {} } },
      "cold":   { "min_age": "30d", "actions": { "freeze": {} } },
      "delete": { "min_age": "365d", "actions": { "delete": {} } }
    }
  }
}
```

---

## Production Checklist

- [ ] All services log structured JSON (not free-text)
- [ ] Correlation/trace ID in every log line
- [ ] No PII, credentials, or tokens in logs
- [ ] Log shipper deployed as DaemonSet (Fluent Bit / Filebeat)
- [ ] Log buffer-to-disk configured to survive aggregator restarts
- [ ] Index lifecycle / retention policy configured
- [ ] Error-rate log alerts configured
- [ ] Log levels configurable at runtime without redeploy
- [ ] Health check requests filtered from logs (reduce noise)
- [ ] Separate log streams per environment (prod/staging do not mix)
- [ ] Debug logs can be temporarily enabled per service without full redeploy
- [ ] Container logs written to stdout/stderr (not files inside container)

---

## Next Steps

- [Observability](./16-observability.md) — metrics and tracing alongside logging
- [Debugging Distributed Systems](./22-debugging-distributed-systems.md) — using logs to diagnose production issues
- [Security](./18-security.md) — securing log pipelines, audit logging
