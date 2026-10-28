# Why Distributed Systems

## The Starting Point: The Monolith

Every system starts as a monolith — a single deployable unit where all components (UI, business logic, database access) live together. This is not a mistake. Monoliths are fast to build, easy to test, simple to deploy, and trivial to trace through a debugger.

```
┌─────────────────────────────────┐
│          Monolith               │
│  ┌──────┐ ┌──────┐ ┌────────┐  │
│  │  UI  │ │  BL  │ │  Data  │  │
│  └──────┘ └──────┘ └────────┘  │
└─────────────────────────────────┘
         │
    Single Process
    Single Database
    Single Deployment
```

The problem is not the monolith pattern itself — the problem is when the monolith cannot keep up with growth.

---

## Why Monoliths Hit Walls

### 1. Vertical Scaling Has a Ceiling

You can keep adding CPU, RAM, and faster disks to a single server — this is called **vertical scaling** (scale up). But it:

- Gets exponentially expensive
- Has a hardware ceiling (you cannot buy a machine with unlimited RAM)
- Creates a single point of failure
- Results in downtime during upgrades

### 2. A Single Slow Component Blocks Everything

In a monolith, if the PDF-generation module takes 30 seconds, it consumes threads from the same pool used by the login endpoint. One slow feature can degrade the entire application.

### 3. Deployments Are All-or-Nothing

Changing a single line in the billing module requires redeploying the entire application. At scale, this means:

- High deployment risk — a bug anywhere can take down everything
- Slow release cycles — teams coordinate one big deployment window
- Inability to scale individual features independently

### 4. Teams Block Each Other

As the team grows, multiple developers modifying the same codebase creates merge conflicts, coordination overhead, and coupling that slows everyone down.

### 5. Technology Lock-in

Every module must use the same language, runtime, and framework. The team cannot choose the right tool per job.

---

## The Three Axes of Scaling

The **Scale Cube** (from "The Art of Scalability") defines three ways to scale a system:

```
              Z-axis
              (Sharding)
              │
              │
              │
Y-axis ───────┼──────────────→ X-axis
(Decompose)   │                (Clone)
              │
```

| Axis | Name | What It Does | Example |
|------|------|--------------|---------|
| X | Horizontal Duplication | Run N identical copies behind a load balancer | 5 identical app servers |
| Y | Functional Decomposition | Split by capability/domain | Auth service, Order service |
| Z | Data Partitioning | Each instance handles a subset of data | Shard by customer ID |

Distributed systems typically apply all three axes simultaneously.

---

## What Makes a System "Distributed"

A system is distributed when:

- It runs across **multiple processes** (possibly on multiple machines)
- Processes communicate over a **network** (unreliable, has latency)
- There is **no shared memory** between processes — only message passing
- **Partial failures** are possible — some nodes can fail while others keep running

The network is the fundamental difference. A distributed system must operate correctly despite:
- Messages being delayed, reordered, or lost
- Individual nodes crashing
- Network partitions splitting the system in two

---

## Problems That Drive Distribution

### High Availability

A monolith on a single server has one failure domain. If the server crashes, the service is down. Distributing the system across multiple nodes means a single node failure does not cause a complete outage.

**Target**: 99.99% availability (52 minutes downtime/year) or higher. This is impossible with a single point of failure.

### Throughput Beyond One Machine

A single machine handles perhaps 50,000–100,000 requests/second for typical web workloads. Netflix, Google, and Amazon serve hundreds of millions of users. No single machine can handle that — you must spread the load.

### Geographic Distribution

Serving users in Tokyo from a datacenter in Virginia adds ~150ms of network latency. Placing services close to users requires running them in multiple geographic regions simultaneously.

### Fault Isolation

A bug in the recommendations engine should not crash the checkout flow. Separating into services means a failure has a limited blast radius.

### Independent Scalability

During checkout season, the payment service needs 10x more capacity but the user profile service does not. In a distributed system, you can scale each independently.

### Independent Deployability

Teams can release their service without coordinating with every other team.

---

## The Real Cost: Distributed Systems Are Hard

Distributing a system solves the scaling problems above but introduces a new class of problems that simply do not exist in a monolith:

| Problem | Description |
|---------|-------------|
| **Network unreliability** | Messages can be lost, delayed, or duplicated |
| **Partial failure** | Some nodes are up, some are down, some are unknown |
| **No shared clock** | You cannot compare timestamps across machines reliably |
| **Consistency vs availability** | When network partition occurs, you must choose one |
| **Distributed transactions** | ACID guarantees across services require complex protocols |
| **Observability** | A single request spans 20 services — tracing it is hard |
| **Operational complexity** | 100 services to deploy, configure, monitor, and secure |

These are not solvable problems — they are trade-offs to manage. This guide covers the tools and patterns that manage them well.

---

## Monolith → Distributed: A Migration Path

Don't start distributed. Start with a well-structured monolith (modular monolith) and distribute only when you have evidence you need to.

```
Modular Monolith
      │
      │  When: single module is bottleneck
      ▼
Extract hot service (strangler fig pattern)
      │
      │  When: multiple services needed independently
      ▼
SOA / Microservices
      │
      │  When: async communication needed, decoupling
      ▼
Event-driven architecture (Kafka / messaging)
      │
      │  When: global users, compliance requirements
      ▼
Multi-region deployment
```

**Signs you are ready to distribute:**

1. A specific module has 10x the load of the rest of the system
2. Deploy frequency is limited by coordination, not code quality
3. A single team owns a module that is large enough to be a product
4. You need different SLAs for different parts of the system
5. Your database is the bottleneck and cannot scale vertically anymore

**Signs you are NOT ready to distribute:**

1. Fewer than 10 engineers
2. Still finding product-market fit
3. You have not profiled where the actual bottleneck is
4. Your team has no experience running distributed infrastructure

---

## Key Terminology

| Term | Definition |
|------|-----------|
| **Service** | A self-contained unit of functionality with its own process and data |
| **Node** | A single machine or process in the distributed system |
| **Cluster** | A group of nodes working together |
| **Replication** | Keeping copies of data on multiple nodes for availability |
| **Partitioning / Sharding** | Splitting data across nodes so each node holds a subset |
| **Consensus** | Multiple nodes agreeing on a single value despite failures |
| **Leader election** | Choosing one node to coordinate a group |
| **Idempotency** | An operation that produces the same result if applied multiple times |
| **At-least-once delivery** | A message will be delivered, possibly more than once |
| **Exactly-once semantics** | A message is processed exactly once, even on retries |

---

## Next Steps

- [Core Concepts & CAP Theorem](./02-core-concepts-cap-theorem.md) — the theoretical foundation for every trade-off decision
- [Database Sharding](./03-database-sharding.md) — the first scaling problem most systems hit
- [Kubernetes](./containerization/kubernetes.md) — the operational foundation for running distributed services
