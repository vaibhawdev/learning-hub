# Phase 5 — Networking and Web

> **Status:** Not Started

## Overview

Everything in distributed systems travels over a network. This phase covers the protocols and API design patterns that govern how services and clients communicate — from TCP internals to HTTP/3 to REST and gRPC design.

## Sections

1. [Networking Fundamentals](./01-networking-fundamentals.md)  
   TCP/IP, UDP, DNS, TLS/SSL, HTTP/1.1-3, QUIC, WebSockets, reverse proxies, load balancers, and CDNs.

2. [API Engineering](./02-api-engineering.md)  
   REST maturity model, versioning, OpenAPI, GraphQL, gRPC, idempotency, pagination, rate limiting, and API gateways.

## Learning Objectives

By the end of this phase, you should be able to:

- Explain the differences between HTTP/1.1, HTTP/2, and HTTP/3 and when each is preferable
- Describe what happens during a TLS 1.3 handshake
- Design an API with proper versioning, idempotency keys, and cursor-based pagination
- Configure rate limiting for an API with per-user and per-tier policies
- Choose between REST and gRPC for inter-service communication with justification

## Suggested Order

Networking fundamentals (1) first to understand the underlying protocols, then API engineering (2) which layers application patterns on top.
