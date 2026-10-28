# API Engineering

> **Phase:** 5 — Networking and Web  
> **Status:** Not Started

## Overview

APIs are the contracts between services and clients — their design determines ease of use, evolvability, and correctness. This section covers the design patterns, protocols, and operational concerns that define good API engineering at scale.

## Topics

### REST Maturity Model

**What it is:** Richardson's Maturity Model categorizes REST APIs in four levels (0-3), from simple RPC-over-HTTP to fully hypermedia-driven APIs that embrace HTTP's resource model.

**Why it matters:** Most APIs claim to be "RESTful" but operate at Level 1 or 2 — understanding the model helps you design APIs that are truly resource-oriented and evolvable.

**Key concepts to understand:**
- Level 0: HTTP as transport tunnel (SOAP-like, single endpoint for all operations)
- Level 1: Resources (individual URLs for each resource, but all POST)
- Level 2: HTTP verbs (GET for reads, POST for creates, PUT/PATCH for updates, DELETE for deletes)
- Level 3: Hypermedia Controls (HATEOAS — responses contain links to available actions)
- Practical REST: Level 2 is sufficient for most APIs; Level 3 adds significant complexity for limited benefit
- Idempotency per verb: GET, PUT, DELETE are idempotent; POST is not

**Resources to explore:**
- [ ] (to be filled)

---

### API Versioning

**What it is:** Strategies for evolving APIs over time while maintaining backward compatibility with existing consumers.

**Why it matters:** API versioning strategy directly affects how many clients break on each deployment — choosing the right strategy early prevents costly migration projects.

**Key concepts to understand:**
- URL versioning: `/api/v1/orders` — simple, cacheable, widely understood
- Header versioning: `Api-Version: 2024-01-01` — cleaner URLs, harder to test
- Query string versioning: `?version=2` — simple but pollutes query strings
- Semantic versioning for APIs: breaking changes increment major version
- Sunset header: `Sunset: Sat, 1 Jan 2025 00:00:00 GMT` — deprecation signal in responses
- Contract testing: ensure API contracts are verified against all known consumers before deployment

**Resources to explore:**
- [ ] (to be filled)

---

### OpenAPI/Swagger

**What it is:** A language-agnostic specification for describing REST APIs — enabling automatic documentation, client SDK generation, and validation tooling.

**Why it matters:** OpenAPI is the standard for API documentation and tooling in the industry — well-maintained OpenAPI specs reduce integration time and client errors.

**Key concepts to understand:**
- OpenAPI 3.x specification: paths, operations, parameters, request bodies, responses, schemas
- Swashbuckle (Swashbuckle.AspNetCore): ASP.NET Core integration for automatic OpenAPI generation from controllers/minimal APIs
- `NSwag`: alternative with stronger client generation (TypeScript, C#) and Minimal API support
- Scalar/Swagger UI: interactive documentation UI from OpenAPI spec
- Schema reuse: `$ref` for referencing shared schemas — reduces duplication
- Generating strongly-typed clients: `NSwag.MSBuild` or Kiota for type-safe API clients

**Resources to explore:**
- [ ] (to be filled)

---

### GraphQL

**What it is:** A query language for APIs where clients specify exactly what data they need in a single request — eliminating over-fetching and under-fetching.

**Why it matters:** GraphQL is the preferred API style for mobile and complex frontend clients that need flexible data shapes — it reduces the number of requests and the total data transferred.

**Key concepts to understand:**
- Schema definition: types, queries, mutations, subscriptions — strongly typed by definition
- Resolver pattern: each field resolves independently — enables composition and data source federation
- N+1 problem in GraphQL: naive resolvers issue one query per object — solved with DataLoader (batching + caching)
- Introspection: clients can query the schema itself — powerful but a security concern (disable in production)
- Hot Chocolate / Strawberry Shake: .NET GraphQL server and client libraries
- Federation: combining multiple GraphQL schemas into one unified API (Apollo Federation)

**Resources to explore:**
- [ ] (to be filled)

---

### gRPC

**What it is:** A high-performance RPC framework using Protocol Buffers for serialization and HTTP/2 for transport — providing strongly typed, code-generated service contracts.

**Why it matters:** gRPC is the preferred protocol for inter-service communication in performance-sensitive microservice architectures — 5-10x smaller payloads and significantly lower CPU than JSON/REST.

**Key concepts to understand:**
- Proto file: service and message definitions compiled to strongly-typed code in any language
- RPC types: unary, server streaming, client streaming, bidirectional streaming
- Protobuf wire format: varint, length-delimited, field numbers — compact binary encoding
- gRPC status codes: richer than HTTP — OK, NOT_FOUND, UNAVAILABLE, DEADLINE_EXCEEDED
- Deadlines (not timeouts): passed end-to-end through the call chain — enabling cascade cancellation
- gRPC-JSON transcoding: expose gRPC services as REST/JSON API via HTTP annotations in proto file

**Resources to explore:**
- [ ] (to be filled)

---

### API Idempotency

**What it is:** Designing API operations so that making the same request multiple times produces the same result as making it once — critical for safe retries in unreliable network environments.

**Why it matters:** Network failures can leave the client uncertain whether a request was processed — idempotent endpoints allow safe retries without fear of double-execution.

**Key concepts to understand:**
- Idempotency key: client-generated unique ID (UUID) sent in a header (`Idempotency-Key: uuid`)
- Server-side deduplication: check if idempotency key was previously processed; return cached response if so
- Key storage: Redis or database for idempotency key records with TTL
- HTTP verb idempotency: GET, PUT, DELETE are idempotent by spec; POST requires explicit design
- Stripe's idempotency keys: the canonical reference implementation
- Idempotency vs caching: idempotency is about safe retries; caching is about performance

**Resources to explore:**
- [ ] (to be filled)

---

### Pagination Patterns

**What it is:** Techniques for returning large data sets in manageable chunks — essential for performance, memory management, and client usability.

**Why it matters:** Unbounded list endpoints will eventually cause OOM or timeouts — every list endpoint must have pagination, and the choice of pattern affects performance and consistency.

**Key concepts to understand:**
- Offset pagination: `?page=2&pageSize=20` — simple but expensive for deep pages (skip N rows) and inconsistent under concurrent writes
- Cursor pagination: `?after=encodedCursor` — consistent, efficient, but no random page access
- Keyset pagination: `WHERE id > lastSeenId LIMIT 20` — the efficient SQL implementation of cursor pagination
- Link headers: `Link: <url>; rel="next"` — RFC 5988 standard for pagination links
- Total count: expensive for large tables — consider estimating or paginating without it
- GraphQL Connections spec: standardized cursor-based pagination for GraphQL APIs

**Resources to explore:**
- [ ] (to be filled)

---

### Rate Limiting

**What it is:** Controlling the rate of requests that clients can make to an API to prevent abuse, protect downstream services, and ensure fair resource allocation.

**Why it matters:** Without rate limiting, a single misbehaving client can exhaust server resources and degrade service for all other clients.

**Key concepts to understand:**
- Fixed window: count requests in a fixed time window — simple but allows burst at window boundaries
- Sliding window: count requests in the last N seconds — smoother but more memory intensive
- Token bucket: replenish tokens at a rate, consume one per request — allows bursts up to bucket size
- Leaky bucket: constant outflow rate regardless of input burst — smooths traffic
- Rate limit headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, `Retry-After`
- Distributed rate limiting: Redis with Lua scripts or ASP.NET Core Rate Limiting middleware

**Resources to explore:**
- [ ] (to be filled)

---

### API Gateways

**What it is:** A managed service or application that acts as the single entry point for API traffic, providing routing, authentication, rate limiting, transformation, and observability.

**Why it matters:** API gateways centralize cross-cutting API concerns — eliminating duplication across microservices and providing a unified place for policy enforcement.

**Key concepts to understand:**
- Authentication/Authorization: validate JWT or API keys at the gateway before forwarding
- Request/response transformation: modify headers, bodies, or URLs as they pass through
- Load balancing: distribute traffic across backend service instances
- Caching: gateway-level response caching for read-heavy endpoints
- Analytics and logging: centralized request/response logging across all services
- Azure API Management: managed gateway with policy XML, developer portal, analytics, and multiple gateways per subscription

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Networking Fundamentals](./01-networking-fundamentals.md)
- [ASP.NET Core Internals](../Phase-02-Deep-DotNet-Internals/03-aspnet-core-internals.md)
- [Security Engineering](../Phase-10-Security-Engineering/01-application-security.md)
