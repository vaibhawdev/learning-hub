# Messaging Systems: Comparison & Selection Guide

## The Landscape

| System | Model | Protocol | Primary Strength | Best For |
|--------|-------|----------|-----------------|---------|
| **Apache Kafka** | Log-based streaming | Custom (Kafka protocol) | Throughput, replay, stream processing | Event streaming, audit log, CDC |
| **RabbitMQ** | Push-based broker | AMQP 0-9-1 | Flexible routing, message acknowledgment | Task queues, complex routing |
| **ActiveMQ Artemis** | Push-based broker | JMS / AMQP / MQTT | JMS compliance, multi-protocol | Enterprise Java, IoT gateways |
| **Apache Pulsar** | Log + broker hybrid | Custom (Pulsar) | Multi-tenancy, geo-replication | Multi-tenant platforms, global streaming |
| **NATS** | Pub/sub + JetStream | Custom (NATS) | Ultra-low latency, simple | Cloud-native microservices, IoT |
| **Amazon SQS/SNS** | Managed queue/fanout | HTTP/HTTPS | Zero ops, AWS native | AWS-native applications |
| **Azure Service Bus** | Managed broker | AMQP 1.0 | Sessions, transactions | Azure-native enterprise apps |
| **Google Pub/Sub** | Managed pub/sub | HTTP/gRPC | Global scale, serverless | GCP-native, global fan-out |
| **Redis Streams** | Log in Redis | RESP | Co-located with cache | Simple event log alongside existing Redis |

---

## Kafka vs RabbitMQ: The Core Difference

This is the most common comparison. The fundamental difference is the **message model**:

### Kafka: Log-Based

```
Topic (immutable log):
[msg0][msg1][msg2][msg3][msg4][msg5]...
  ↑               ↑
Consumer A      Consumer B
(offset 0)      (offset 3)
```

- Messages are retained for a period (days/weeks) regardless of consumption
- Multiple consumer groups can read the same data independently
- Consumers control their own position (offset)
- Replay historical data at any time

### RabbitMQ: Queue-Based

```
Queue:
[msg0][msg1][msg2][msg3]...
        ↑
   Consumer
   (message deleted after ack)
```

- Messages are deleted after acknowledgment
- One consumer group gets each message
- Broker controls delivery (push)
- No replay after consumption

### Decision Matrix

| Requirement | Kafka | RabbitMQ |
|-------------|-------|---------|
| Replay historical events | ✓ | ✗ |
| Multiple independent consumers of same data | ✓ | Only via fan-out exchange + multiple queues |
| Per-message routing rules | ✗ (partition only) | ✓ (exchange types) |
| Message priority | ✗ | ✓ |
| Delayed message delivery | ✗ (workaround) | ✓ (plugin) |
| Very high throughput (>100k msg/s) | ✓ | Possible but harder |
| Complex acknowledgment + retry logic | Requires coding | ✓ Built-in |
| Dead letter queues per message | ✗ | ✓ |
| Request-reply patterns | ✗ | ✓ (reply-to) |
| Stream processing (joins, windows) | ✓ (Kafka Streams) | ✗ |
| Audit log / event sourcing | ✓ | ✗ |
| Low operational complexity | Moderate | Low |
| Managed cloud service quality | Excellent | Good |

---

## When to Choose Each

### Choose Kafka When

1. **You need event streaming** — real-time pipelines between systems (CDC, ETL, analytics)
2. **Replay is a requirement** — audit log, event sourcing, reprocessing after bug fix
3. **Multiple independent consumers** — many services subscribe to the same event stream
4. **High throughput** — millions of events per second
5. **Stream processing** — aggregations, joins, windowing over event streams
6. **Ordering matters at scale** — ordering per partition key with high volume

### Choose RabbitMQ When

1. **Task queues / work queues** — job distribution across workers
2. **Complex routing** — route messages to different queues based on content, topic patterns, or headers
3. **Per-message acknowledgment matters** — need to know each individual message was processed
4. **Delayed or scheduled message delivery**
5. **Priority queues** — high-priority messages processed before low-priority
6. **Lower operational complexity** — RabbitMQ is easier to operate at moderate scale
7. **Request-reply patterns** — RPC over messaging
8. **Mixed language environments** — AMQP 0-9-1 clients in Python, Ruby, .NET, Java

### Choose ActiveMQ When

1. **JMS compliance required** — existing Java EE/Jakarta EE applications using JMS API
2. **Multi-protocol broker in one** — need JMS + MQTT + AMQP + STOMP from one broker
3. **IoT gateway** — MQTT for devices, JMS for Java back-end in same broker
4. **Enterprise Java ecosystem** — Spring, JBoss, Camel, ServiceMix

### Choose NATS When

1. **Microservices on Kubernetes** — ultra-low latency (sub-millisecond), tiny footprint
2. **Request-reply at scale** — native request-reply with built-in correlation
3. **Simple pub/sub** — no complex routing requirements
4. **JetStream** for persistence — append-only log, at-least-once, replay

### Choose Apache Pulsar When

1. **Multi-tenancy** — need strong namespace and tenant isolation in one cluster
2. **Global geo-replication** — active-active or active-passive across regions natively
3. **Hybrid queue + stream** — subscription types cover both models
4. **Unlimited retention** — tiered storage to object storage (S3) built-in

### Choose Managed Cloud Services When

1. **Minimal operations** — no infrastructure to manage
2. **AWS/Azure/GCP native** — tight integration with other cloud services (Lambda triggers, etc.)
3. **Variable workloads** — pay per message, automatic scaling

---

## Throughput Comparison

These numbers are approximate and hardware-dependent:

| System | Throughput | Latency (p99) |
|--------|-----------|--------------|
| Kafka | 1M+ msg/s per partition | 2–15ms |
| Redpanda (Kafka-compatible) | 1M+ msg/s | <1ms |
| RabbitMQ | 50k–100k msg/s per queue | 1–10ms |
| ActiveMQ Artemis | 100k–300k msg/s | 1–5ms |
| NATS (JetStream) | 500k+ msg/s | <1ms |
| Redis Streams | 500k+ msg/s | <1ms |
| Amazon SQS | ~3k msg/s standard, ~30k FIFO | 1–50ms |

---

## Protocol Interoperability

| Protocol | Kafka | RabbitMQ | Artemis | NATS |
|----------|-------|---------|---------|------|
| Kafka protocol | ✓ | via plugin | ✗ | ✗ |
| AMQP 0-9-1 | ✗ | ✓ (native) | ✓ | ✗ |
| AMQP 1.0 | ✗ | via plugin | ✓ | ✗ |
| MQTT | via plugin | via plugin | ✓ | via plugin |
| STOMP | via plugin | ✓ | ✓ | ✗ |
| JMS | ✓ (kafka-clients) | ✓ | ✓ (native) | ✗ |

---

## Operational Complexity

| System | Cluster Setup | Operations | Observability |
|--------|-------------|------------|---------------|
| Kafka | Complex (ZooKeeper or KRaft) | High — partition management, rebalancing | Good (JMX, Prometheus exporter) |
| RabbitMQ | Moderate (Erlang cluster) | Moderate | Excellent (management UI, Prometheus) |
| ActiveMQ Artemis | Moderate (live-backup pair) | Moderate | Good (JMX, Prometheus) |
| NATS | Simple (peer-to-peer cluster) | Low | Good (built-in metrics endpoint) |
| Amazon SQS | None (managed) | Minimal | CloudWatch |

---

## Combining Multiple Systems

It is valid and common to use multiple messaging systems for different purposes:

```
User Action (HTTP)
       ↓
  REST API
       ├──→ RabbitMQ (immediate task queue for email, notifications)
       └──→ Kafka (event log for analytics, CDC, audit)

Kafka Consumer
       └──→ RabbitMQ (fan-out to specific worker queues)
```

**Kafka as the backbone**: All domain events flow through Kafka. Downstream services that need queue semantics (retry, routing) pick up from Kafka and push to RabbitMQ.

---

## Quick Decision Flowchart

```
Do you need to replay events after consumption?
├── Yes → Kafka (or Pulsar)
└── No
    ├── Do you need complex routing (topic patterns, headers, priority)?
    │   ├── Yes → RabbitMQ
    │   └── No
    │       ├── Do you need JMS compliance?
    │       │   ├── Yes → ActiveMQ Artemis
    │       │   └── No
    │       │       ├── Are you on AWS/Azure/GCP with minimal ops?
    │       │       │   └── Yes → Managed cloud service (SQS, Service Bus, Pub/Sub)
    │       │       └── Do you need ultra-low latency for microservices?
    │       │           └── Yes → NATS
    └── High throughput streaming + retention?
        └── Kafka
```

---

## Migration Patterns

### RabbitMQ → Kafka

Common when outgrowing RabbitMQ's throughput or needing replay:
1. Run Kafka alongside RabbitMQ
2. New events published to both
3. Consumers migrated to Kafka one at a time
4. RabbitMQ decommissioned once all consumers migrated

### Kafka → Managed Service

1. Evaluate Confluent Cloud, MSK, or Redpanda Cloud
2. Mirror Maker 2 for topic migration between Kafka clusters
3. Update producer/consumer bootstrap servers
4. Validate consumer groups and offsets

---

## Summary Table

| | Kafka | RabbitMQ | ActiveMQ Artemis | NATS |
|-|-------|---------|---------|------|
| **Message retention** | Configurable (days–forever) | Until consumed | Until consumed | Until consumed (JetStream) |
| **Replay** | ✓ | ✗ | ✗ | ✓ (JetStream) |
| **Routing** | By partition key | Exchange types | Addresses/Queues | Subject hierarchy |
| **Ordering** | Per partition | Per queue | Per queue | Per subject |
| **Throughput** | Highest | High | High | Very High |
| **Ops complexity** | High | Medium | Medium | Low |
| **JMS support** | Third-party | Third-party | Native | ✗ |
| **Ecosystem** | Huge (Connect, Streams) | Large | Large (JMS/Spring) | Growing |
