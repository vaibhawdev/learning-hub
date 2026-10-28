# Cloud Architecture

> **Phase:** 6 — Cloud and Azure  
> **Status:** Not Started

## Overview

Cloud architecture patterns determine whether a system survives failures, scales gracefully, and costs appropriately. This section covers the architectural concerns that separate production-ready cloud systems from development-grade deployments — high availability, multi-region, DR, autoscaling, and multi-tenancy.

## Topics

### High Availability

**What it is:** System design that minimizes downtime by eliminating single points of failure, using redundancy, health checks, and automatic failover to maintain service despite component failures.

**Why it matters:** Modern SLAs require 99.9%+ availability — achieving this requires deliberate design, not just hoping infrastructure doesn't fail.

**Key concepts to understand:**
- Availability zones: physically separate datacenters within a region — zone-redundant deployments survive one zone failure
- SLA calculation: combined SLA = product of component SLAs; 99.9% × 99.9% = 99.8%
- Active-active vs active-passive: active-active serves traffic from all instances; active-passive promotes a standby
- Health checks: liveness (is the process alive) vs readiness (can it serve traffic) — distinct checks for different purposes
- Graceful degradation: serve a reduced feature set when a dependency is unavailable
- Chaos engineering baseline: system should survive zone failure without customer-visible impact

**Resources to explore:**
- [ ] (to be filled)

---

### Fault Tolerance

**What it is:** The ability of a system to continue operating correctly in the presence of component failures — going beyond HA by designing for continued operation rather than just recovery.

**Why it matters:** Fault tolerance transforms failures from outages into invisible events — designing for it requires thinking adversarially about every dependency.

**Key concepts to understand:**
- Redundancy: N+1 or N+2 — extra capacity absorbs failure without performance impact
- Isolation: bulkheads prevent failure propagation between system components
- Timeouts: every external call must have a timeout — blocking forever cascades to full service failure
- Fallbacks: predefined safe responses when a dependency fails (cached response, default value, graceful error)
- Failure injection testing: deliberately introduce failures (chaos engineering) to validate fault tolerance
- Byzantine fault tolerance: system continues operating despite arbitrary (including malicious) component behavior

**Resources to explore:**
- [ ] (to be filled)

---

### Multi-Region Systems

**What it is:** Deploying application infrastructure across multiple Azure regions to reduce latency for global users, survive regional outages, and comply with data residency requirements.

**Why it matters:** A regional Azure outage affects all services in that region — multi-region deployment is the only way to provide continuous service during a regional failure.

**Key concepts to understand:**
- Active-active multi-region: both regions serve traffic simultaneously — highest availability, complex data sync
- Active-passive multi-region: primary region serves all traffic; secondary is warm standby
- Data replication: synchronous (zero data loss, higher latency) vs asynchronous (potential data loss, lower latency)
- Global routing: Azure Traffic Manager (DNS-based), Azure Front Door (anycast routing) for directing users to nearest region
- Data sovereignty: some regulations require data to remain in specific geographic regions
- Multi-region write conflicts: require conflict resolution strategy (last-write-wins, custom merge)

**Resources to explore:**
- [ ] (to be filled)

---

### DR Strategies

**What it is:** Disaster Recovery strategies define how a system recovers from major failures or disasters, characterized by Recovery Time Objective (RTO) and Recovery Point Objective (RPO).

**Why it matters:** DR planning separates systems that recover from disasters from those that become them — every production system needs a documented DR strategy with tested runbooks.

**Key concepts to understand:**
- RTO: maximum acceptable downtime (time to restore service after failure)
- RPO: maximum acceptable data loss (time window of data that can be lost)
- Backup and restore: cheapest, highest RTO/RPO — acceptable for dev/test, not production
- Pilot light: minimal infrastructure running in DR region, scale up on disaster — medium RTO
- Warm standby: scaled-down but running replica in DR region — lower RTO, higher cost
- Hot standby (active-passive): full replica running in DR region, instant failover — lowest RTO, highest cost

**Resources to explore:**
- [ ] (to be filled)

---

### Cost Optimization

**What it is:** Strategies for reducing cloud spending while maintaining performance and reliability — the practice of matching resource consumption to actual needs.

**Why it matters:** Cloud bills scale with usage — without deliberate cost optimization, cloud spend grows faster than business value, eventually becoming a business constraint.

**Key concepts to understand:**
- Right-sizing: match VM and database sizes to actual utilization — Azure Advisor recommendations
- Reserved Instances / Savings Plans: commit to 1 or 3 years for 30-60% discount on compute
- Spot instances: unused Azure capacity at up to 90% discount — suitable for interruptible workloads
- Storage tiering: automatically move cold data to Cool or Archive tiers
- Cost allocation tags: tag resources with cost center, team, and environment for showback/chargeback
- Azure Cost Management + Budgets: alerting when spending approaches budget limits

**Resources to explore:**
- [ ] (to be filled)

---

### Autoscaling

**What it is:** Automatically adjusting the number of compute instances based on load metrics — scaling out to handle traffic spikes and scaling in to reduce cost during low utilization.

**Why it matters:** Autoscaling is what makes cloud economics work — paying for resources when needed and releasing them when not, vs provisioning for peak load at all times.

**Key concepts to understand:**
- Horizontal scaling (scale out/in): add or remove instances — preferred, more flexible
- Vertical scaling (scale up/down): increase or decrease instance size — usually requires restart
- Scale-out rules: trigger on CPU%, memory%, HTTP queue length, custom metrics
- Scale-in protection: prevent scale-in during active work processing or within a minimum window
- KEDA (Kubernetes Event-Driven Autoscaling): scale AKS pods based on external event sources (Service Bus queue depth)
- Warm-up time: allow new instances to initialize before receiving traffic — health check must pass first

**Resources to explore:**
- [ ] (to be filled)

---

### Multi-Tenant Systems

**What it is:** A system serving multiple customers (tenants) from shared infrastructure — requiring isolation, fair resource allocation, and data segregation between tenants.

**Why it matters:** Multi-tenancy is foundational to SaaS economics — sharing infrastructure reduces per-tenant cost but introduces isolation and noisy neighbor challenges.

**Key concepts to understand:**
- Isolation models: shared database (row-level security), database-per-tenant, infrastructure-per-tenant
- Noisy neighbor: one tenant's heavy usage degrading others — mitigation via per-tenant rate limiting and bulkheads
- Tenant identification: from JWT claims, subdomain, header, or route prefix
- Data isolation: row-level security in SQL, `HasQueryFilter` in EF Core, Cosmos DB partition key per tenant
- Onboarding automation: tenant provisioning must be automated for SaaS to scale
- Pricing tiers: isolation level often maps to pricing tier (shared DB = basic, dedicated DB = premium)

**Resources to explore:**
- [ ] (to be filled)

---

### Zero Trust Networking

**What it is:** A security model that assumes no network location is inherently trusted — every request must be authenticated and authorized regardless of whether it originates inside or outside the corporate network.

**Why it matters:** Traditional perimeter security fails in cloud and hybrid environments — zero trust aligns security with the reality that threats can originate from anywhere, including inside the network.

**Key concepts to understand:**
- "Never trust, always verify": identity, device health, and explicit authorization required for every request
- Microsegmentation: minimize lateral movement by restricting service-to-service connectivity with NSGs and firewalls
- mTLS between services: all service-to-service communication authenticated and encrypted
- Conditional Access: Azure AD policies that evaluate context (user, device, location, risk) before granting access
- Just-in-time access: temporary elevated access for admin operations instead of persistent privileged roles
- Azure Private Endpoints: remove public internet exposure from all PaaS services

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Azure Core](./01-azure-core.md)
- [Infrastructure as Code](./03-infrastructure-as-code.md)
- [Reliability Engineering](../Phase-09-Reliability-And-Observability/02-sre-concepts.md)
