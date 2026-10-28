# Observability

## What Is Observability?

Observability is the ability to understand the internal state of a system by examining its external outputs. In distributed systems, you cannot attach a debugger — you need to instrument your code and infrastructure so that when things go wrong, you can diagnose the cause from collected data.

The three pillars of observability:

| Pillar | What It Answers | Example |
|--------|----------------|---------|
| **Logs** | What happened and when? | "Payment failed for order 123 at 14:35:02 — invalid card" |
| **Metrics** | How is the system performing right now? | "Payment service error rate: 2.3%" |
| **Traces** | Why is this request slow? | "Order request spent 800ms in inventory check" |

---

## The OpenTelemetry Standard

OpenTelemetry (OTel) is the CNCF standard for instrumentation. It provides a single API/SDK to collect logs, metrics, and traces — independent of the backend you send them to.

```
Application (instrumented with OTel SDK)
          ↓
  OTel Collector (agent or gateway)
          ↓
  ┌────────────────────────────────┐
  │ Jaeger  │ Prometheus │ Loki    │
  │ (traces)│  (metrics) │ (logs)  │
  └────────────────────────────────┘
```

**Why it matters**: Instrument once, switch backends without code changes.

---

## Logging

### Structured Logging

Always log in JSON. Free-text logs are unqueryable at scale.

```json
{
  "timestamp": "2026-06-06T14:35:02.123Z",
  "level": "error",
  "service": "payment-service",
  "trace_id": "abc123def456",
  "span_id": "789xyz",
  "user_id": "user-456",
  "order_id": "order-789",
  "message": "Card charge failed",
  "error": "insufficient_funds",
  "duration_ms": 234
}
```

Key fields to always include:
- `trace_id` and `span_id` — link logs to distributed traces
- `service` and `version` — know which service/version logged this
- `level` — error/warn/info/debug
- `timestamp` in ISO 8601 UTC

### The ELK Stack

**Elasticsearch + Logstash + Kibana** — the classic self-hosted log stack.

```
Application → Logstash (parse/transform) → Elasticsearch (index + store) → Kibana (UI)
           or Filebeat (lightweight shipper) ↗
```

- **Elasticsearch**: Distributed search and analytics engine. Full-text search + aggregations.
- **Logstash**: Parse, transform, enrich logs (Grok patterns, filters).
- **Beats (Filebeat)**: Lightweight log shipper from each node to Logstash/Elasticsearch.
- **Kibana**: Query, visualize, and alert on log data.

**Disadvantages**: Expensive to operate at scale (Elasticsearch is memory-hungry). Index management complexity.

### Loki (Grafana Labs)

A lightweight, horizontally scalable log aggregation system inspired by Prometheus.

```
Application → Promtail/Alloy (agent) → Loki → Grafana
```

**Key difference from ELK**: Loki does **not** index log content — only metadata labels. This makes it much cheaper to store and operate, but full-text search is slower.

```logql
# Query: all error logs from payment service in the last 5 minutes
{app="payment-service"} |= "error"

# Parse JSON and filter
{app="payment-service"} | json | level="error" | line_format "{{.message}}"

# Rate of errors over time
rate({app="payment-service"} |= "error" [1m])
```

**Choose Loki when**: You already use Prometheus/Grafana and want unified observability with lower cost.
**Choose ELK when**: You need rich full-text search, complex field extraction at query time.

### Cloud-Native Log Services

| Service | Platform | Notes |
|---------|----------|-------|
| AWS CloudWatch Logs | AWS | Native ECS/Lambda integration |
| Google Cloud Logging | GCP | Integrated with GKE |
| Azure Monitor / Log Analytics | Azure | KQL query language |
| Datadog Logs | Any | Full-stack observability platform |
| Splunk | Any | Enterprise, expensive, powerful |

---

## Metrics

### Prometheus

The standard for Kubernetes metrics. A pull-based time-series database.

**How it works:**
1. Services expose metrics at `/metrics` endpoint in Prometheus text format
2. Prometheus scrapes these endpoints on a schedule
3. Metrics stored in local TSDB
4. Grafana queries Prometheus via PromQL

```
# Example /metrics output (Prometheus text format)
http_requests_total{method="GET",status="200"} 15234
http_requests_total{method="POST",status="500"} 42
http_request_duration_seconds{quantile="0.99"} 0.234
```

### PromQL (Prometheus Query Language)

```promql
# Error rate over last 5 minutes
rate(http_requests_total{status=~"5.."}[5m])
  /
rate(http_requests_total[5m])

# P99 latency per service
histogram_quantile(0.99,
  sum by (service, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)

# CPU usage per pod
sum by (pod) (rate(container_cpu_usage_seconds_total[5m]))
```

### Instrumentation with Prometheus

```python
from prometheus_client import Counter, Histogram, start_http_server

REQUEST_COUNT = Counter('http_requests_total', 'Total HTTP requests', ['method', 'endpoint', 'status'])
REQUEST_LATENCY = Histogram('http_request_duration_seconds', 'Request latency', ['endpoint'])

@app.route('/api/orders', methods=['POST'])
def create_order():
    with REQUEST_LATENCY.labels(endpoint='/api/orders').time():
        result = process_order()
        REQUEST_COUNT.labels(method='POST', endpoint='/api/orders', status='200').inc()
        return result
```

### The Four Golden Signals

| Signal | What | PromQL Example |
|--------|------|----------------|
| **Latency** | Time to serve requests | `histogram_quantile(0.99, ...)` |
| **Traffic** | Request rate | `rate(http_requests_total[5m])` |
| **Errors** | Error rate | `rate(http_errors_total[5m])` |
| **Saturation** | How "full" the service is | CPU, memory, queue depth |

### Grafana

The visualization layer. Query multiple data sources (Prometheus, Loki, Jaeger, Elasticsearch, databases).

```yaml
# Dashboard as code (Grafonnet / JSON)
# Alert rule example
groups:
- name: myapp
  rules:
  - alert: HighErrorRate
    expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "High error rate on {{ $labels.service }}"
      description: "Error rate is {{ $value | humanize }}"
```

### Prometheus Alertmanager

Routes alerts to PagerDuty, Slack, email, OpsGenie:

```yaml
# alertmanager.yml
route:
  group_by: ['alertname', 'service']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 12h
  receiver: 'slack-critical'
  routes:
  - match:
      severity: critical
    receiver: 'pagerduty-critical'

receivers:
- name: slack-critical
  slack_configs:
  - api_url: 'https://hooks.slack.com/services/...'
    channel: '#alerts'
    text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'

- name: pagerduty-critical
  pagerduty_configs:
  - service_key: 'pd-service-key'
```

---

## Distributed Tracing

### Why Traces?

A single user request in a microservices system touches dozens of services. A trace shows the full journey:

```
User Request (trace_id: abc123) — total: 523ms
│
├── API Gateway — 5ms
├── Auth Service — 12ms
├── Order Service — 498ms
│   ├── DB Query (getUser) — 8ms
│   ├── Inventory Service — 420ms   ← SLOW
│   │   └── DB Query (checkStock) — 415ms  ← root cause
│   └── DB Insert (createOrder) — 32ms
└── Notification Service — 8ms (async)
```

Without traces, you'd see "order service is slow" but not know it's the inventory DB query.

### Jaeger

Open-source distributed tracing system (CNCF). Originally built at Uber.

```
App (OTel SDK) → Jaeger Collector → Cassandra/Elasticsearch → Jaeger UI
```

### Zipkin

Twitter's open-source tracing system. Simpler, more mature, smaller footprint than Jaeger.

### Tempo (Grafana)

Cost-effective tracing backend that stores traces in object storage (S3/GCS). Integrates natively with Grafana for unified logs/metrics/traces.

### OpenTelemetry Tracing

```python
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter

tracer = trace.get_tracer("payment-service")

def process_payment(order_id):
    with tracer.start_as_current_span("process_payment") as span:
        span.set_attribute("order.id", order_id)
        result = charge_card(order_id)
        span.set_attribute("payment.status", result.status)
        return result
```

---

## Unified Observability Stacks

### Prometheus + Grafana + Loki + Tempo (PLG/LGTM Stack)

The fully open-source, cloud-native stack:

```
Metrics → Prometheus → Grafana
Logs    → Loki       → Grafana
Traces  → Tempo      → Grafana
Events  → OTel Collector (fan-out)
```

Deploy via Helm:
```bash
helm repo add grafana https://grafana.github.io/helm-charts
helm install lgtm grafana/lgtm-distributed -f values.yaml
```

### Datadog

Fully managed SaaS observability platform. Unified UI for logs, metrics, traces, APM, synthetic monitoring, real user monitoring.

**Pros**: Excellent correlation between signals, AI-powered anomaly detection, low operational burden.
**Cons**: Expensive at scale.

### New Relic, Dynatrace

Enterprise APM platforms. Similar to Datadog — full-stack, managed, expensive.

### OpenSearch (ELK successor)

Open-source fork of Elasticsearch after the Elastic license change. Compatible with existing Kibana / Logstash tooling.

---

## Kubernetes-Specific Monitoring

### kube-state-metrics

Exposes Kubernetes object state as Prometheus metrics:

```
kube_deployment_status_replicas_available
kube_pod_container_status_restarts_total
kube_node_status_condition
```

### node-exporter

Exposes node hardware and OS metrics:

```
node_cpu_seconds_total
node_memory_MemAvailable_bytes
node_disk_io_time_seconds_total
```

### Kubernetes Dashboard Alerts

```yaml
- alert: PodCrashLooping
  expr: rate(kube_pod_container_status_restarts_total[15m]) > 0
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "Pod {{ $labels.namespace }}/{{ $labels.pod }} is crash looping"

- alert: NodeMemoryHigh
  expr: (node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes > 0.9
  for: 10m
  labels:
    severity: warning
```

---

## SLO, SLI, SLA

| Term | Definition | Example |
|------|-----------|---------|
| **SLI** (Service Level Indicator) | A metric measuring a service behavior | 99th percentile latency |
| **SLO** (Service Level Objective) | Target value for an SLI | P99 latency < 500ms for 99.9% of requests |
| **SLA** (Service Level Agreement) | Contractual commitment to external customers | "99.9% uptime, or credits issued" |
| **Error Budget** | How much unreliability is allowed | 43.8 minutes/month for 99.9% SLO |

### Error Budget Alerting

```yaml
# Alert when burning through error budget too fast
- alert: HighErrorBudgetBurn
  expr: |
    (
      rate(http_requests_total{status=~"5.."}[1h])
      /
      rate(http_requests_total[1h])
    ) > (0.001 * 14.4)   # burning 14.4x the allowed rate
  for: 5m
  labels:
    severity: critical
```

---

## Production Checklist

- [ ] Structured JSON logging in all services
- [ ] Trace ID propagated through all service calls and present in logs
- [ ] Four Golden Signals dashboards for each service
- [ ] Alerting on error rate, latency P99, and saturation thresholds
- [ ] PagerDuty/OpsGenie integration for critical alerts (not just Slack)
- [ ] Runbooks linked in alert annotations
- [ ] SLOs defined and error budget dashboards in Grafana
- [ ] Distributed tracing sampling rate configured (1–10% for high-volume)
- [ ] Log retention policy: hot (7 days), warm (30 days), cold (1 year)
- [ ] Dashboards as code (Grafonnet / Terraform) — no manual dashboard changes
- [ ] OTel Collector deployed as a Daemonset or sidecar for log/metric collection
- [ ] Alert deduplication and grouping configured in Alertmanager

---

## Next Steps

- [Design Patterns](./19-design-patterns.md) — circuit breaker, bulkhead patterns measured via observability
- [Service Mesh](./14-service-mesh.md) — mesh generates golden signal metrics automatically
- [Kubernetes](./containerization/kubernetes.md) — K8s-specific monitoring (kube-state-metrics, node-exporter)
