# Distributed Systems: Complete Engineering Guide

A comprehensive reference covering why distributed systems exist, the problems they solve, and the tools and patterns used to build them reliably at scale.

---

## How to Use This Guide

Each document is self-contained. Start with the fundamentals (sections 1–2), then dive into whichever technology or problem domain is relevant to you. Every doc covers:

- **Core concepts** — what it is and how it works internally
- **Alternatives** — competing tools and when to pick each
- **License** — whether each tool is open-source, source-available, or commercial
- **Implementation checklist** — things to watch for in production
- **Best practices** — patterns that hold up at scale

---

## Table of Contents

### Foundations

| # | Topic | What It Covers |
|---|-------|----------------|
| 01 | [Why Distributed Systems](./01-why-distributed-systems.md) | Monolith limits, scaling axes, when to distribute |
| 02 | [Core Concepts & CAP Theorem](./02-core-concepts-cap-theorem.md) | CAP, PACELC, ACID vs BASE, consistency models, failure modes |

### Data Layer

| # | Topic | What It Covers |
|---|-------|----------------|
| 03 | [Database Sharding & Partitioning](./03-database-sharding.md) | Horizontal vs vertical partitioning, sharding strategies, consistent hashing |
| 04 | [Distributed Caching](./04-distributed-caching.md) | Redis, Memcached, cache-aside vs write-through, eviction, stampede |
| 20 | [Graph Databases & Neo4j](./20-graph-databases-neo4j.md) | Property graph model, Cypher, GDS algorithms, when to use graphs vs relational |

### Messaging & Event Streaming

| # | Topic | What It Covers |
|---|-------|----------------|
| 05 | [Apache Kafka](./messaging/kafka.md) | Log-based streaming, partitions, consumer groups, exactly-once semantics |
| 06 | [RabbitMQ](./messaging/rabbitmq.md) | AMQP, exchanges, queues, routing, acks, dead-letter queues |
| 07 | [ActiveMQ](./messaging/activemq.md) | JMS, topics vs queues, ActiveMQ Classic vs Artemis |
| 08 | [Messaging Comparison & Selection Guide](./messaging/comparison.md) | Kafka vs RabbitMQ vs ActiveMQ vs Pulsar vs NATS — when to use what |

### Containerization & Orchestration

| # | Topic | What It Covers |
|---|-------|----------------|
| 09 | [Docker](./containerization/docker.md) | Images, layers, networking, volumes, multi-stage builds, security |
| 10 | [Kubernetes](./containerization/kubernetes.md) | Architecture, workloads, networking, storage, autoscaling, best practices |

### DevOps & Deployment

| # | Topic | What It Covers |
|---|-------|----------------|
| 11 | [Helm Charts](./devops/helm-charts.md) | Templating, chart structure, values, hooks, OCI registry, Helmfile |
| 12 | [ArgoCD & GitOps](./devops/argocd-gitops.md) | GitOps principles, ArgoCD architecture, App of Apps, alternatives |
| 13 | [CI/CD Patterns](./devops/ci-cd-patterns.md) | Pipeline design, deployment strategies (blue-green, canary, rolling) |

### Cross-Cutting Concerns

| # | Topic | What It Covers |
|---|-------|----------------|
| 14 | [Service Mesh](./14-service-mesh.md) | Istio, Linkerd, Envoy, mTLS, traffic management, observability plane |
| 15 | [API Gateway & Ingress](./15-api-gateway.md) | Kong, NGINX, AWS API Gateway, rate limiting, auth, routing |
| 16 | [Observability](./16-observability.md) | Metrics (Prometheus/Grafana), tracing (Jaeger/Tempo), OpenTelemetry, SLOs |
| 17 | [Distributed Transactions](./17-distributed-transactions.md) | 2PC, Saga pattern, outbox pattern, idempotency, eventual consistency |
| 18 | [Security](./18-security.md) | Zero-trust, mTLS, Vault, RBAC, network policies, OPA |
| 19 | [Design Patterns](./19-design-patterns.md) | Circuit breaker, bulkhead, retry, CQRS, event sourcing, strangler fig |

### Observability & Operations

| # | Topic | What It Covers |
|---|-------|----------------|
| 21 | [Distributed Logging](./21-distributed-logging.md) | Structured logging, correlation IDs, ELK vs Loki, Fluent Bit, log pipelines |
| 22 | [Debugging Distributed Systems](./22-debugging-distributed-systems.md) | Common failures, network debugging, memory leaks, cascading failures, tools |
| 25 | [Disaster Recovery & HA](./25-disaster-recovery.md) | RTO/RPO, multi-AZ/multi-region, backup strategies, chaos engineering |

### APIs & Contracts

| # | Topic | What It Covers |
|---|-------|----------------|
| 23 | [Service Discovery & Config Management](./23-service-discovery.md) | Consul, etcd, Kubernetes DNS, Eureka, Spring Cloud Config |
| 24 | [Data Serialization & API Contracts](./24-data-serialization.md) | Protobuf, Avro, gRPC, OpenAPI, schema evolution, versioning |

---

## Problem → Solution Quick Reference

| Problem | Solutions Covered |
|---------|-------------------|
| Single database is a bottleneck | [Sharding](./03-database-sharding.md), [Caching](./04-distributed-caching.md), [CQRS](./19-design-patterns.md) |
| Need to model highly connected data | [Graph Databases / Neo4j](./20-graph-databases-neo4j.md) |
| Services need to communicate async | [Kafka](./messaging/kafka.md), [RabbitMQ](./messaging/rabbitmq.md), [Comparison](./messaging/comparison.md) |
| Deploy reliably across environments | [Docker](./containerization/docker.md), [Kubernetes](./containerization/kubernetes.md), [Helm](./devops/helm-charts.md) |
| Automate deployments via Git | [ArgoCD & GitOps](./devops/argocd-gitops.md) |
| Service-to-service security & reliability | [Service Mesh](./14-service-mesh.md), [Circuit Breaker](./19-design-patterns.md) |
| Can't find bugs in production | [Debugging Guide](./22-debugging-distributed-systems.md), [Observability](./16-observability.md) |
| Logs from 10 services are unrelated | [Distributed Logging & Correlation IDs](./21-distributed-logging.md) |
| Transactions span multiple services | [Distributed Transactions](./17-distributed-transactions.md) |
| Blast radius of a failure is too large | [Bulkhead + Circuit Breaker](./19-design-patterns.md) |
| Services can't find each other | [Service Discovery](./23-service-discovery.md) |
| API contracts breaking between services | [Data Serialization & Contracts](./24-data-serialization.md) |
| Need to survive a datacenter outage | [Disaster Recovery](./25-disaster-recovery.md) |
| "Works in dev, fails in production" | [Debugging](./22-debugging-distributed-systems.md#problem-9-works-in-dev-fails-in-production) |

---

## Reading Paths

**New to distributed systems**
→ 01 → 02 → 03 → 09 → 10 → 21 → 16

**Backend engineer adding async messaging**
→ 02 (consistency) → 08 (comparison) → 05 or 06 → 17 (transactions) → 21 (logging)

**Platform / DevOps engineer**
→ 09 → 10 → 11 → 12 → 13 → 16 → 25 (DR)

**Architect evaluating tradeoffs**
→ 02 → 08 → 14 → 15 → 17 → 19 → 24 (contracts) → 25 (DR)

**Engineer debugging a production incident**
→ 22 → 21 → 16

**Engineer adding a new data store**
→ 02 (CAP) → 03 (sharding) → 20 (graph) → 04 (caching) → 24 (serialization)

---

## Tool License Reference

Understanding licensing matters before adopting tools in production, especially for commercial products or SaaS offerings.

> **Key categories used below:**
> - **OSS** — Open-source (OSI-approved license). Free to use, modify, distribute.
> - **Source-available** — Code is public but license restricts certain commercial use. Read before adopting at scale.
> - **Commercial** — Paid license required for use or for enterprise features.
> - **Managed SaaS** — Pay-per-use cloud service. No license purchase; vendor manages the infrastructure.
> - **Dual** — Free community edition + paid enterprise edition.

### Messaging

| Tool | License | Notes |
|------|---------|-------|
| Apache Kafka | OSS (Apache-2) | Completely free. Confluent Platform adds enterprise features (commercial). |
| Redpanda | BSL (source-available) | Free for non-production; production use of BSL restricted after 4 years. |
| RabbitMQ | OSS (MPL-2) | Completely free. CloudAMQP / CloudRabbit are managed SaaS options. |
| ActiveMQ Classic | OSS (Apache-2) | Completely free. |
| ActiveMQ Artemis | OSS (Apache-2) | Completely free. Red Hat AMQ = commercial build on top. |
| Apache Pulsar | OSS (Apache-2) | Completely free. StreamNative = commercial managed service. |
| NATS / JetStream | OSS (Apache-2) | Completely free. Synadia = commercial cloud. |
| Amazon SQS/SNS | Managed SaaS | Pay per million requests. No license. AWS only. |
| Azure Service Bus | Managed SaaS | Pay per operation. Azure only. |
| Confluent Cloud | Managed SaaS | Kafka-compatible. Pay per usage. |

### Databases & Caching

| Tool | License | Notes |
|------|---------|-------|
| PostgreSQL | OSS (PostgreSQL License) | Free. |
| MySQL | OSS (GPL-2) / Commercial | Free community; Oracle MySQL Enterprise = commercial. |
| Redis | Dual: RSALv2 + SSPLv1 (source-available since 7.4) | Prior versions (≤7.2) are BSD-3. Redis Community Edition (7.4+) is source-available, not OSI. |
| Valkey | OSS (BSD-3) | Redis fork maintained by Linux Foundation. Drop-in replacement. |
| Memcached | OSS (BSD) | Free. |
| MongoDB | SSPL (source-available) | Free for self-hosting; SSPL restricts building competing SaaS. MongoDB Atlas = managed. |
| Cassandra | OSS (Apache-2) | Free. DataStax Astra = managed SaaS. |
| CockroachDB | Dual: BSL / Commercial | CockroachDB Core free; enterprise features commercial. |
| Neo4j | Dual: GPL-3 community / Commercial enterprise | Community = OSS, no clustering. Enterprise = commercial. AuraDB = managed SaaS. |
| ArangoDB | Dual: Apache-2 community / Commercial enterprise | Community OSS; Enterprise adds features. |
| JanusGraph | OSS (Apache-2) | Fully free. |
| Amazon Neptune | Managed SaaS | AWS only. Pay per I/O. |
| Elasticsearch | Elastic License 2.0 (source-available since 7.11) | Pre-7.11 versions Apache-2. Elastic Cloud = managed. |
| OpenSearch | OSS (Apache-2) | AWS fork of Elasticsearch. Drop-in compatible. Truly open-source. |
| ClickHouse | OSS (Apache-2) | Free. ClickHouse Cloud = managed. |

### Containers & Orchestration

| Tool | License | Notes |
|------|---------|-------|
| Docker Engine | OSS (Apache-2) | Free. Docker Desktop requires paid subscription for large companies. |
| Kubernetes | OSS (Apache-2) | Free. EKS/GKE/AKS = managed SaaS. |
| Helm | OSS (Apache-2) | Free. |
| containerd | OSS (Apache-2) | Free, CNCF. |
| k3s | OSS (Apache-2) | Free (Rancher/SUSE). |

### DevOps & GitOps

| Tool | License | Notes |
|------|---------|-------|
| ArgoCD | OSS (Apache-2) | Free, CNCF. Akuity = managed SaaS. |
| Flux | OSS (Apache-2) | Free, CNCF. Weave GitOps = commercial build. |
| Jenkins | OSS (MIT) | Free. CloudBees Jenkins = commercial. |
| GitHub Actions | Managed SaaS | Free tier; paid for more minutes. |
| GitLab CI | OSS (MIT) self-hosted / Managed SaaS | GitLab Community = free OSS; GitLab.com = SaaS. |
| Tekton | OSS (Apache-2) | Free, CNCF. |
| Argo Rollouts | OSS (Apache-2) | Free, canary + blue-green controller. |

### Service Mesh & Networking

| Tool | License | Notes |
|------|---------|-------|
| Istio | OSS (Apache-2) | Free, CNCF. Solo.io Gloo Mesh = commercial. |
| Linkerd | OSS (Apache-2) | Free, CNCF. Buoyant = commercial support. |
| Envoy Proxy | OSS (Apache-2) | Free, CNCF. Used internally by Istio. |
| Consul (HashiCorp) | BSL 1.1 (source-available since Aug 2023) | Was Apache-2; now BSL. Free for non-competing products. HCP Consul = managed SaaS. |
| Kong Gateway | OSS (Apache-2) for core / Commercial for Enterprise | Kong OSS free; Kong Enterprise (plugins, RBAC) = commercial. |
| NGINX | OSS (BSD-2) for core / Commercial for Plus | Free OSS; NGINX Plus adds HA, dynamic config = commercial. |
| Traefik | OSS (MIT) for Traefik Proxy / Commercial for Enterprise | Free OSS proxy; Traefik Enterprise = commercial. |
| cert-manager | OSS (Apache-2) | Free, CNCF. |

### Observability

| Tool | License | Notes |
|------|---------|-------|
| Prometheus | OSS (Apache-2) | Free, CNCF. |
| Grafana | OSS (AGPL-3) for OSS / Commercial for Grafana Cloud | Grafana OSS free but AGPL (copyleft). Grafana Cloud = managed SaaS. |
| Grafana Loki | OSS (AGPL-3) | Free. Grafana Cloud Logs = managed. |
| Grafana Tempo | OSS (AGPL-3) | Free. Grafana Cloud Traces = managed. |
| Jaeger | OSS (Apache-2) | Free, CNCF. |
| Zipkin | OSS (Apache-2) | Free. |
| OpenTelemetry | OSS (Apache-2) | Free, CNCF. Industry standard. |
| ELK Stack | Elastic License 2.0 (source-available) | Logstash/Beats = Apache-2. Elasticsearch/Kibana = Elastic License since 7.11. |
| OpenSearch | OSS (Apache-2) | AWS fork. Truly open-source alternative to Elasticsearch. |
| Datadog | Commercial SaaS | No free tier for production. Expensive at scale; full-featured. |
| New Relic | Commercial SaaS | Free tier available (100GB/month). Full-stack observability. |
| Pyroscope | OSS (AGPL-3) | Continuous profiling. Grafana Cloud Profiles = managed. |
| Fluent Bit | OSS (Apache-2) | Free. Best K8s log shipper. |
| Fluentd | OSS (Apache-2) | Free. More plugins, higher overhead than Fluent Bit. |
| Vector | OSS (MPL-2) | Free. Datadog project, but open-source. |

### Security & Secrets

| Tool | License | Notes |
|------|---------|-------|
| HashiCorp Vault | BSL 1.1 (source-available since Aug 2023) | Was Apache-2. BSL restricts competing SaaS. HCP Vault = managed. |
| OpenBao | OSS (MPL-2) | Linux Foundation fork of Vault pre-BSL. Drop-in replacement. |
| External Secrets Operator | OSS (Apache-2) | Free. Syncs secrets from AWS/GCP/Azure/Vault into K8s. |
| cert-manager | OSS (Apache-2) | Free. Automatic TLS certs. |
| Sealed Secrets | OSS (Apache-2) | Free. Bitnami. Encrypt K8s secrets for Git storage. |
| Open Policy Agent (OPA) | OSS (Apache-2) | Free, CNCF. |
| Falco | OSS (Apache-2) | Free, CNCF. Runtime security for K8s. |
| Trivy | OSS (Apache-2) | Free vulnerability scanner (Aqua Security). |

### Distributed Systems Infrastructure

| Tool | License | Notes |
|------|---------|-------|
| Apache ZooKeeper | OSS (Apache-2) | Free. Being replaced by KRaft in Kafka. |
| etcd | OSS (Apache-2) | Free, CNCF. Kubernetes backbone. |
| Temporal | OSS (MIT) for server / Commercial cloud | Free self-hosted; Temporal Cloud = managed SaaS. |
| Apache Flink | OSS (Apache-2) | Free. Stream processing. |
| Apache Spark | OSS (Apache-2) | Free. Batch + stream processing. |
| Debezium | OSS (Apache-2) | Free. CDC connector for Kafka. |

> **Important BSL/License-Change Tools to Watch**: HashiCorp changed Terraform, Vault, Consul, and Nomad to BSL in August 2023. Redis changed to RSALv2+SSPL in March 2024. Elasticsearch changed to Elastic License in 2021. If open-source is a hard requirement, use: **OpenTofu** (Terraform fork), **OpenBao** (Vault fork), **Valkey** (Redis fork), **OpenSearch** (Elasticsearch fork).

---

## Architect Decision Checklist

Questions to answer before choosing any tool in this guide:

### Licensing & Compliance
- [ ] Is the license compatible with your company's legal requirements?
- [ ] If source-available (BSL, SSPL, Elastic License) — does your use case fall within the allowed uses?
- [ ] Do you need commercial support? (open-source tools may need paid support contracts)
- [ ] Are there GDPR/HIPAA/PCI compliance requirements that affect where data can be stored?

### Operational Readiness
- [ ] Does your team have the expertise to operate this system?
- [ ] Is there a managed/cloud version that reduces operational burden?
- [ ] What happens when the tool has a critical security patch? Can you upgrade quickly?
- [ ] Is the community/vendor actively maintained?

### Scale & Performance
- [ ] What is your expected traffic volume now and in 2 years?
- [ ] Have you benchmarked the tool against your actual data and access patterns?
- [ ] Does the tool scale horizontally to meet your needs?

### Failure Modes
- [ ] What happens when this component fails?
- [ ] Is this component a single point of failure?
- [ ] Have you tested your recovery procedure for this component?
- [ ] Do you have observability (metrics, logs, traces) for this component?

### Integration
- [ ] Does this tool work well with your existing stack?
- [ ] Does it require changes to the data model of other services?
- [ ] How do you handle schema/API evolution?

---

## Common Stumbling Points for Developers & Architects

These are the mistakes that appear repeatedly in production:

| Stumble | What Goes Wrong | Where to Read |
|---------|----------------|--------------|
| Missing correlation IDs | Production bug investigation takes 10x longer; logs from 8 services tell unrelated stories | [Distributed Logging](./21-distributed-logging.md) |
| No idempotency keys | Payment charged twice on network retry | [Distributed Transactions](./17-distributed-transactions.md) |
| Unlimited retries without backoff | Retry storm takes down a recovering service | [Design Patterns](./19-design-patterns.md) |
| No circuit breaker | One slow downstream cascades into full outage | [Design Patterns](./19-design-patterns.md) |
| Default Kafka partitions | 1 partition = no consumer parallelism; hard to increase later | [Kafka](./messaging/kafka.md) |
| Mutable Docker tags in production | `latest` tag in prod means deploys are non-reproducible | [Docker](./containerization/docker.md) |
| Secrets in ConfigMaps or env vars | Credentials visible to anyone with kubectl access | [Security](./18-security.md) |
| No resource limits on pods | One misbehaving pod starves all other pods on the node | [Kubernetes](./containerization/kubernetes.md) |
| Untested backups | Backup exists but restore process has never been run | [Disaster Recovery](./25-disaster-recovery.md) |
| Sharding by timestamp | All writes go to the "current" shard — hotspot | [Database Sharding](./03-database-sharding.md) |
| No DLQ for message queues | Failed messages silently lost; bugs invisible for weeks | [RabbitMQ](./messaging/rabbitmq.md) / [Kafka](./messaging/kafka.md) |
| Joining across microservice databases | Services tightly coupled through shared DB; can't deploy independently | [Design Patterns](./19-design-patterns.md) |
| Missing readiness probe | Pod added to service before it can handle traffic → 50x errors at deploy | [Kubernetes](./containerization/kubernetes.md) |
| Protobuf field number reuse | Old serialized messages decode incorrectly | [Data Serialization](./24-data-serialization.md) |
| DNS TTL too high during failover | Clients cache old address for hours after failover | [Disaster Recovery](./25-disaster-recovery.md) |
| No load test before launch | System works for 100 users; falls over at 10k | [Why Distributed Systems](./01-why-distributed-systems.md) |
| Choosing microservices too early | Operational complexity without the scale benefits | [Why Distributed Systems](./01-why-distributed-systems.md) |
