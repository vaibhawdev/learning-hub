# Networking Fundamentals

> **Phase:** 5 — Networking and Web  
> **Status:** Not Started

## Overview

Network fundamentals are the invisible plumbing of every distributed system. Understanding the protocols that carry your data — from TCP's reliability mechanisms to HTTP/3's QUIC transport — helps you design for real network conditions, diagnose latency problems, and configure load balancers and CDNs correctly.

## Topics

### TCP/IP

**What it is:** The foundational protocol suite of the internet — IP handles packet routing and addressing, TCP provides reliable, ordered, connection-oriented byte stream delivery on top of IP.

**Why it matters:** TCP's congestion control, flow control, and connection establishment costs affect the latency and throughput characteristics of every network service you build.

**Key concepts to understand:**
- Three-way handshake: SYN → SYN-ACK → ACK — adds one round trip before data can flow
- Flow control: receiver advertises window size to prevent sender from overwhelming it
- Congestion control: slow start, AIMD (additive increase, multiplicative decrease), CUBIC
- TCP head-of-line blocking: packet loss stalls all subsequent data in the stream
- `TIME_WAIT` state: 2×MSL wait after closing to prevent old segments from confusing new connections
- `SO_REUSEPORT`, `TCP_NODELAY`, buffer sizing: socket options relevant for high-performance servers

**Resources to explore:**
- [ ] (to be filled)

---

### UDP

**What it is:** An unreliable, connectionless transport protocol — no handshake, no retransmission, no ordering — providing low-latency datagram delivery at the cost of reliability.

**Why it matters:** UDP is the transport for DNS, QUIC, VoIP, gaming, and real-time video — its characteristics are chosen deliberately for scenarios where latency matters more than reliability.

**Key concepts to understand:**
- No connection overhead: first packet can carry data, unlike TCP's three-way handshake
- No retransmission: lost packets are lost — application layer must handle reliability if needed
- No head-of-line blocking: each datagram is independent
- UDP-based reliable protocols: QUIC, SCTP, WebRTC's DTLS/SRTP
- Amplification attacks: UDP is used in DDoS reflection attacks (DNS, NTP, Memcached amplification)
- Kernel UDP receive buffer tuning: `net.core.rmem_max` for high-throughput UDP receivers

**Resources to explore:**
- [ ] (to be filled)

---

### DNS

**What it is:** The Domain Name System — a distributed, hierarchical database that translates human-readable domain names to IP addresses.

**Why it matters:** DNS resolution adds latency to every new connection; DNS misconfiguration is a common cause of service disruptions; TTL and caching behavior affects how quickly changes propagate.

**Key concepts to understand:**
- Recursive resolver vs authoritative nameserver vs root nameserver
- DNS resolution chain: client → recursive resolver → root → TLD → authoritative
- Record types: A (IPv4), AAAA (IPv6), CNAME (alias), MX (mail), TXT (verification), SRV (service discovery)
- TTL: time-to-live controls how long DNS responses are cached — reduce before a migration, raise after
- DNS negative caching: NXDOMAIN responses are also cached (SOA minimum TTL)
- DNS-based load balancing: round-robin DNS, geo-routing, health-check-based routing (Route53, Azure Traffic Manager)

**Resources to explore:**
- [ ] (to be filled)

---

### TLS/SSL

**What it is:** Transport Layer Security — a cryptographic protocol providing authentication, integrity, and confidentiality for network communications, running between transport (TCP/UDP) and application (HTTP) layers.

**Why it matters:** TLS is mandatory for all production services; understanding its handshake, certificate chain, and configuration options enables correct HTTPS and mTLS setup.

**Key concepts to understand:**
- TLS 1.3 handshake: 1-RTT (compared to TLS 1.2's 2-RTT) — resumed sessions with 0-RTT
- Certificate chain: leaf certificate → intermediate CA → root CA — each signed by the next
- Certificate validation: hostname matching, expiry, revocation (CRL, OCSP), chain trust
- SNI (Server Name Indication): allows multiple TLS certificates on one IP address
- mTLS (mutual TLS): both client and server authenticate with certificates — used in service meshes
- ALPN (Application-Layer Protocol Negotiation): negotiate HTTP/2 vs HTTP/1.1 during TLS handshake

**Resources to explore:**
- [ ] (to be filled)

---

### HTTP/1.1

**What it is:** The first widely adopted version of HTTP, using a text-based request/response protocol over TCP with keep-alive connections for request reuse.

**Why it matters:** HTTP/1.1 is still the protocol most developers understand implicitly — knowing its limitations (head-of-line blocking, header overhead) motivates HTTP/2 and HTTP/3.

**Key concepts to understand:**
- Request/response structure: method, URL, headers, body
- Persistent connections: `Connection: keep-alive` allows multiple requests on one TCP connection
- HTTP pipelining: send multiple requests without waiting for responses — widely disabled due to HOL blocking
- Head-of-line blocking at HTTP layer: a slow response blocks subsequent responses on the same connection
- Chunked transfer encoding: stream response body without knowing content-length in advance
- Status codes: 1xx informational, 2xx success, 3xx redirect, 4xx client error, 5xx server error

**Resources to explore:**
- [ ] (to be filled)

---

### HTTP/2

**What it is:** A major revision of HTTP using binary framing, multiplexing, header compression (HPACK), and server push — all over a single TCP connection.

**Why it matters:** HTTP/2 multiplexing eliminates HTTP/1.1's head-of-line blocking at the HTTP layer and HPACK reduces repetitive header overhead — significant improvement for APIs with many small requests.

**Key concepts to understand:**
- Binary framing: requests/responses split into frames (HEADERS, DATA) multiplexed over streams
- Multiplexing: multiple concurrent request/response streams on one TCP connection — no 6-connection parallelism hack needed
- HPACK header compression: static table + dynamic table for header deduplication across requests
- Stream priority: HTTP/2 allows priority hints (mostly not used by browsers in practice)
- Server push: server proactively sends resources before client requests them (largely deprecated)
- HTTP/2 over TLS only in practice: all major browsers require TLS for HTTP/2

**Resources to explore:**
- [ ] (to be filled)

---

### HTTP/3

**What it is:** HTTP over QUIC — a UDP-based transport that implements reliable streams without TCP's head-of-line blocking, enabling faster connection establishment and better performance on unreliable networks.

**Why it matters:** HTTP/3 eliminates TCP-level head-of-line blocking (HTTP/2's remaining issue) and provides 0-RTT connection resumption — dramatically improves performance on mobile and lossy networks.

**Key concepts to understand:**
- QUIC: UDP-based, multiplexed, reliable transport — each stream is independently reliable
- Connection migration: QUIC connections survive IP changes (cellular handoff) via connection IDs
- 0-RTT connection resumption: send data with the first packet using cached session state
- HTTP/3 over QUIC: QPACK (HTTP/3's header compression, replaces HPACK with ordered delivery awareness)
- Deployment challenges: UDP is blocked by some firewalls, QUIC has higher CPU cost than TCP
- ASP.NET Core HTTP/3 support: `builder.WebHost.UseQuic()` in .NET 7+

**Resources to explore:**
- [ ] (to be filled)

---

### QUIC

**What it is:** A UDP-based multiplexed transport protocol originally developed by Google, now standardized as IETF RFC 9000 — combining TLS 1.3, stream multiplexing, and congestion control in one protocol.

**Why it matters:** QUIC is the transport layer of the modern web (HTTP/3) and is being adopted for non-HTTP use cases — understanding it is increasingly important for network-level performance engineering.

**Key concepts to understand:**
- QUIC streams: independently reliable — a lost packet in one stream doesn't block others
- Integrated TLS: QUIC includes TLS 1.3 in the handshake — eliminates separate TLS negotiation round trip
- Connection ID: enables connection migration across IP changes
- QUIC congestion control: pluggable (CUBIC, BBR) — same as TCP but implemented in userspace
- MsQuic: Microsoft's open-source QUIC implementation used in ASP.NET Core and SMB
- QUIC vs TCP+TLS: QUIC reduces handshake from 2-3 RTT to 1 RTT (0 RTT for resumption)

**Resources to explore:**
- [ ] (to be filled)

---

### WebSockets

**What it is:** A full-duplex communication protocol over a persistent TCP connection — established via an HTTP upgrade and allowing both client and server to send messages at any time.

**Why it matters:** WebSockets enable real-time, bidirectional communication for chat, live dashboards, collaboration tools, and gaming without the overhead of polling or long-polling.

**Key concepts to understand:**
- Upgrade handshake: HTTP `Upgrade: websocket` with `Sec-WebSocket-Key` negotiation
- Frame format: opcode (text, binary, ping, pong, close), masking (client→server must be masked), payload
- Application-level heartbeat: ping/pong frames to detect dead connections
- WebSocket scaling: connections are stateful — requires sticky sessions or a backplane for multi-node
- SignalR over WebSockets: the recommended way to use WebSockets in .NET
- WebSocket vs SSE: WebSocket is bidirectional; SSE is server-to-client only, over HTTP, simpler

**Resources to explore:**
- [ ] (to be filled)

---

### Reverse Proxies

**What it is:** A server that sits in front of backend services, accepting client requests and forwarding them — used for load balancing, SSL termination, caching, compression, and request routing.

**Why it matters:** Every production web service has a reverse proxy (Nginx, HAProxy, Azure Application Gateway) — understanding how they work is essential for correct configuration and debugging.

**Key concepts to understand:**
- Nginx: event-driven, single-threaded, highly concurrent — the standard reverse proxy
- HAProxy: high-performance load balancer with ACL-based routing and health checking
- YARP (Yet Another Reverse Proxy): .NET-native reverse proxy library for custom gateway scenarios
- Pass-through headers: `X-Forwarded-For`, `X-Forwarded-Proto` for preserving client info behind proxy
- Connection multiplexing: proxy maintains persistent connections to backends, reuses them across many client connections
- Health checks: proxy removes unhealthy backends from rotation — active (HTTP probe) vs passive (failure detection)

**Resources to explore:**
- [ ] (to be filled)

---

### Load Balancers

**What it is:** Infrastructure that distributes incoming network traffic across multiple backend instances to improve availability and scalability.

**Why it matters:** Load balancers are the availability and scalability primitive — understanding their algorithms, health checking, and session affinity prevents misdirected traffic and uneven load distribution.

**Key concepts to understand:**
- Algorithms: round-robin, weighted round-robin, least connections, IP hash, consistent hashing
- Layer 4 (TCP) vs Layer 7 (HTTP) load balancing: L7 can route based on URL, headers, cookies
- Session affinity (sticky sessions): route user sessions to the same backend — complicates scaling
- Health check types: TCP connect, HTTP GET, custom health endpoint — remove unhealthy nodes fast
- Slow start: gradually ramp up traffic to new backends to avoid overwhelming them
- Azure Load Balancer (L4) vs Application Gateway (L7 WAF) vs Traffic Manager (DNS-based)

**Resources to explore:**
- [ ] (to be filled)

---

### CDN Architecture

**What it is:** A Content Delivery Network — a geographically distributed network of edge servers that cache and serve content close to users, reducing origin server load and user latency.

**Why it matters:** CDNs reduce latency for static assets and cacheable API responses by orders of magnitude — understanding cache control, edge logic, and invalidation is essential for global applications.

**Key concepts to understand:**
- Edge PoP (Point of Presence): CDN servers deployed globally — traffic served from nearest PoP
- Origin pull: on cache miss, edge fetches from origin and caches for subsequent requests
- Cache control headers: `Cache-Control: max-age=3600, stale-while-revalidate=60`
- CDN cache invalidation: purge by URL, by tag, or by path pattern — propagation takes time
- Edge compute: run code at CDN edge (Cloudflare Workers, Azure CDN Rules) for personalization and A/B testing
- Tiered caching: mid-tier cache between edge and origin to reduce origin traffic for cold edges

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [API Engineering](./02-api-engineering.md)
- [ASP.NET Core Internals](../Phase-02-Deep-DotNet-Internals/03-aspnet-core-internals.md)
- [Azure Core](../Phase-06-Cloud-And-Azure/01-azure-core.md)
