# Phase 2 — Deep .NET Internals

> **Status:** Not Started

## Overview

This phase goes beneath the surface of everyday .NET development to understand the runtime machinery that powers every C# application. Mastery here enables you to write genuinely high-performance code, diagnose deep issues in production, and make architectural decisions grounded in the actual cost of runtime features.

## Sections

1. [CLR Runtime Internals](./01-clr-runtime-internals.md)  
   GC, JIT, AOT, tiered compilation, Span/Memory, SIMD, unsafe code, and assembly loading.

2. [Async & Concurrency](./02-async-concurrency.md)  
   async/await state machines, thread pool internals, channels, pipelines, ValueTask, and lock-free programming.

3. [ASP.NET Core Internals](./03-aspnet-core-internals.md)  
   Middleware pipeline, endpoint routing, DI internals, Kestrel, gRPC, SignalR, and Native AOT APIs.

## Learning Objectives

By the end of this phase, you should be able to:

- Explain what the GC does during a Gen2 collection and how to minimize promotion to Gen2
- Describe the state machine the compiler generates for an `async` method
- Diagnose thread pool starvation and explain how to prevent it
- Configure Kestrel for high-throughput scenarios
- Use `Span<T>`, `Memory<T>`, and `ArrayPool<T>` to write zero-allocation data processing code

## Suggested Order

Start with CLR internals (1) as the foundation, then async/concurrency (2) which builds on thread and memory models, then ASP.NET Core internals (3) which applies both.
