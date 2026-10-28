# ActiveMQ

## What Is ActiveMQ?

Apache ActiveMQ is an open-source message broker implementing the **Java Message Service (JMS)** specification. It is one of the oldest and most widely deployed message brokers in enterprise Java ecosystems.

ActiveMQ comes in two versions:
- **ActiveMQ Classic** (5.x): The original; widely used in enterprise Java environments
- **ActiveMQ Artemis** (2.x): A complete rewrite with higher performance, non-blocking I/O, and a new storage engine. This is the future of the project and the basis for Red Hat AMQ

---

## JMS Concepts

JMS defines a standard API for Java applications to interact with message brokers.

### Two Messaging Models

#### Point-to-Point (Queues)

```
Producer → Queue → Consumer 1 (one consumer gets each message)
```

- Each message is consumed by exactly one consumer
- Messages persist until consumed or expired
- Consumers compete for messages (load balanced)

#### Publish-Subscribe (Topics)

```
Producer → Topic → Consumer 1 (all get a copy)
                 → Consumer 2
                 → Consumer 3
```

- Each subscribed consumer gets a copy of every message
- Without durable subscriptions, consumers miss messages sent while offline

### Durable Subscriptions

A durable topic subscriber retains messages sent while the subscriber was offline:

```java
// Create a durable subscription
TopicSubscriber subscriber = session.createDurableSubscriber(topic, "mySubscription");
```

The broker stores messages for durable subscribers until they reconnect.

---

## ActiveMQ Classic vs Artemis

| Feature | ActiveMQ Classic | ActiveMQ Artemis |
|---------|-----------------|------------------|
| Storage | KahaDB or JDBC | Journal-based (async, high-performance) |
| Threading | Blocking I/O | Non-blocking (Netty) |
| Throughput | Moderate | High (3–5x Classic) |
| Message model | JMS Queues + Topics | Addresses + Queues (more flexible) |
| Protocols | OpenWire, STOMP, AMQP, MQTT | OpenWire, STOMP, AMQP, MQTT, CORE |
| HA | Master-Slave shared store or network of brokers | Live-backup pairs, replication |
| Web console | Hawtio | Artemis Web Console |
| Spring Boot | Full support | Full support |

**Recommendation**: Use Artemis for new projects. Classic is only for legacy system compatibility.

---

## ActiveMQ Artemis Architecture

### Addresses and Queues

Artemis separates the routing concept (Address) from the storage concept (Queue):

```
Address: "orders"
  ├── Queue: "orders.billing"   (routing-type: anycast)
  ├── Queue: "orders.inventory" (routing-type: anycast)
  └── Queue: "orders.audit"     (routing-type: multicast)
```

- **Anycast**: One consumer gets the message (queue semantics)
- **Multicast**: All consumers get the message (topic semantics)

### Journal Storage

Artemis uses a high-performance journal for message persistence:
- Writes are sequential (fast disk access)
- Periodic journal compaction removes consumed messages
- Supports `NIO` (standard Java) and `ASYNCIO` (Linux io_uring via libaio — much faster)

---

## Protocols Supported

| Protocol | Use Case |
|----------|----------|
| **OpenWire** | Native ActiveMQ protocol; best for Java/JMS clients |
| **AMQP 1.0** | Interoperability with RabbitMQ, Azure Service Bus, and other AMQP brokers |
| **STOMP** | Simple text protocol; good for non-Java clients (Python, Ruby, JavaScript) |
| **MQTT** | Lightweight protocol for IoT devices |
| **WebSockets** | Browser-based clients (via STOMP or MQTT over WebSocket) |

Multi-protocol support makes Artemis suitable as a **universal broker** for polyglot environments.

---

## High Availability

### Live-Backup Pair

A live broker and a passive backup:

```
Live Broker → Journal → Backup Broker (passive, replicates journal)
                              ↑
                    Takes over if live fails
```

Two HA modes:
1. **Shared Store**: Both live and backup share the same filesystem (NFS/SAN)
2. **Replication**: Journal replicated over network to backup; no shared filesystem needed

### Network of Brokers (Classic only)

Connect multiple Classic brokers in a network topology for load balancing and failover:

```
Client → Broker 1 ←→ Broker 2 ←→ Broker 3
```

Messages forwarded between brokers based on consumer demand.

---

## Message Grouping

Route messages with the same group ID to the same consumer — useful when ordered processing per entity is required:

```java
message.setStringProperty("JMSXGroupID", "order-123");
```

All messages with `JMSXGroupID=order-123` go to the same consumer, preserving order.

---

## Message Selectors

Consumers can filter messages using SQL-like expressions on message properties:

```java
MessageConsumer consumer = session.createConsumer(queue, "priority > 5 AND region = 'US'");
```

**Caution**: Selectors are evaluated on the broker — all messages are scanned. High selectivity with large queues can be a performance problem. Prefer routing via exchanges/addresses over selectors.

---

## Transactions in JMS

JMS supports local transactions — all messages within a session are committed or rolled back together:

```java
Session session = connection.createSession(true, Session.SESSION_TRANSACTED);
try {
    producer.send(message1);
    producer.send(message2);
    session.commit();  // both sent atomically
} catch (Exception e) {
    session.rollback(); // neither sent
}
```

For cross-broker or cross-database transactions, XA (distributed transactions / JTA) is supported.

---

## Dead Letter Address

Configure where undeliverable messages go:

```xml
<!-- broker.xml -->
<address-setting match="#">
    <dead-letter-address>DLQ</dead-letter-address>
    <max-delivery-attempts>3</max-delivery-attempts>
</address-setting>
```

After 3 failed delivery attempts, the message moves to DLQ.

---

## Spring Boot Integration

```java
@Component
public class OrderListener {

    @JmsListener(destination = "orders", containerFactory = "jmsListenerContainerFactory")
    public void processOrder(Order order) {
        // process order
    }
}
```

With Spring Boot:
- `spring-boot-starter-activemq` for Classic
- `spring-boot-starter-artemis` for Artemis
- `@JmsListener` annotation handles consumer lifecycle
- Auto-configuration sets up connection factory and JmsTemplate

---

## Things to Watch in Production

| Concern | Indicator |
|---------|-----------|
| Queue depth growing | Consumers slower than producers → scale consumers |
| Memory usage | Broker paging to disk when memory exceeds threshold |
| Journal size | Old journal files not being cleaned up → disk full |
| Connection leaks | Applications not closing connections → broker resource exhaustion |
| Slow consumers | Message redelivery to DLQ → inspect DLQ regularly |
| ASYNCIO not enabled | Using NIO on Linux → switch to ASYNCIO for 3-5x write throughput |

---

## When to Choose ActiveMQ

**Choose ActiveMQ Classic when:**
- Existing Java/JMS ecosystem already uses it
- You need JMS 1.x/2.x API compliance
- Legacy Spring/EJB applications require OpenWire

**Choose ActiveMQ Artemis when:**
- High-throughput JMS workloads
- Multi-protocol broker (JMS + MQTT + AMQP in one broker)
- IoT or mixed-language environments
- New projects that want JMS semantics

**Choose something else when:**
- You need event streaming with replay (→ Kafka)
- You need flexible message routing with AMQP 0-9-1 (→ RabbitMQ)
- You need a fully managed cloud service (→ Amazon SQS/SNS, Azure Service Bus)

---

## Production Checklist

- [ ] Artemis preferred over Classic for new deployments
- [ ] `ASYNCIO` journal enabled on Linux (`libaio` installed)
- [ ] Live-backup HA pair configured with automatic failover
- [ ] Dead letter address configured for all queues
- [ ] `max-delivery-attempts` set (3–5 typically)
- [ ] Connection factory configured with reconnect delay and max reconnects
- [ ] Heap tuned appropriately (broker heap separate from application)
- [ ] Paging configured: `page-size-bytes` and `max-size-bytes` per address
- [ ] TLS enabled for all external client connections
- [ ] Management console access restricted to internal network
- [ ] JMX monitoring integrated with Prometheus/Grafana

---

## Next Steps

- [Messaging Comparison](./comparison.md) — ActiveMQ vs Kafka vs RabbitMQ — choose the right tool
- [Kafka](./kafka.md) — when you need event streaming and replay
- [RabbitMQ](./rabbitmq.md) — when you need flexible routing with AMQP 0-9-1
