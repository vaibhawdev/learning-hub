# Apache Kafka

## What Is Kafka?

Apache Kafka is a distributed event streaming platform designed for high-throughput, fault-tolerant, persistent publish-subscribe messaging. Unlike traditional message brokers, Kafka stores events in an ordered, immutable log rather than deleting them after consumption. This makes it a platform for both real-time streaming and historical event replay.

Kafka was originally built at LinkedIn in 2011 to handle 1 trillion messages per day. It is now maintained by the Apache Software Foundation.

---

## Core Concepts

### Topics and Partitions

A **topic** is a named stream of events. Think of it as a category or feed. Topics are split into **partitions** for parallelism.

```
Topic: "orders"
┌──────────────────────────────────────────┐
│  Partition 0: [msg0] [msg1] [msg3] [msg5] │
│  Partition 1: [msg2] [msg4] [msg6]        │
│  Partition 2: [msg7] [msg8]               │
└──────────────────────────────────────────┘
         ↑ Each partition is an ordered, immutable log
```

- Each partition is **ordered** — messages within a partition are always consumed in order.
- No ordering guarantee **across** partitions.
- Messages are retained for a configurable duration (e.g., 7 days), regardless of whether they've been consumed.

### Offsets

Each message in a partition has a unique sequential integer — its **offset**. Consumers track their position by storing the last-read offset.

```
Partition 0: offset 0, 1, 2, 3, 4, 5...
                                    ↑
                              consumer position
```

This allows:
- Consumers to replay from any point in history
- Independent consumer groups to read the same data at different offsets

### Producers

Producers write messages to topics. Key decisions:

- **Partition selection**: By default, round-robin. With a key, hash of the key determines partition (`hash(key) % num_partitions`). Messages with the same key always go to the same partition — guaranteeing order per key.
- **Acknowledgment modes**:
  - `acks=0`: Fire and forget (fastest, no durability)
  - `acks=1`: Leader acknowledges (fast, risk if leader crashes before replication)
  - `acks=all` (`acks=-1`): All in-sync replicas acknowledge (slowest, strongest durability)

### Consumers and Consumer Groups

A **consumer group** is a logical subscriber. Each partition is assigned to exactly one consumer within the group. Multiple groups can independently consume the same topic.

```
Topic "orders" (3 partitions):

Consumer Group A (3 consumers):
  Consumer A1 → Partition 0
  Consumer A2 → Partition 1
  Consumer A3 → Partition 2

Consumer Group B (2 consumers):
  Consumer B1 → Partition 0, 1
  Consumer B2 → Partition 2
```

**Key insight:** The number of partitions is the maximum parallelism for a consumer group. If your group has more consumers than partitions, some consumers are idle.

### Brokers

A Kafka **broker** is a server that stores partitions and handles producer/consumer connections. A Kafka cluster typically has 3+ brokers.

Each partition has one **leader** and N-1 **followers** (replicas). Leaders handle all reads and writes; followers replicate the leader's log.

```
Partition 0:
  Leader: Broker 1
  Follower: Broker 2
  Follower: Broker 3
```

If the leader crashes, ZooKeeper (or KRaft in modern Kafka) elects a new leader from the in-sync replicas.

---

## Kafka Architecture

```
Producers              Brokers (Kafka Cluster)             Consumers
                     ┌──────────────────────────┐
Producer A ─────────→│ Broker 1  Broker 2  Broker 3│────────→ Consumer Group A
                     │                            │
Producer B ─────────→│  Topic: orders             │────────→ Consumer Group B
                     │  Partition 0 (Leader: B1)  │
                     │  Partition 1 (Leader: B2)  │────────→ Kafka Streams App
                     │  Partition 2 (Leader: B3)  │
                     └──────────────────────────┘
                                │
                           ZooKeeper / KRaft
                         (metadata, leader election)
```

### KRaft Mode (Kafka Without ZooKeeper)

Since Kafka 3.3, KRaft (Kafka Raft) is the production-ready replacement for ZooKeeper. Benefits:
- Simpler deployment (one fewer system to manage)
- Faster metadata operations
- Better scalability (millions of partitions)
- ZooKeeper reaches end-of-life in Kafka 4.x

---

## Delivery Semantics

| Semantic | What It Means | How to Achieve |
|---------|--------------|----------------|
| **At-most-once** | Messages may be lost, never duplicated | `acks=0`, no retries |
| **At-least-once** | Messages never lost, but may be duplicated | `acks=all`, retries enabled |
| **Exactly-once** | No loss, no duplication | Idempotent producer + transactional API |

### Exactly-Once Semantics (EOS)

Kafka's transactional API allows atomic writes across multiple partitions and topics:

```java
producer.initTransactions();
producer.beginTransaction();
producer.send(record1);
producer.send(record2);
producer.sendOffsetsToTransaction(offsets, groupMetadata);
producer.commitTransaction(); // or abortTransaction()
```

EOS requires:
- `enable.idempotence=true` on the producer
- Kafka 0.11+ and `transactional.id` configured
- Processing framework support (Kafka Streams has it built-in)

---

## Kafka Streams

Kafka Streams is a Java library for building stream processing applications that read from and write to Kafka.

```
Input Topic → KStream → Filter → Map → Aggregate → Output Topic
```

Key concepts:
- **KStream**: Unbounded stream of records
- **KTable**: Changelog stream — represents the latest value per key (a materialized view)
- **GlobalKTable**: KTable replicated to every node (for broadcast joins)
- **Windowing**: Tumbling, hopping, and session windows for time-based aggregations

---

## Kafka Connect

Kafka Connect is a framework for moving data between Kafka and external systems (databases, S3, Elasticsearch) without writing code.

```
Source Connector: MySQL → Kafka
Sink Connector: Kafka → Elasticsearch
```

- **Debezium**: Popular CDC (Change Data Capture) connector that captures database changes as Kafka events
- **S3 Sink**: Archive Kafka topics to object storage
- **JDBC Connector**: Sync relational databases bidirectionally

---

## Schema Registry

Messages in Kafka are bytes — there's no enforcement of schema. The Schema Registry (Confluent) provides:

- Centralized schema storage (Avro, Protobuf, JSON Schema)
- Schema evolution with compatibility rules (backward, forward, full)
- Producers/consumers reference schema by ID, not by embedding it in every message

```
Producer → Serialize with Avro → [SchemaID | payload] → Kafka
Consumer → Read SchemaID → Fetch schema from Registry → Deserialize
```

---

## Performance Characteristics

| Metric | Typical Range |
|--------|--------------|
| Throughput | 100MB/s–1GB/s per broker (hardware dependent) |
| Latency (end-to-end) | 2–10ms (low-latency tuning) to 100ms+ (throughput tuning) |
| Message size | Default max 1MB; configurable up to 1GB |
| Retention | Days to forever (compacted topics) |
| Partitions per cluster | Millions (KRaft mode) |

### Performance Tuning

**Producer:**
- `batch.size`: Larger batches = higher throughput, higher latency
- `linger.ms`: Wait up to N ms to fill a batch before sending
- `compression.type`: `lz4` or `zstd` for significant throughput improvement
- `buffer.memory`: Total memory for buffering; increase for high-throughput producers

**Consumer:**
- `fetch.min.bytes`: Minimum data to fetch per request (increase for throughput)
- `max.poll.records`: Records per poll call
- Parallelism = number of partitions

**Broker:**
- `num.io.threads` and `num.network.threads`: Match to CPU cores
- `log.segment.bytes`: Size of individual log segment files
- OS page cache is critical — Kafka relies on OS to cache recently written data

---

## Log Compaction

Kafka can be configured with **log compaction** instead of time-based retention. With compaction, Kafka keeps only the latest message per key.

```
Before compaction:
[key=A, val=1] [key=B, val=2] [key=A, val=3] [key=C, val=4] [key=A, val=5]

After compaction:
[key=A, val=5] [key=B, val=2] [key=C, val=4]
```

**Use case**: Event sourcing, CDC — maintain a full snapshot of the latest state for each entity.

Tombstone records (null value for a key) cause the key to be deleted during compaction.

---

## Common Kafka Patterns

### Event Sourcing

All state changes are stored as immutable events. The current state is derived by replaying events.

### CQRS

Commands produce events to Kafka. Read models consume events and build materialized views optimized for queries.

### Saga Pattern

Long-running transactions span multiple services. Each service publishes events to Kafka; downstream services react and publish their own events.

### Outbox Pattern

Prevents the dual-write problem (updating DB and publishing to Kafka atomically):
1. Write the event to an `outbox` table in the same DB transaction
2. A separate process (Debezium) reads the outbox table via CDC and publishes to Kafka

---

## Things to Watch in Production

| Concern | What to Monitor |
|---------|----------------|
| **Consumer lag** | Difference between latest offset and consumer offset — indicates backlog |
| **Under-replicated partitions** | Partitions where followers are behind leader — risk of data loss |
| **Disk usage** | Kafka stores all messages — disk fills up if retention is not set |
| **Producer failures** | `records-error-rate` metric; DLQ for undeliverable messages |
| **Rebalance frequency** | Frequent consumer group rebalances cause processing pauses |
| **Partition skew** | Some partitions much larger than others — bad shard key |
| **Leader imbalance** | All leaders on one broker after failures — run preferred replica election |

---

## Production Checklist

- [ ] `acks=all` and `min.insync.replicas=2` for durability
- [ ] Idempotent producer enabled (`enable.idempotence=true`)
- [ ] Replication factor = 3 for all production topics
- [ ] Retention period explicitly set (not relying on defaults)
- [ ] Schema Registry deployed with compatibility rules
- [ ] Consumer lag monitoring with alerting
- [ ] Dead letter queue (DLQ) topic for failed messages
- [ ] Graceful consumer shutdown (poll loop handles wakeup signal)
- [ ] Proper partition count chosen based on expected throughput (can only increase, not decrease)
- [ ] JVM heap tuned (6–8GB typical; avoid GC pauses with G1GC)
- [ ] Disk I/O monitoring — Kafka is I/O intensive
- [ ] TLS encryption + SASL authentication enabled
- [ ] Network policies restricting Kafka cluster access

---

## Managed Kafka Options

| Option | Notes |
|--------|-------|
| **Confluent Cloud** | Fully managed, enterprise features, Schema Registry included |
| **Amazon MSK** | Managed Kafka on AWS, deep AWS integration |
| **Azure Event Hubs** | Kafka-compatible API on Azure |
| **Redpanda** | Kafka-compatible, written in C++, 10x lower latency, no JVM |
| **Aiven for Kafka** | Multi-cloud managed Kafka |
| **WarpStream** | Kafka-compatible, object storage backend (low cost) |

---

## Next Steps

- [RabbitMQ](./rabbitmq.md) — when traditional message queueing is more appropriate
- [Messaging Comparison](./comparison.md) — Kafka vs RabbitMQ vs ActiveMQ
- [Distributed Transactions](../17-distributed-transactions.md) — Saga and Outbox patterns with Kafka
