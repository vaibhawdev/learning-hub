# Azure Core

> **Phase:** 6 — Cloud and Azure  
> **Status:** Not Started

## Overview

Azure is the cloud platform for most .NET enterprise workloads. This section covers the foundational Azure services — from networking primitives to compute, messaging, and data services — that every cloud-native .NET engineer needs to understand and configure correctly in production.

## Topics

### Resource Groups

**What it is:** Logical containers in Azure that hold related resources for an application or environment — providing lifecycle management, access control, and billing grouping.

**Why it matters:** Resource groups are the fundamental unit of Azure organization — poor resource group design makes access control, cost tracking, and lifecycle management difficult.

**Key concepts to understand:**
- All resources in a group share a lifecycle (create, delete together) and a region for metadata
- Naming conventions: environment + application + region (e.g., `rg-myapp-prod-eastus`)
- RBAC scope: permissions granted at resource group level apply to all contained resources
- Tags: key-value metadata for cost allocation, environment marking, and automated policies
- Locks: `ReadOnly` and `Delete` locks prevent accidental modification/deletion
- ARM templates and Bicep operate at resource group scope by default

**Resources to explore:**
- [ ] (to be filled)

---

### VNets / Subnets

**What it is:** Virtual Networks (VNets) and Subnets are the networking fabric in Azure — VNets provide isolated private network space, subnets segment VNets for resource grouping and security.

**Why it matters:** VNet design determines traffic flow, security boundaries, and connectivity options — a poorly designed VNet is expensive and difficult to fix without downtime.

**Key concepts to understand:**
- VNet address space: CIDR block (e.g., 10.0.0.0/16) — plan for future growth
- Subnet segmentation: separate subnets for web tier, app tier, data tier, management
- Service endpoints vs Private Endpoints: endpoint routes traffic to Azure PaaS over Microsoft backbone
- VNet peering: connect VNets within or across regions — no gateway, low latency, not transitive
- VNet integration for App Services: allows App Service to reach resources in a VNet
- Network Watcher: connectivity diagnostics, packet capture, flow logs for VNet traffic

**Resources to explore:**
- [ ] (to be filled)

---

### NSGs

**What it is:** Network Security Groups — stateful firewall rules (allow/deny by source IP, port, protocol) applied to subnets or individual NICs to control network traffic flow.

**Why it matters:** NSGs are the primary network access control mechanism in Azure — misconfigured NSGs either leave services exposed or cause mysterious connectivity failures.

**Key concepts to understand:**
- Inbound and outbound rules: priority-ordered rules evaluated top-down, first match wins
- Default rules: Azure adds default allow rules for VNet traffic and deny rules for internet (inbound)
- Service tags: logical groups of Azure service IP ranges (AzureLoadBalancer, Internet, VirtualNetwork) for easier rule management
- Application Security Groups (ASG): group VMs logically, write NSG rules against groups instead of IP addresses
- NSG flow logs: record accepted/denied traffic — feed to Log Analytics for traffic analysis
- Effective security rules: merged view of subnet NSG + NIC NSG for debugging connectivity

**Resources to explore:**
- [ ] (to be filled)

---

### DNS Zones

**What it is:** Azure DNS provides public DNS hosting for custom domains and private DNS zones for name resolution within VNets.

**Why it matters:** Correct DNS configuration is essential for service discovery, private endpoint resolution, and custom domain routing in Azure.

**Key concepts to understand:**
- Public DNS zones: delegate domain to Azure nameservers — `ns1-01.azure-dns.com` etc.
- Private DNS zones: link to VNets for internal name resolution (e.g., `privatelink.database.windows.net`)
- Auto-registration: VMs in a linked VNet automatically register their private IPs
- DNS resolution order: VNet DNS → Azure DNS → custom DNS server
- Private endpoints: automatically create DNS records in private zones for PaaS service resolution
- Alias records: route apex domain (`@`) to Azure Traffic Manager, CDN, or Front Door

**Resources to explore:**
- [ ] (to be filled)

---

### Managed Identity

**What it is:** An Azure AD identity automatically created for Azure resources (VMs, App Services, Functions, AKS pods) — eliminating the need for credentials in application code.

**Why it matters:** Managed identities are the zero-credential approach to Azure authentication — storing secrets in code or config is a security risk that Managed Identity eliminates entirely.

**Key concepts to understand:**
- System-assigned: identity tied to the resource lifecycle — deleted when resource is deleted
- User-assigned: standalone identity resource, assignable to multiple services — survives resource deletion
- DefaultAzureCredential: tries multiple auth methods in order — Managed Identity in production, developer credential locally
- RBAC role assignment: grant specific Azure RBAC roles to the identity for least-privilege access
- Workload Identity for AKS: pod-level identity using federated credentials
- MSI endpoint: internal Azure IMDS endpoint that Managed Identity SDK calls to get tokens

**Resources to explore:**
- [ ] (to be filled)

---

### Key Vault

**What it is:** Azure's managed secrets, keys, and certificate store — providing secure storage with access auditing, automatic rotation, and hardware security module (HSM) backing.

**Why it matters:** Key Vault is the correct place for all secrets, connection strings, and certificates in Azure — avoiding hardcoded secrets in code and config files.

**Key concepts to understand:**
- Secrets: arbitrary key-value strings with versioning and expiry
- Keys: cryptographic keys for signing/encryption — can be HSM-backed (Premium tier)
- Certificates: X.509 certificates with automatic renewal integration (Let's Encrypt, DigiCert)
- Access policies vs RBAC: newer deployments should use Azure RBAC for Key Vault access
- Reference syntax: App Service / Functions can reference Key Vault secrets directly in app settings: `@Microsoft.KeyVault(SecretUri=...)`
- Soft delete and purge protection: 90-day recovery window + prevent permanent deletion

**Resources to explore:**
- [ ] (to be filled)

---

### Private Endpoints

**What it is:** A network interface in your VNet with a private IP address that connects to an Azure PaaS service — traffic flows over the Microsoft backbone, never the public internet.

**Why it matters:** Private endpoints are the security baseline for production Azure PaaS services — without them, services are accessible from the public internet even with firewall rules.

**Key concepts to understand:**
- Private Link: the technology powering private endpoints — creates a private IP in your VNet for PaaS
- DNS configuration: private endpoints require DNS records in private zones (e.g., `privatelink.vault.azure.net`)
- Supported services: Storage, SQL, Cosmos DB, Key Vault, Service Bus, Redis, AKS, Container Registry
- Approval workflow: private endpoint connections must be approved by the service owner
- Network policy: subnet must have `privateEndpointNetworkPolicies: Disabled` for private endpoints
- Cost: private endpoint has an hourly charge plus per-GB data processed charge

**Resources to explore:**
- [ ] (to be filled)

---

### App Service

**What it is:** Azure's PaaS offering for hosting web applications, REST APIs, and background services — abstracting the VM layer with managed scaling, deployment slots, and built-in SSL.

**Why it matters:** App Service is the fastest path to production for .NET web applications — understanding its plan types, scaling model, and deployment options prevents configuration mistakes.

**Key concepts to understand:**
- App Service Plans: Free/Shared (no SLA), Basic/Standard/Premium (dedicated VMs), Isolated (App Service Environment)
- Deployment slots: staging/production slots with warm-up and swap — enables blue-green deployments
- Scale out: manual or autoscale rules based on CPU, memory, HTTP queue length
- VNet integration: enable private outbound connectivity from App Service to a VNet
- Always On: prevent app from unloading when idle — required for background services
- Health check: App Service can remove instances that fail a health check endpoint

**Resources to explore:**
- [ ] (to be filled)

---

### Azure Functions

**What it is:** Azure's serverless compute service — event-triggered, automatically scaled, pay-per-execution functions that run in response to HTTP requests, timers, queues, or other Azure service triggers.

**Why it matters:** Azure Functions are the right tool for event-driven processing, scheduled jobs, and lightweight APIs where you pay only for execution and don't manage infrastructure.

**Key concepts to understand:**
- Hosting plans: Consumption (serverless, cold start), Premium (pre-warmed, VNet), Dedicated (App Service Plan)
- Triggers: HTTP, Timer, Service Bus, Event Hub, Blob Storage, Cosmos DB change feed
- Bindings: declarative input/output connections to Azure services — eliminate boilerplate
- Durable Functions: stateful function orchestrations — fan-out/fan-in, human approval, long-running sagas
- Cold start: Consumption plan functions take 1-3 seconds to start on first invocation — Premium plan eliminates this
- Isolated worker model: .NET 8 isolated process model vs in-process — isolated recommended for new projects

**Resources to explore:**
- [ ] (to be filled)

---

### AKS

**What it is:** Azure Kubernetes Service — a managed Kubernetes control plane where Azure handles cluster upgrades, patching, and scaling of control plane nodes.

**Why it matters:** AKS is the standard container orchestration platform for complex microservices on Azure — understanding its node pools, networking, and scaling enables reliable production deployments.

**Key concepts to understand:**
- Node pools: system (control plane workloads) and user (application workloads) — separate for taint/toleration isolation
- Azure CNI vs kubenet: CNI gives pods real VNet IPs (better for Private Link); kubenet uses NAT
- Cluster autoscaler: scales node count based on pending pods (scale up) or underutilization (scale down)
- Workload Identity: AAD pod identity replacement — federated credentials for pod-level Managed Identity
- AGIC (Application Gateway Ingress Controller): integrate Azure Application Gateway as Kubernetes Ingress
- Node image upgrades: auto-upgrade channels for security patches vs manual control for stability

**Resources to explore:**
- [ ] (to be filled)

---

### Service Bus

**What it is:** Azure Service Bus is an enterprise messaging service supporting queues (point-to-point) and topics/subscriptions (pub/sub) with exactly-once delivery, sessions, and dead-lettering.

**Why it matters:** Service Bus is the standard asynchronous messaging backbone for Azure-native microservices — its premium tier and geo-redundancy make it suitable for mission-critical workloads.

**Key concepts to understand:**
- Queues vs Topics: queue for work distribution; topic+subscription for fan-out publishing
- Message sessions: ordered, exclusive processing per session ID — enables per-entity serialization
- Dead-letter queue: automatic quarantine of undeliverable messages
- Message deferral: defer processing of a message for later while continuing to process others
- Premium tier: dedicated capacity, VNet integration, larger messages (up to 100MB), geo-replication
- Service Bus Explorer: GUI tool for inspecting queues, dead-letter messages, and replaying

**Resources to explore:**
- [ ] (to be filled)

---

### Event Grid

**What it is:** Azure's event routing service that delivers events from Azure resources and custom applications to subscribers (Azure Functions, Logic Apps, webhooks) using a pub/sub model with at-least-once delivery.

**Why it matters:** Event Grid connects Azure services reactively without polling — a Blob uploaded triggers a Function, a Resource Group change triggers an audit log — enabling event-driven Azure automation.

**Key concepts to understand:**
- Event domains: publishing to many topics from one source (e.g., one event per customer)
- Event subscriptions with filters: content-based filtering on event type, subject prefix/suffix
- Dead-letter destination: blob storage for events that cannot be delivered after retries
- Custom topics: your application publishes events to a custom Event Grid topic
- CloudEvents schema: standard event envelope (vs Event Grid's own schema)
- Event Grid vs Service Bus: Event Grid for reactive eventing (fire and forget); Service Bus for reliable message delivery

**Resources to explore:**
- [ ] (to be filled)

---

### Event Hub

**What it is:** Azure's high-throughput event streaming service designed for telemetry ingestion — similar to Kafka, supporting millions of events per second with partitioned, time-ordered storage and consumer groups.

**Why it matters:** Event Hub is the standard telemetry and event streaming backbone in Azure — used for IoT, clickstream, application logs, and any high-volume event ingestion scenario.

**Key concepts to understand:**
- Partitions: up to 32 per event hub — fixed at creation, determine max parallelism
- Consumer groups: independent logical views of the stream per consumer application
- Capture: automatically store event hub data to Blob Storage or Data Lake in Avro format
- Kafka endpoint: Event Hub exposes a Kafka-compatible endpoint — migrate Kafka producers/consumers with config change
- Throughput units (TUs) / Processing units (PUs): scaling unit for Standard/Premium tiers
- EventProcessorClient: .NET client that distributes partition ownership across processor instances with checkpointing

**Resources to explore:**
- [ ] (to be filled)

---

### Cosmos DB

**What it is:** Azure's globally distributed, multi-model NoSQL database offering single-digit millisecond latency, multi-region writes, and five consistency levels.

**Why it matters:** Cosmos DB is the go-to globally distributed database for Azure-native applications requiring low latency worldwide — its partitioning and consistency model require careful design.

**Key concepts to understand:**
- Request Units (RU/s): normalized cost unit for all operations — provision or autoscale RU/s
- Partition key: determines data distribution — choose for even distribution and co-location of related items
- Consistency levels: Strong → Bounded Staleness → Session → Consistent Prefix → Eventual
- Multi-region writes: any region can accept writes — conflict resolution via LWW or custom merge procedure
- Change feed: ordered stream of all changes — enables event sourcing, CDC, and real-time projections
- Point reads: read by partition key + id = single RU — cheapest operation, prefer over queries

**Resources to explore:**
- [ ] (to be filled)

---

### Azure SQL

**What it is:** Azure's managed SQL Server offering — available as single database, elastic pool, or managed instance — providing the full SQL Server engine with automated backups and high availability.

**Why it matters:** Azure SQL is the primary relational database for Azure-native .NET applications — understanding its tiers, HA model, and performance features enables correct configuration.

**Key concepts to understand:**
- DTU vs vCore pricing: DTU is bundled compute+storage; vCore gives independent scaling and more control
- Elastic pools: share DTU/vCore across databases — cost-effective for multi-tenant SaaS
- Managed Instance: near-100% SQL Server compatibility — for lift-and-shift migrations
- Business Critical vs General Purpose: Business Critical has in-memory OLTP, local SSD, read replicas
- Accelerated Database Recovery (ADR): dramatically reduces recovery time from long transactions
- Query Performance Insight and Automatic Tuning: built-in query diagnostics and index recommendations

**Resources to explore:**
- [ ] (to be filled)

---

### Azure Redis

**What it is:** Azure Cache for Redis — a managed Redis service with options from basic (single node) to enterprise (Redis Enterprise with active geo-replication and RediSearch).

**Why it matters:** Azure Redis is the standard caching layer for Azure-native applications — understanding tier capabilities and geo-replication options prevents choosing the wrong tier for HA requirements.

**Key concepts to understand:**
- Tiers: Basic (dev/test, no SLA), Standard (primary+replica, SLA), Premium (clustering, VNet, persistence), Enterprise
- Redis Cluster on Premium tier: horizontal sharding across multiple nodes
- Geo-replication: Premium passive geo-replication; Enterprise active geo-replication for multi-region writes
- Data persistence: RDB or AOF — Premium tier only
- Private endpoint: connect Redis to VNet without public internet exposure
- Cache-aside pattern implementation with `IDistributedCache` in .NET

**Resources to explore:**
- [ ] (to be filled)

---

### Storage Accounts

**What it is:** Azure Storage Accounts provide durable, highly available object (Blob), file (File Share), queue (Queue), and table (Table) storage under a single account namespace.

**Why it matters:** Storage Accounts are the foundational durable storage primitive in Azure — understanding replication options, tiers, and access patterns prevents data loss and cost surprises.

**Key concepts to understand:**
- Storage types: Blob (object storage), Files (SMB/NFS file shares), Queues, Tables
- Redundancy: LRS (3 copies in one datacenter), ZRS (3 AZs), GRS (secondary region), GZRS
- Access tiers: Hot (frequent access), Cool (infrequent, lower storage cost), Archive (offline, cheapest)
- Lifecycle management: automatically tier or delete blobs based on age
- SAS tokens: time-limited, permission-scoped access tokens for external clients
- Hierarchical namespace (ADLS Gen2): enables directory-based operations and POSIX permissions for data lake workloads

**Resources to explore:**
- [ ] (to be filled)

---

### Application Insights

**What it is:** Azure's APM (Application Performance Monitoring) service — collecting telemetry (requests, dependencies, exceptions, traces, metrics) from applications with automatic correlation and distributed tracing.

**Why it matters:** Application Insights is the observability backbone for Azure-hosted .NET applications — understanding its sampling, correlation, and alerting features is essential for production operations.

**Key concepts to understand:**
- Auto-instrumentation: Azure App Service and AKS can instrument apps without code changes
- SDK instrumentation: `services.AddApplicationInsightsTelemetry()` for custom telemetry and enrichment
- Distributed tracing: `Operation-Id` propagated across services — view full request chain in Application Map
- Adaptive sampling: automatically reduce telemetry volume under high load while preserving representative samples
- Log Analytics workspace: Application Insights data stored in Log Analytics — Kusto query language (KQL)
- Smart Detection: automatic anomaly detection for failure rates, latency spikes, and unusual patterns

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Cloud Architecture](./02-cloud-architecture.md)
- [Infrastructure as Code](./03-infrastructure-as-code.md)
- [Containers and Kubernetes](../Phase-07-Containers-And-Kubernetes/02-kubernetes.md)
