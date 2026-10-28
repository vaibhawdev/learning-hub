# RabbitMQ

## What Is RabbitMQ?

RabbitMQ is an open-source message broker implementing the **AMQP 0-9-1** protocol (Advanced Message Queuing Protocol). Unlike Kafka's log-based model, RabbitMQ is a traditional **push-based broker**: it receives messages from producers, routes them through exchanges to queues, and delivers them to consumers. Messages are deleted from the queue once acknowledged.

RabbitMQ is ideal when:
- You need complex routing logic (topic-based, header-based, content-based)
- Messages should be consumed once and deleted
- You need per-message acknowledgment, dead-lettering, and priority queues
- Task/job queue semantics (work queues, delayed jobs)

---

## Core Concepts

### AMQP Model

```
Producer → Exchange → Binding → Queue → Consumer
```

The AMQP model separates **message routing** (exchange) from **message storage** (queue):

- **Producer**: Publishes messages to an exchange with a routing key
- **Exchange**: Receives messages and routes them to queues based on rules
- **Binding**: A link between an exchange and a queue, with optional routing criteria
- **Queue**: Stores messages until consumers process them
- **Consumer**: Subscribes to a queue and processes messages

---

## Exchange Types

### 1. Direct Exchange

Routes messages to queues whose binding key exactly matches the message's routing key.

```
Message: routing_key="payment.processed"
Binding: queue="billing", binding_key="payment.processed"
→ Delivered to "billing" queue
```

**Use for**: Event routing where you know the exact destination.

### 2. Topic Exchange

Routes messages using wildcard patterns on routing keys.

- `*` matches exactly one word
- `#` matches zero or more words

```
Message: routing_key="order.us.placed"
Binding 1: queue="us-orders",     binding_key="order.us.#"   → MATCH
Binding 2: queue="all-orders",    binding_key="order.#"       → MATCH
Binding 3: queue="eu-orders",     binding_key="order.eu.#"    → NO MATCH
```

**Use for**: Publish-subscribe with flexible routing, event categorization.

### 3. Fanout Exchange

Broadcasts messages to all bound queues, ignoring routing keys.

```
Message → Fanout Exchange → Queue 1 (copy)
                          → Queue 2 (copy)
                          → Queue 3 (copy)
```

**Use for**: Broadcasting notifications, cache invalidation, audit logging.

### 4. Headers Exchange

Routes based on message header attributes instead of routing key.

```
Message headers: {format: "pdf", type: "report"}
Binding: x-match=all, format=pdf, type=report → MATCH
Binding: x-match=any, format=csv             → NO MATCH
```

**Use for**: Complex routing conditions that don't map to routing key patterns.

### 5. Default (Nameless) Exchange

Every queue is automatically bound to the default exchange with the queue name as routing key. Allows producers to send directly to a named queue.

---

## Message Acknowledgment

RabbitMQ delivers messages and waits for acknowledgment before removing them.

### Modes

| Mode | Behavior | Risk |
|------|---------|------|
| `auto` | Message deleted on delivery | Message lost if consumer crashes before processing |
| `manual` | Consumer explicitly acks/nacks | Safest — message redelivered on crash |
| `none` | No acknowledgment (fire and forget) | Fastest; messages never redelivered |

### Manual Acknowledgment Flow

```
Consumer:
  while message = queue.get():
      try:
          process(message)
          channel.basic_ack(delivery_tag)     # success
      except:
          channel.basic_nack(delivery_tag, requeue=True)  # retry
          # or requeue=False to send to DLQ
```

### Negative Acknowledgment (Nack)

`basic_nack` with `requeue=False` moves the message to the **Dead Letter Exchange (DLX)** if configured.

---

## Dead Letter Queues

A dead letter queue (DLQ) receives messages that cannot be processed:

- Message is rejected with `requeue=False`
- Message TTL expires before being consumed
- Queue length limit is exceeded

```
Main Queue (x-dead-letter-exchange: dlx) → DLX → DLQ
```

```python
channel.queue_declare(
    queue='orders',
    arguments={
        'x-dead-letter-exchange': 'dlx',
        'x-dead-letter-routing-key': 'orders.failed',
        'x-message-ttl': 30000  # 30 seconds
    }
)
```

**Best practice**: Always configure DLQ in production. Failed messages should be inspected, not silently discarded.

---

## Message Persistence and Durability

By default, queues and messages are in-memory and lost on broker restart.

### Making Queues Durable

```python
channel.queue_declare(queue='orders', durable=True)
```

Durable queues survive broker restart but only store durable messages.

### Making Messages Persistent

```python
channel.basic_publish(
    exchange='',
    routing_key='orders',
    body='{"order_id": 123}',
    properties=pika.BasicProperties(delivery_mode=2)  # 2 = persistent
)
```

**Note**: Durability does not guarantee zero message loss. Messages written to disk can still be lost if the broker crashes before the OS flushes the write buffer. For stronger guarantees, use publisher confirms.

### Publisher Confirms

An extension to AMQP that allows the broker to confirm when a message has been written to disk:

```python
channel.confirm_delivery()
channel.basic_publish(...)
# Raises exception if message not confirmed
```

---

## Quorum Queues (Modern HA)

Quorum queues replace classic mirrored queues for high availability. They use the Raft consensus algorithm:

- Messages replicated to a majority of nodes before acknowledgment
- Leader election on node failure
- No split-brain scenarios
- **Recommended for all production use cases** over classic mirrored queues

```python
channel.queue_declare(
    queue='orders',
    arguments={'x-queue-type': 'quorum'}
)
```

### Classic Mirrored Queues (Deprecated)

The old HA mechanism — messages replicated to all mirror nodes. Deprecated in RabbitMQ 3.13+. Migrate to quorum queues.

---

## Priority Queues

Messages can have priority levels; higher-priority messages are delivered first.

```python
channel.queue_declare(
    queue='jobs',
    arguments={'x-max-priority': 10}  # supports 0-10
)

# Publish with priority
channel.basic_publish(
    ...,
    properties=pika.BasicProperties(priority=8)  # high priority
)
```

**Caution**: Priority queues increase broker CPU and memory overhead. Use only when necessary.

---

## Delayed Messages

RabbitMQ does not natively support delayed message delivery. Options:

1. **TTL + DLQ pattern**: Publish to a queue with TTL, route expired messages to the work queue.
2. **rabbitmq-delayed-message-exchange plugin**: Exchange that delays message delivery.
3. **External scheduler**: Store delayed jobs in a database; a scheduler publishes to RabbitMQ when time comes.

---

## Work Queues (Task Queues)

The classic pattern for distributing jobs across workers:

```
Producer → Queue → Worker 1
                 → Worker 2
                 → Worker 3
```

- Each message is delivered to exactly one worker
- Workers process independently in parallel
- Messages redelivered if worker crashes before acking
- Set `prefetch_count` to control how many unacknowledged messages a worker can hold

```python
channel.basic_qos(prefetch_count=1)  # process one at a time
```

`prefetch_count=1` ensures no worker is overloaded — it only receives the next job after acknowledging the current one.

---

## Competing Consumers vs Fan-Out

| Pattern | Exchange Type | Consumers | Message Copies |
|---------|-------------|-----------|---------------|
| Competing consumers (work queue) | Default or direct | Multiple, same queue | One per message |
| Publish-subscribe | Fanout | Multiple, different queues | One per queue |
| Topic-based routing | Topic | Selective, matched queues | One per matching queue |

---

## RabbitMQ Clustering

Multiple RabbitMQ nodes form a cluster for HA and throughput:

```
┌───────────┐    ┌───────────┐    ┌───────────┐
│  Node 1   │◄──►│  Node 2   │◄──►│  Node 3   │
│ (primary) │    │           │    │           │
└───────────┘    └───────────┘    └───────────┘
       ↑
   Erlang cookie authentication between nodes
```

In a cluster, queue metadata and bindings are replicated. But queue **contents** are only on the node where the queue was declared (unless using quorum queues or mirrored queues).

### Federation and Shovel

For multi-datacenter setups where clustering is not feasible:
- **Federation**: Forward messages between independent RabbitMQ clusters
- **Shovel**: Move or copy messages between queues/exchanges (even across clusters)

---

## Performance Tuning

| Parameter | Default | Recommendation |
|-----------|---------|----------------|
| `prefetch_count` | Unlimited | Set 10–50 for fair dispatch; 1 for strict ordering |
| `heartbeat` | 60s | Reduce for faster connection failure detection |
| `connection_max` | 128 | Increase for high-concurrency workloads |
| `vm_memory_high_watermark` | 0.4 (40% RAM) | Tune based on available memory |
| `disk_free_limit` | 50MB | Set to at least 2GB for production |
| `channel_max` | 2047 | Increase for apps that use many channels |

**Connection pooling**: Creating connections and channels is expensive. Use a connection pool. One connection per application process; multiple channels per connection.

---

## Streams (RabbitMQ 3.9+)

RabbitMQ Streams is a new queue type that provides Kafka-like semantics:
- Persistent, append-only log
- Multiple consumers read the same data (no deletion on consumption)
- Offset-based consumption
- High throughput

Use Streams when you need replay semantics within RabbitMQ without migrating to Kafka.

---

## Things to Watch in Production

| Concern | Metric / Alert |
|---------|---------------|
| Queue depth growing | `messages_ready` > threshold → consumers not keeping up |
| Memory alarm | `mem_alarm=true` → broker flow-controls producers |
| Disk alarm | `disk_alarm=true` → broker stops accepting publishes |
| Unacknowledged messages | `messages_unacknowledged` → consumers stalled |
| Connection churn | High `connection_created`/`connection_closed` → missing connection pooling |
| Dead letter volume | DLQ growing → consumer processing failures |
| Consumer cancellations | Consumers being cancelled after HA failover |

---

## Production Checklist

- [ ] Quorum queues used instead of classic mirrored queues
- [ ] All queues declared as `durable=True`
- [ ] All production messages sent as `delivery_mode=2` (persistent)
- [ ] Publisher confirms enabled on producers
- [ ] Dead letter exchange configured on all queues
- [ ] `prefetch_count` set appropriately per consumer
- [ ] Connection pooling used in application
- [ ] Memory and disk alarms configured
- [ ] TLS enabled for AMQP connections
- [ ] Management plugin secured (default guest/guest user disabled)
- [ ] Shovel or Federation configured if multi-region
- [ ] Automated consumer restart on failure (supervisor or k8s restart policy)

---

## Next Steps

- [ActiveMQ](./activemq.md) — JMS-based alternative with different trade-offs
- [Messaging Comparison](./comparison.md) — choose between Kafka, RabbitMQ, and ActiveMQ
- [Design Patterns](../19-design-patterns.md) — circuit breaker, retry patterns for consumers
