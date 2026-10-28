# Distributed Transactions

## The Problem

In a monolith with a single database, ACID transactions are straightforward:

```sql
BEGIN;
UPDATE accounts SET balance = balance - 100 WHERE id = 1;
UPDATE accounts SET balance = balance + 100 WHERE id = 2;
COMMIT;  -- atomic: both succeed or both fail
```

In a distributed system, an operation might span:
- Multiple microservices (Order Service, Payment Service, Inventory Service)
- Multiple databases (each service has its own DB)
- External systems (payment provider API)

There is no `COMMIT` that spans all these. Failures anywhere leave the system in a partially updated state.

---

## Why Not Just Use Two-Phase Commit (2PC)?

Two-Phase Commit is the classic protocol for distributed transactions:

**Phase 1 (Prepare)**: Coordinator asks all participants if they can commit.
**Phase 2 (Commit)**: If all say yes, coordinator tells all to commit.

```
Coordinator
    │
    ├─ "Prepare?" → Service A → "Yes (vote)"
    ├─ "Prepare?" → Service B → "Yes (vote)"
    └─ "Prepare?" → Service C → "No (abort)"

If any say No → Send "Abort" to all
If all say Yes → Send "Commit" to all
```

### Problems with 2PC

1. **Blocking protocol**: If the coordinator crashes after Phase 1 but before Phase 2, participants are locked indefinitely waiting for a commit/abort signal.
2. **Availability penalty**: All participants must be available simultaneously. A single node failure aborts the entire transaction.
3. **Performance**: Two round trips minimum; locks held during both phases.
4. **Tight coupling**: All services must implement XA protocol (heavy JTA).

2PC works well within a single database system (PostgreSQL uses it internally). For distributed microservices, better patterns exist.

---

## The Saga Pattern

A saga is a sequence of local transactions, where each local transaction updates its own database and publishes an event/message to trigger the next step. If a step fails, compensating transactions undo previous steps.

### Example: Order Placement Saga

```
CreateOrder → ProcessPayment → ReserveInventory → ShipOrder → OrderConfirmed

On failure at ShipOrder:
ShipOrder (failed) → ReleaseInventory (compensate) → RefundPayment (compensate) → CancelOrder
```

Each step is a local transaction. Eventual consistency — there's a window where the system is in an intermediate state.

### Choreography-Based Saga

Services react to events published by other services. No central coordinator.

```
OrderService: publishes "OrderCreated"
    ↓ (subscribes to OrderCreated)
PaymentService: processes payment, publishes "PaymentProcessed"
    ↓ (subscribes to PaymentProcessed)
InventoryService: reserves inventory, publishes "InventoryReserved"
    ↓ (subscribes to InventoryReserved)
ShippingService: creates shipment, publishes "OrderShipped"
```

**Failure flow:**
```
ShippingService fails → publishes "ShipmentFailed"
    ↓
InventoryService: listens, releases reservation, publishes "InventoryReleased"
    ↓
PaymentService: listens, issues refund, publishes "PaymentRefunded"
    ↓
OrderService: listens, marks order as cancelled
```

**Pros**: Loose coupling, no central coordinator, easy to add new participants.
**Cons**: Hard to track overall saga state, complex failure debugging, cyclic dependencies risk.

### Orchestration-Based Saga

A central saga orchestrator (a dedicated service or workflow engine) explicitly calls each service and handles failures.

```
Order Saga Orchestrator:
1. Call PaymentService.charge()
   ├─ Success → goto 2
   └─ Failure → END (no compensation needed yet)
2. Call InventoryService.reserve()
   ├─ Success → goto 3
   └─ Failure → Call PaymentService.refund() → END
3. Call ShippingService.ship()
   ├─ Success → END (complete)
   └─ Failure → Call InventoryService.release() + PaymentService.refund() → END
```

**Pros**: Explicit saga flow, easy to monitor, clear compensation logic, centralized error handling.
**Cons**: Orchestrator is a central bottleneck, couples orchestrator to participant APIs.

### Choreography vs Orchestration

| | Choreography | Orchestration |
|-|-------------|---------------|
| **Coupling** | Loose (event-driven) | Tighter (orchestrator knows all) |
| **Visibility** | Difficult to see full flow | Clear, centralized |
| **Failure handling** | Complex, distributed | Centralized, explicit |
| **Testing** | Hard (require all services) | Easier (mock services) |
| **Best for** | Simple, linear sagas | Complex sagas with many compensations |

**Tools for orchestration**: Temporal, Apache Airflow, AWS Step Functions, Netflix Conductor, Cadence.

---

## The Outbox Pattern

The #1 problem in event-driven distributed systems: **dual write problem**.

```
// This is WRONG — not atomic
function createOrder(order):
    db.save(order)           // ← if this succeeds but next fails...
    kafka.publish(orderCreated)  // ← ...event is never published!

// Also WRONG — other direction
function createOrder(order):
    kafka.publish(orderCreated)  // ← if service crashes before DB write...
    db.save(order)               // ← ...event published but order never saved!
```

### Outbox Solution

Write the event to an `outbox` table in the same local database transaction as the business data. A separate process (relay) reads the outbox and publishes to the message broker.

```
BEGIN TRANSACTION:
  INSERT INTO orders (id, ...) VALUES (...)
  INSERT INTO outbox (event_type, payload) VALUES ('OrderCreated', '{"id": ...}')
COMMIT;   ← both or neither

Outbox Relay (separate process):
  SELECT * FROM outbox WHERE published = false
  FOR EACH event:
    kafka.publish(event)
    UPDATE outbox SET published = true WHERE id = event.id
```

**Implementations:**
- **Debezium**: CDC connector reads outbox table via database transaction log (WAL/binlog) and publishes to Kafka. Zero-polling, minimal latency.
- **Polling publisher**: Simple background job that queries the outbox table periodically.
- **Transactional outbox with inbox**: Both producer and consumer use outbox/inbox tables for end-to-end exactly-once semantics.

### Outbox Schema

```sql
CREATE TABLE outbox (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    aggregate_type  VARCHAR(255) NOT NULL,   -- e.g., 'Order'
    aggregate_id    VARCHAR(255) NOT NULL,   -- e.g., order ID
    event_type      VARCHAR(255) NOT NULL,   -- e.g., 'OrderCreated'
    payload         JSONB NOT NULL,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    published_at    TIMESTAMPTZ
);
```

---

## Idempotency

When retrying operations (due to timeouts, network failures, at-least-once delivery), the same operation may be executed multiple times. Idempotency ensures that processing the same message multiple times has the same effect as processing it once.

### Idempotency Keys

```
Client → POST /payments {amount: 100, idempotency_key: "req-abc-123"}

Server:
1. Check if idempotency_key already in processed_requests table
2. If yes → return cached result (don't process again)
3. If no → process payment, store result with idempotency_key
```

```sql
CREATE TABLE idempotency_keys (
    key         VARCHAR(255) PRIMARY KEY,
    response    JSONB,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);
```

### Making Operations Idempotent by Design

```
Bad:  INSERT INTO payments (amount) VALUES (100)
      -- run twice = two rows = two charges

Good: INSERT INTO payments (id, amount) VALUES ('req-abc-123', 100)
      ON CONFLICT (id) DO NOTHING
      -- run twice = same result
```

---

## Eventual Consistency Patterns

### Change Data Capture (CDC)

Stream database changes to other services by reading the transaction log:

```
PostgreSQL WAL → Debezium → Kafka → Downstream consumers
MySQL binlog   ↗

Every INSERT, UPDATE, DELETE becomes an event
Guaranteed order, no polling, low latency
```

### CQRS with Event Sourcing

Command Query Responsibility Segregation:
- **Write side (Commands)**: Accept commands, validate, emit events, store events
- **Read side (Queries)**: Consume events, build optimized read models

```
Command: PlaceOrder
    ↓
Write model: validates, saves events to event store
    ↓
Events: OrderPlaced, PaymentCharged, InventoryReserved
    ↓ (consumed asynchronously)
Read models:
  - Order summary view (for GET /orders/123)
  - Analytics materialized view
  - Customer order history
```

Reads are eventually consistent — a write may take milliseconds to appear in the read model.

---

## Handling Partial Failures

### Retry with Exponential Backoff and Jitter

```python
def retry_with_backoff(fn, max_retries=5, base_delay=1.0):
    for attempt in range(max_retries):
        try:
            return fn()
        except TransientError as e:
            if attempt == max_retries - 1:
                raise
            delay = base_delay * (2 ** attempt) + random.uniform(0, 1)
            time.sleep(delay)
```

### Compensation vs Retry

| Scenario | Strategy |
|----------|---------|
| Network timeout on idempotent operation | Retry (safe) |
| Network timeout on non-idempotent operation | Retry with idempotency key |
| Business logic failure (e.g., out of stock) | Compensate (don't retry) |
| Infrastructure failure | Retry with backoff |

### Dead Letter Queues for Sagas

Saga steps that fail after max retries land in a DLQ. Operators inspect and decide:
- Manual compensation
- Replay after fixing root cause
- Accept the inconsistency and reconcile offline

---

## When to Use What

| Pattern | Use When |
|---------|---------|
| **Local transactions only** | Keep data together — redesign boundaries |
| **Saga (choreography)** | Loosely coupled services, simple compensation |
| **Saga (orchestration)** | Complex compensation logic, need visibility |
| **Outbox Pattern** | Need reliable event publishing from DB write |
| **CDC (Debezium)** | Real-time data sync across systems |
| **Idempotency Keys** | Any mutating operation that might be retried |
| **2PC (XA)** | Rarely — only when both systems support XA and you accept availability trade-off |

---

## Production Checklist

- [ ] All saga participants implement idempotent operations
- [ ] Outbox pattern used for any event publishing alongside DB writes
- [ ] Compensation transactions defined and tested for all saga steps
- [ ] Dead letter queue for failed saga steps with alerting
- [ ] Saga state visible somewhere (orchestrator dashboard or dedicated table)
- [ ] Timeout handling for saga steps that don't respond
- [ ] Idempotency keys stored and honored with TTL (clean up after 24–48 hours)
- [ ] Reconciliation job that detects and fixes stuck sagas
- [ ] No 2PC across microservice boundaries

---

## Next Steps

- [Design Patterns](./19-design-patterns.md) — Saga, outbox in the broader patterns context
- [Kafka](./messaging/kafka.md) — Kafka's transactional API for exactly-once saga steps
- [Core Concepts](./02-core-concepts-cap-theorem.md) — eventual consistency theory
