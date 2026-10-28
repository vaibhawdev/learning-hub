# Messaging Systems

> **Phase:** 4 — Distributed Systems  
> **Status:** Not Started

## Overview

Messaging systems are the connective tissue of distributed architectures — they decouple services, enable async processing, and provide durability guarantees for inter-service communication. This section covers the internals and operational patterns of the key messaging platforms used in modern systems.

## Topics

### Kafka Internals

**What it is:** A distributed, partitioned, replicated, durably-persisted commit log designed for high-throughput event streaming — acting as both a message broker and an event store.

**Why it matters:** Kafka is the standard for high-volume event streaming in enterprise systems — understanding its storage model, replication, and consumer protocol prevents common misconfigurations.

**Key concepts to understand:**
- Log-based storage: messages appended to partition log files — sequential I/O, extremely fast
- Topics, partitions, and offsets: each partition is an ordered, immutable sequence with monotonic offsets
- Replication: leader partition + follower replicas; `acks=all` ensures leader and all ISR replicas acknowledge
- ISR (In-Sync Replicas): replicas that are caught up within `replica.lag.time.max.ms`
- Consumer groups: multiple consumers sharing a topic, each partition assigned to one consumer
- Log compaction: retain only the latest value per key — enables Kafka as a state store

**Resources to explore:**
- [ ] (to be filled)

---

### RabbitMQ Internals

**What it is:** A message broker implementing AMQP, supporting multiple messaging patterns via exchanges (direct, fanout, topic, headers), queues, and bindings.

**Why it matters:** RabbitMQ is widely used for task queues, work distribution, and pub/sub in .NET ecosystems — understanding its routing model enables building flexible messaging topologies.

**Key concepts to understand:**
- Exchange types: direct (routing key == binding key), fanout (broadcast), topic (wildcard matching), headers
- Queue properties: durable (survives broker restart), exclusive (single consumer), auto-delete
- Message acknowledgments: basic.ack, basic.nack, basic.reject — with requeue option
- Publisher confirms: synchronous or asynchronous confirmation of message persistence
- Quorum queues: Raft-based replicated queues replacing classic mirrored queues in RabbitMQ 3.8+
- Dead-letter exchange (DLX): failed messages routed to DLX for inspection or retry

**Resources to explore:**
- [ ] (to be filled)

---

### Azure Service Bus

**What it is:** Microsoft Azure's enterprise messaging service supporting queues (point-to-point) and topics/subscriptions (pub/sub), with sessions, deferred processing, and dead-lettering.

**Why it matters:** Azure Service Bus is the standard messaging backbone for Azure-native .NET microservices — its sessions and deferred processing capabilities enable sophisticated ordering and workflow patterns.

**Key concepts to understand:**
- Queues vs Topics/Subscriptions: queue is point-to-point; topic with subscriptions is pub/sub
- Sessions: grouping related messages with a session ID for ordered, exclusive processing per session
- Message lock: a consumer holds a message lock for processing time — lock renewal prevents expiry
- Dead-letter queue (DLQ): messages that fail processing or exceed delivery count moved here
- Peek-lock vs ReceiveAndDelete: peek-lock allows nack/requeue; receive-and-delete is fire-and-forget
- Scheduled messages: deliver a message at a specific future time — for delayed processing

**Resources to explore:**
- [ ] (to be filled)

---

### Ordering Guarantees

**What it is:** The extent to which a messaging system guarantees that messages are delivered to consumers in the same order they were produced.

**Why it matters:** Ordering bugs are among the hardest to reproduce — understanding where ordering is and isn't guaranteed prevents state corruption in event-driven systems.

**Key concepts to understand:**
- Kafka: ordering guaranteed within a partition, not across partitions — use same partition key for ordered messages
- RabbitMQ: single queue with single consumer maintains FIFO; multiple consumers break ordering
- Azure Service Bus: sessions provide ordered, exclusive delivery; without sessions, ordering is not guaranteed
- Partial ordering: ordering requirements often apply within an entity (e.g., all events for Order #123) not globally
- Timestamp-based ordering: unreliable due to clock skew — prefer logical ordering (Kafka offsets, sequence numbers)
- Idempotent + commutative operations: can tolerate out-of-order delivery

**Resources to explore:**
- [ ] (to be filled)

---

### Outbox Pattern

**What it is:** A transactional pattern where outgoing messages are written to an "outbox" table in the same database transaction as the domain change, and a separate process reliably publishes them to the message broker.

**Why it matters:** The outbox pattern solves the dual-write problem: without it, a service can write to the database but fail to publish the event (or vice versa), causing data inconsistency.

**Key concepts to understand:**
- Problem: `SaveChanges()` succeeds but `PublishEvent()` fails — database updated, event lost
- Outbox table: `Id, EventType, Payload, CreatedAt, ProcessedAt` — in the same transactional database
- Outbox processor: polls or uses change data capture (CDC) to read unprocessed outbox entries and publish
- At-least-once guarantee: outbox processor may republish on retry — consumer must be idempotent
- CDC-based outbox: Debezium reads PostgreSQL WAL / SQL Server CDC to publish changes reliably
- Transactional outbox in .NET: MassTransit and NServiceBus provide built-in outbox support

**Resources to explore:**
- [ ] (to be filled)

---

### Inbox Pattern

**What it is:** The consumer-side complement to the outbox pattern — storing received message IDs in a database table within the same transaction as message processing to enable idempotent consumption.

**Why it matters:** Brokers provide at-least-once delivery — the inbox pattern ensures that redelivered messages don't cause duplicate processing of non-idempotent operations.

**Key concepts to understand:**
- Inbox table: `MessageId, ReceivedAt, ProcessedAt` — checked before processing each message
- Transactional semantics: inbox write and domain update in same transaction — atomically prevents duplicates
- Inbox cleanup: delete processed inbox entries after a retention window
- Idempotency vs inbox: inbox is explicit deduplication; some operations are naturally idempotent
- Distributed inbox: when consuming from Kafka across replicas, each replica needs its own inbox
- Inbox table vs idempotency key in domain: choose based on how reusable the deduplication logic needs to be

**Resources to explore:**
- [ ] (to be filled)

---

### Dead-Letter Queues

**What it is:** A special queue where messages are moved when they cannot be processed after a configured number of delivery attempts or when they expire — enabling investigation without losing messages.

**Why it matters:** Dead-letter queues are essential for operational visibility — without them, failed messages are silently lost and systems appear to work when they are actually losing data.

**Key concepts to understand:**
- Reasons for dead-lettering: max delivery count exceeded, message TTL expired, processing exception, poison message
- DLQ monitoring: alert when DLQ depth exceeds zero — indicates systematic processing failures
- DLQ replay: reprocess DLQ messages after fixing the consumer bug
- Selective replay: replay only specific message types or from a specific time range
- Poison message handling: some messages may never be processable — route to a separate analysis queue
- DLQ in Azure Service Bus, RabbitMQ DLX, Kafka dead-letter topic (manual pattern)

**Resources to explore:**
- [ ] (to be filled)

---

### Message Replay

**What it is:** The ability to re-process messages from a messaging system — either from the beginning, from a specific offset, or from a point in time — to rebuild state or recover from errors.

**Why it matters:** Message replay is one of Kafka's most powerful capabilities — it enables rebuilding read models, recovering from consumer bugs, and onboarding new services to historical data.

**Key concepts to understand:**
- Kafka offset management: consumer commits offsets — resetting offsets enables replay
- `consumer.seekToBeginning(partitions)`: replay all messages from partition start
- Time-based seek: `consumer.offsetsForTimes()` to replay from a specific timestamp
- Log retention: Kafka retains data based on time or size — replay is only possible within retention window
- Log compaction: compacted topics retain only the latest value per key — suitable for state rebuild
- Consumer group isolation: use a separate consumer group for replay to avoid affecting production consumers

**Resources to explore:**
- [ ] (to be filled)

---

### Event Versioning

**What it is:** Strategies for evolving message/event schemas over time while maintaining compatibility between producers and consumers that may be at different schema versions.

**Why it matters:** Events are immutable once stored — schema evolution strategy determines whether old consumers can process new events and vice versa, avoiding breaking changes in distributed systems.

**Key concepts to understand:**
- Backward compatibility: new schema can read data written with old schema — add optional fields only
- Forward compatibility: old schema can read data written with new schema — ignore unknown fields
- Full compatibility: both forward and backward — the ideal for independent service deployment
- Schema registry (Confluent Schema Registry, Azure Schema Registry): enforce compatibility at publish time
- Event upcasting: transform stored events to newer schema versions at read time
- Versioned event types: `OrderCreatedV1`, `OrderCreatedV2` — explicit versioning with routing by type name

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Core Distributed Systems](./01-core-distributed-systems.md)
- [High-Scale Architecture](./03-high-scale-architecture.md)
- [Azure Core](../Phase-06-Cloud-And-Azure/01-azure-core.md)
- [Reliability Engineering](../Phase-09-Reliability-And-Observability/01-observability.md)
