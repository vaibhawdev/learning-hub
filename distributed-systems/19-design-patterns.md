# Design Patterns for Distributed Systems

## Why Patterns Matter

Distributed systems face recurring failure modes: network latency, partial failures, cascading outages, data inconsistency. Patterns are battle-tested solutions to these recurring problems. Knowing them lets you recognize the problem and apply the right solution rather than reinventing it.

---

## Resilience Patterns

### Circuit Breaker

Prevents cascading failures by stopping requests to a service that is failing, giving it time to recover.

```
States:
CLOSED → requests pass through normally
    ↓ (N failures in window)
OPEN → all requests fail fast (no actual call made)
    ↓ (after timeout)
HALF-OPEN → test request goes through
    ↓ success → CLOSED
    ↓ failure → OPEN again
```

```python
from circuitbreaker import circuit

@circuit(failure_threshold=5, recovery_timeout=30, expected_exception=TimeoutError)
def call_payment_service(order_id):
    return payment_client.charge(order_id)
```

**When to use**: Any synchronous service-to-service call that could fail repeatedly.

**Libraries**: Resilience4j (Java), Polly (.NET), pybreaker (Python), opossum (Node.js).

**Metrics to monitor**: Circuit state changes, open circuit duration, failure rate.

---

### Retry with Exponential Backoff

Automatically retry failed operations, waiting longer between each attempt.

```python
def retry(fn, max_attempts=5, base_delay_ms=100):
    for attempt in range(max_attempts):
        try:
            return fn()
        except RetriableError as e:
            if attempt == max_attempts - 1:
                raise
            delay = (base_delay_ms * 2**attempt + random.randint(0, 100)) / 1000
            time.sleep(delay)
```

**Jitter**: Add random delay offset to prevent all retrying clients hitting the server at the same time (retry storm).

**Retry on**: Network timeouts, HTTP 429 (rate limited), HTTP 503 (service unavailable), connection refused.

**Don't retry on**: HTTP 400 (bad request — retrying won't help), HTTP 404 (not found), business logic errors.

**Always combine with**: Circuit breaker (don't retry when circuit is open) and idempotency keys (don't double-charge).

---

### Bulkhead

Isolate components so that a failure in one does not exhaust resources for all others. Named after ship compartments that prevent the whole vessel from sinking when one section floods.

```
Without Bulkhead:
All API calls share one thread pool
  Slow Inventory calls fill up the pool
  → Payment calls also blocked
  → Checkout completely down

With Bulkhead:
  Thread pool A (20 threads): Inventory calls
  Thread pool B (20 threads): Payment calls
  Thread pool C (20 threads): User profile calls
  → Inventory pool exhausted → only Inventory slow
  → Payment still works
```

```java
// Resilience4j bulkhead
BulkheadConfig config = BulkheadConfig.custom()
    .maxConcurrentCalls(20)
    .maxWaitDuration(Duration.ofMillis(100))
    .build();

Bulkhead inventoryBulkhead = Bulkhead.of("inventory", config);
```

**In Kubernetes**: Set resource limits per container to prevent one pod consuming all node resources.

**In Istio**: Connection pool limits in DestinationRule.

---

### Timeout

Every external call must have a deadline. Without timeouts, slow upstream services exhaust thread pools:

```python
response = requests.get(
    url,
    timeout=(3, 10)  # (connect timeout, read timeout) in seconds
)
```

**Setting timeouts**: P99 latency of the dependency × 2–3. If the payment API P99 is 500ms, set timeout to 1s.

**Cascading timeouts**: If A calls B which calls C, B's timeout must be shorter than A's timeout (leave time for B to process C's response and return to A).

---

### Fallback

Return a degraded but acceptable response when the main path fails:

```python
def get_recommendations(user_id):
    try:
        return recommendation_service.get(user_id)  # personalized
    except CircuitBreakerOpen:
        return cache.get("popular_items")  # fallback: popular items
    except Exception:
        return DEFAULT_RECOMMENDATIONS    # static fallback
```

**Graceful degradation**: The system continues functioning at reduced quality rather than failing completely.

---

## Data Patterns

### CQRS (Command Query Responsibility Segregation)

Separate the model used for reading data from the model used for writing data.

```
Command Side (Write):
  User → Command → Command Handler → Event → Event Store
  Optimized for: write throughput, consistency

Query Side (Read):
  User → Query → Query Handler → Read Model (denormalized)
  Optimized for: read performance, specific views
```

**Why**: The shape of data needed for reads is often very different from the shape used for writes. A dashboard needs 15 joined fields pre-aggregated; writes need normalized, consistent data.

**Eventual consistency**: Read models are updated asynchronously from write events — there's a propagation delay.

**Examples**:
- Write: `CREATE TABLE orders (id, user_id, items_json, total)`
- Read: `CREATE TABLE order_summaries (order_id, user_name, item_count, total, status)` — denormalized for fast reads

---

### Event Sourcing

Instead of storing current state, store the full sequence of state-changing events. Current state is derived by replaying events.

```
Traditional:
orders table: { id: 1, status: "shipped", items: [...], total: 99.99 }
(history lost on update)

Event Sourced:
event_store: [
  { order_id: 1, type: "OrderPlaced",   data: { items: [...], total: 99.99 } },
  { order_id: 1, type: "PaymentCharged", data: { amount: 99.99 } },
  { order_id: 1, type: "OrderShipped",   data: { tracking: "1Z..." } },
]
Current state = replay all events
```

**Benefits**:
- Full audit history
- Temporal queries ("what did the order look like yesterday?")
- Derive new projections from historical events (add a new read model from day 1)
- Natural fit for event-driven systems

**Drawbacks**:
- Event schema evolution is hard (old events must still be replayable)
- Rebuilding state for complex entities requires replaying many events (use snapshots)
- Eventual consistency between write model and read projections

---

### Saga Pattern

See [Distributed Transactions](./17-distributed-transactions.md) — managing multi-step operations across services with compensating transactions.

---

### Outbox Pattern

See [Distributed Transactions](./17-distributed-transactions.md) — reliably publishing events alongside database writes.

---

## Structural Patterns

### Sidecar

Attach a secondary container to the main application container, handling cross-cutting concerns without changing the application:

```
┌─────────────────────────────────────┐
│              Pod                     │
│  ┌─────────────┐  ┌──────────────┐  │
│  │  App        │  │  Sidecar     │  │
│  │  Container  │  │  (Envoy/log  │  │
│  │             │  │   shipper)   │  │
│  └─────────────┘  └──────────────┘  │
└─────────────────────────────────────┘
```

**Uses**: Proxy (service mesh), log collection, metrics collection, config reload, certificate rotation.

---

### Ambassador

A variation of Sidecar specifically for handling network communication:

```
App → Ambassador Sidecar → (handles: retries, circuit breaking, auth, logging) → External Service
```

The application makes simple local calls; the ambassador handles all network complexity.

---

### Strangler Fig

Migrate a monolith to microservices incrementally without big-bang rewrites.

```
Phase 1: Route ALL traffic through facade → Monolith
Phase 2: Route /api/payments → Payment Service; rest → Monolith
Phase 3: Route /api/payments + /api/orders → New services; rest → Monolith
...
Phase N: Monolith is empty → decommission
```

The facade (API Gateway or proxy) intercepts traffic and routes to either the monolith or new services based on path/headers.

**Key rule**: Never call the monolith from new microservices. New services are self-contained.

---

### Anti-Corruption Layer (ACL)

A translation layer between systems with different models, preventing external models from "corrupting" the internal domain model:

```
External System (legacy API with messy model)
    ↓
ACL (translates: maps fields, normalizes, validates)
    ↓
Your Domain Model (clean, consistent)
```

Prevents external system concepts from leaking into your domain logic.

---

## Deployment and Operations Patterns

### Blue-Green Deployment

See [CI/CD Patterns](./devops/ci-cd-patterns.md) — run two environments and switch traffic atomically.

### Canary Deployment

See [CI/CD Patterns](./devops/ci-cd-patterns.md) — gradually shift traffic to new version.

### Feature Flags

Control feature exposure at runtime without deploying new code. See [CI/CD Patterns](./devops/ci-cd-patterns.md).

---

## Scalability Patterns

### Database Per Service

Each microservice owns its own database and schema. No service accesses another service's database directly.

```
Order Service → orders_db (PostgreSQL)
User Service  → users_db (PostgreSQL)
Product Service → products_db (MongoDB)
Analytics Service → analytics_db (ClickHouse)
```

**Why**: Enables polyglot persistence, prevents coupling through the database layer, allows independent scaling and migration.

**Challenge**: Joins across services must be done in application code or via denormalization.

### Shared Database (Anti-Pattern)

Multiple services sharing one database. Common in legacy system migrations. Avoid in new design — tight coupling prevents independent deployment and scaling.

---

### Backends for Frontends (BFF)

A dedicated API layer per client type (web, mobile, IoT):

```
Mobile App → Mobile BFF → (aggregates from) → Services A, B, C
Web App    → Web BFF    → (aggregates from) → Services A, B, D
IoT Device → IoT BFF   → (aggregates from) → Services B, E
```

Each BFF is tailored to the specific client's needs (data shape, performance requirements). Replaces a single general-purpose API that tries to serve all clients.

---

### API Composition

When a request needs data from multiple services, an aggregator fetches from all and combines:

```
Client → API Composition Layer → User Service → combine → Response
                               → Order Service ↗
                               → Product Service ↗
```

For latency, fetch from all services in parallel.

For consistency, this is eventually consistent — data from different services may reflect different points in time.

---

## Messaging Patterns

### Dead Letter Queue

Messages that cannot be processed after N retries go to a DLQ for inspection:

```
Queue → Consumer fails 3 times → DLQ
                                   ↓
                            Human inspection / replay
```

Always monitor DLQ depth and alert when messages accumulate.

### Competing Consumers

Multiple consumers read from the same queue, each processing a subset:

```
Queue → Consumer 1 (processes msg 1, 3, 5)
      → Consumer 2 (processes msg 2, 4, 6)
```

Enables horizontal scaling of consumers. RabbitMQ work queues, Kafka consumer groups.

### Message Deduplication (Idempotent Consumer)

At-least-once delivery means the same message may arrive twice. Deduplicate:

```python
def process(message):
    if db.exists("processed_messages", message.id):
        return  # already processed
    
    with db.transaction():
        # process message
        db.insert("processed_messages", message.id)
```

---

## Summary: Pattern Selection Guide

| Problem | Pattern |
|---------|---------|
| Slow/failing dependency brings down service | Circuit Breaker + Bulkhead |
| Network timeouts and transient failures | Retry with Exponential Backoff + Jitter |
| All service calls must handle failure | Fallback + Graceful Degradation |
| Read and write scales differ | CQRS |
| Need full audit history and time-travel | Event Sourcing |
| Multi-service write must be atomic | Saga + Outbox Pattern |
| Migrating monolith to microservices | Strangler Fig |
| External API has messy model | Anti-Corruption Layer |
| Multiple client types with different needs | Backends for Frontends |
| Cross-cutting concerns (logging, proxy) | Sidecar |
| Service calling complex external dependencies | Ambassador |

---

## Combining Patterns

Patterns work together. A typical production order service might use:

```
User Places Order:
  API Gateway (rate limiting, auth)
    → Order Service (CQRS write side)
      → Saga Orchestrator (multi-step transaction)
        → Payment Service (Circuit Breaker + Retry)
        → Inventory Service (Circuit Breaker + Bulkhead)
      → Outbox (event publishing guarantee)
        → Kafka (event streaming)
          → Order Read Model Updater (CQRS read side)
          → Notification Service
          → Analytics Pipeline
```

---

## Next Steps

- [Distributed Transactions](./17-distributed-transactions.md) — Saga and Outbox in depth
- [Observability](./16-observability.md) — measuring pattern effectiveness (circuit state, retry rate)
- [Messaging Kafka](./messaging/kafka.md) — event sourcing backbone
- [Core Concepts](./02-core-concepts-cap-theorem.md) — theoretical grounding for these patterns
