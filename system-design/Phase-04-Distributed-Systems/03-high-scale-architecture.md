# High-Scale Architecture

> **Phase:** 4 — Distributed Systems  
> **Status:** Not Started

## Overview

Architecture patterns are the vocabulary for describing how systems are structured. This section covers the major architectural styles — from monoliths to microservices to event-driven systems — along with the design patterns that govern service boundaries, communication, and data flow at scale.

## Topics

### Monolith vs Microservices

**What it is:** A monolith packages all functionality in a single deployable unit; microservices split functionality into small, independently deployable services communicating over a network.

**Why it matters:** The choice between monolith and microservices is one of the most consequential architectural decisions — the wrong choice in either direction creates years of pain.

**Key concepts to understand:**
- Monolith advantages: simpler deployment, lower latency (in-process calls), easier debugging, no distributed transactions
- Microservices advantages: independent scaling, independent deployment, polyglot tech stacks, team autonomy
- Microservices costs: distributed system complexity, network latency, data consistency, operational overhead
- The "Majestic Monolith": a well-structured monolith often outperforms a hastily decomposed microservices system
- When to migrate: when specific parts need different scaling, deployment frequency, or tech stack
- Strangler fig pattern: incrementally migrate from monolith by routing portions to new services

**Resources to explore:**
- [ ] (to be filled)

---

### Modular Monoliths

**What it is:** A single deployable unit internally structured with well-defined module boundaries, explicit interfaces, and enforced dependency rules — combining the operational simplicity of a monolith with the organizational clarity of microservices.

**Why it matters:** Modular monoliths are often the right architecture for small-to-medium teams — they allow future migration to microservices along natural module boundaries without the upfront operational complexity.

**Key concepts to understand:**
- Module boundaries enforced via project/assembly separation in .NET
- No direct cross-module database access: modules communicate via interfaces or events
- Shared kernel: code shared across modules (IDs, domain events, infrastructure utilities)
- Vertical slicing within modules: feature-first organization rather than layer-first
- Testing module boundaries: architecture tests using ArchUnitNET or NetArchTest
- Migration path: a well-modularized monolith can be extracted to microservices by converting module interfaces to network interfaces

**Resources to explore:**
- [ ] (to be filled)

---

### Event-Driven Architecture

**What it is:** An architectural style where services communicate by producing and consuming events — decoupling producers from consumers through a message broker.

**Why it matters:** Event-driven architecture enables loose coupling, independent scaling, temporal decoupling, and easy addition of new consumers without modifying producers.

**Key concepts to understand:**
- Event: a fact about something that happened — immutable, past-tense, describes state change
- Event producer: publishes events without knowledge of consumers
- Event consumer: subscribes to relevant events and reacts
- Event broker (Kafka, Service Bus): routes events from producers to consumers
- Event-carried state transfer: events contain enough data for consumers to act without callbacks
- Choreography complexity: in large systems, event flows become hard to trace without proper tooling

**Resources to explore:**
- [ ] (to be filled)

---

### Hexagonal Architecture

**What it is:** An architectural pattern (also called Ports and Adapters) that places the domain model at the center, with all external dependencies (database, HTTP, messaging) behind interface adapters.

**Why it matters:** Hexagonal architecture makes the domain model testable in isolation, enables swapping infrastructure implementations, and enforces separation of business logic from technical concerns.

**Key concepts to understand:**
- Domain core: pure business logic with no infrastructure dependencies
- Ports: interfaces defined by the domain that the outside world must implement
- Primary/driving adapters: initiators of interaction (HTTP controllers, message consumers)
- Secondary/driven adapters: infrastructure implementations (database repositories, email senders)
- Dependency inversion: domain defines interfaces; adapters implement them
- Testing: unit test domain without infrastructure; integration test adapters in isolation

**Resources to explore:**
- [ ] (to be filled)

---

### Clean Architecture

**What it is:** Robert Martin's layered architecture with dependencies pointing inward — Entities → Use Cases → Interface Adapters → Frameworks & Drivers — enforcing that inner layers have no knowledge of outer layers.

**Why it matters:** Clean architecture enforces the dependency rule systematically, making the domain and use cases framework-agnostic, testable, and stable against infrastructure changes.

**Key concepts to understand:**
- Dependency rule: source code dependencies can only point inward (toward policies)
- Entities layer: enterprise-wide business rules — most stable
- Use cases layer: application-specific business rules — orchestrate entity behavior
- Interface adapters: convert data from external format to use case format (controllers, presenters)
- Frameworks layer: outermost ring — databases, web frameworks, UI
- Criticism: over-engineered for simple CRUD applications, boilerplate-heavy

**Resources to explore:**
- [ ] (to be filled)

---

### Domain-Driven Design

**What it is:** A software design approach focused on modeling software to match a domain's complexity, using a ubiquitous language shared between domain experts and developers.

**Why it matters:** DDD provides the vocabulary and patterns for designing complex business software — bounded contexts, aggregates, and domain events are foundational concepts for microservice decomposition.

**Key concepts to understand:**
- Ubiquitous language: a shared vocabulary between developers and domain experts used in code
- Bounded context: the boundary within which a domain model is internally consistent
- Aggregate: a cluster of domain objects treated as a single unit of consistency, accessed via a root
- Domain events: significant state changes in the domain that other parts of the system react to
- Value objects: immutable objects defined by their attributes, not identity (Money, Address)
- Strategic DDD patterns: Context Map, Shared Kernel, Customer-Supplier, Anti-Corruption Layer

**Resources to explore:**
- [ ] (to be filled)

---

### Vertical Slice Architecture

**What it is:** Organizing code by feature (vertical slice through all layers) rather than by technical layer (all controllers together, all services together) — each feature is a self-contained unit.

**Why it matters:** Vertical slices reduce coupling between features and make it easier to understand, change, and test a complete feature in isolation — teams working on different features don't step on each other.

**Key concepts to understand:**
- Feature folder: all code for a feature in one folder (command, handler, validator, query, response)
- MediatR pattern: decouple feature code from controllers using a mediator
- Minimal coupling between slices: slices share infrastructure but not business logic
- Shared kernel: code genuinely shared across slices (domain events, authentication, base classes)
- Trade-offs: some code duplication between slices vs tight coupling across features
- Contrast with layered architecture: vertical vs horizontal cuts through the codebase

**Resources to explore:**
- [ ] (to be filled)

---

### API Gateway Pattern

**What it is:** A single entry point for clients that routes requests to appropriate backend services, handling cross-cutting concerns like authentication, rate limiting, SSL termination, and request aggregation.

**Why it matters:** API gateways simplify clients by hiding service topology, enable per-service policies centrally, and reduce the number of client-service connections in a microservices architecture.

**Key concepts to understand:**
- Gateway functions: routing, auth/authz, rate limiting, SSL termination, request/response transformation
- Single gateway vs multiple gateways: risk of gateway becoming a monolith bottleneck
- Gateway aggregation: combine multiple backend responses into one client response
- YARP (Yet Another Reverse Proxy): Microsoft's .NET-native reverse proxy library for custom gateways
- Azure API Management: managed API gateway with policies, portal, analytics
- Latency addition: every gateway hop adds latency — measure p99, not just average

**Resources to explore:**
- [ ] (to be filled)

---

### BFF Pattern

**What it is:** Backend for Frontend — a separate API gateway or aggregation service tailored to the specific needs of a client type (mobile app, web app, partner API) rather than a single generic gateway.

**Why it matters:** Different clients have radically different data needs — a single API forced to serve all clients becomes bloated; BFF allows each client team to own their backend aggregation layer.

**Key concepts to understand:**
- One BFF per client type: mobile BFF, web BFF, partner API — each owned by the client team
- BFF responsibilities: aggregation, transformation, protocol translation, session management
- BFF vs general API gateway: BFF is client-specific; gateway is common infrastructure
- GraphQL as BFF: clients specify exactly what data they need — eliminates over/under-fetching
- Organizational alignment: BFF follows Conway's Law — teams own the BFF for their client
- BFF anti-pattern: sharing a BFF between incompatible clients defeats the purpose

**Resources to explore:**
- [ ] (to be filled)

---

### Service Mesh Basics

**What it is:** A dedicated infrastructure layer for handling service-to-service communication — implemented as a sidecar proxy (Envoy/Linkerd) that handles mTLS, retries, circuit breaking, and observability transparently.

**Why it matters:** Service meshes remove networking concerns from application code — services don't need to implement their own retry, circuit breaking, or mutual TLS; the mesh handles it.

**Key concepts to understand:**
- Data plane: Envoy sidecar proxies intercept all service traffic
- Control plane: Istio/Linkerd control plane distributes configuration to data plane proxies
- mTLS: mutual TLS between all services — zero-trust networking within the cluster
- Traffic management: load balancing, traffic splitting (canary deployments), fault injection
- Observability: automatic distributed tracing, metrics, and access logging without code changes
- Service mesh overhead: ~1ms latency added per hop, ~5-10% CPU overhead for proxy — worth measuring

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Core Distributed Systems](./01-core-distributed-systems.md)
- [Messaging Systems](./02-messaging-systems.md)
- [System Design Mastery](../Phase-13-System-Design-Mastery/02-high-level-design.md)
