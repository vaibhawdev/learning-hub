# Phase 4 — Distributed Systems

> **Status:** Not Started

## Overview

Distributed systems are the foundation of all modern backend infrastructure. This phase covers the theory and practice of building systems that span multiple machines — from the fundamental theorems that constrain what is possible, to messaging systems and architectural patterns for building reliable services at scale.

## Sections

1. [Core Distributed Systems](./01-core-distributed-systems.md)  
   CAP theorem, consistency models, consensus (Raft, Paxos), idempotency, retries, circuit breakers, sagas, CQRS, and event sourcing.

2. [Messaging Systems](./02-messaging-systems.md)  
   Kafka internals, RabbitMQ, Azure Service Bus, ordering, outbox pattern, dead-letter queues, and event versioning.

3. [High-Scale Architecture](./03-high-scale-architecture.md)  
   Monolith vs microservices, modular monoliths, event-driven architecture, DDD, vertical slices, API gateways, BFF, and service mesh.

## Learning Objectives

By the end of this phase, you should be able to:

- Explain the CAP theorem and categorize common databases by their CAP position
- Describe how Raft leader election and log replication work
- Design an outbox pattern implementation for a .NET service using EF Core
- Identify the appropriate saga pattern (choreography vs orchestration) for a given workflow
- Choose between monolith, modular monolith, and microservices based on team size and domain complexity

## Suggested Order

Core distributed systems theory (1) first, then messaging systems (2) as the practical implementation of async patterns, then high-scale architecture (3) which synthesizes both.
