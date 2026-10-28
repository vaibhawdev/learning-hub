# Phase 3 — Database Engineering

> **Status:** Not Started

## Overview

Databases are the most critical stateful components in any system — their behavior under load, failure, and schema change determines whether a system is reliable. This phase covers SQL internals, EF Core mastery, and NoSQL/caching patterns needed for staff-level backend work.

## Sections

1. [SQL Deep Dive](./01-sql-deep-dive.md)  
   Query plans, index internals, transactions, isolation levels, MVCC, partitioning, and sharding.

2. [Entity Framework Core](./02-entity-framework-core.md)  
   Change tracking, NoTracking queries, compiled queries, N+1 issues, bulk operations, and interceptors.

3. [NoSQL and Caching](./03-nosql-and-caching.md)  
   Redis internals, caching patterns, cache invalidation, Cosmos DB, MongoDB, and DynamoDB.

4. [Data Partitioning & Sharding — Deep Dive](./04-data-partitioning-and-sharding.md)  
   Partitioning vs. sharding vs. replication, partitioning strategies, shard key selection, consistent hashing and other routing algorithms, resharding, and a decision guide with gotchas.

## Learning Objectives

By the end of this phase, you should be able to:

- Read and interpret a SQL execution plan and identify the cause of a slow query
- Design indexes that eliminate key lookups for critical queries
- Identify and fix N+1 query problems in EF Core
- Choose between isolation levels with an understanding of the consistency/concurrency tradeoff
- Design a partition key strategy for Cosmos DB or DynamoDB given access patterns
- Choose a sharding strategy (range, hash, or composite) and shard key for a given access pattern, and explain the rebalancing/consistent-hashing tradeoffs involved

## Suggested Order

SQL deep dive (1) first to understand the database layer, then EF Core (2) as the .NET abstraction over SQL, then NoSQL/caching (3) for alternative storage patterns, then partitioning & sharding (4) to tie the scalability concepts from all three together in depth.
