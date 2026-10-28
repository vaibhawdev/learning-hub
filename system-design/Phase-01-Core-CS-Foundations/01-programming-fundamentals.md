# Programming Fundamentals

> **Phase:** 1 — Core CS Foundations  
> **Status:** Not Started

## Overview

This section covers the foundational programming concepts that underpin all modern software engineering. Understanding these deeply — not just syntactically but mechanically — is what separates engineers who write code from engineers who design systems. These concepts are language-agnostic but we will often reference .NET/C# for concrete examples.

## Topics

### Memory Management

**What it is:** The process by which programs allocate, use, and release memory during execution, either manually (C/C++) or automatically via a garbage collector (.NET, JVM).

**Why it matters:** Understanding memory management helps you write code that avoids leaks, reduces GC pressure, and runs efficiently under load.

**Key concepts to understand:**
- Stack vs heap allocation and their performance characteristics
- How garbage collectors decide what to collect (reachability, generations)
- Memory fragmentation and its effect on allocation speed
- Pinned memory and why it can degrade GC performance
- When to use `ArrayPool<T>`, `MemoryPool<T>`, or object pools

**Resources to explore:**
- [ ] (to be filled)

---

### Stack vs Heap

**What it is:** Two distinct regions of memory — the stack holds short-lived, fixed-size data (local variables, return addresses) while the heap holds dynamically allocated, variable-lifetime objects.

**Why it matters:** Knowing which data goes where affects performance: stack allocation is essentially free (just a pointer increment), while heap allocation involves allocator bookkeeping and eventual GC work.

**Key concepts to understand:**
- Stack frames and how method calls push/pop them
- Why stack overflow happens (unbounded recursion, large local arrays)
- The managed heap in .NET: small object heap (SOH) vs large object heap (LOH)
- How value types (structs) can live on either stack or heap depending on context
- Boxing: when a value type is promoted to the heap and why it's costly

**Resources to explore:**
- [ ] (to be filled)

---

### Value Types vs Reference Types

**What it is:** Value types (int, struct, bool) hold data directly and are copied on assignment; reference types (class, object, string) store a reference (pointer) to heap-allocated data.

**Why it matters:** Misunderstanding this leads to subtle bugs (unexpected aliasing) and performance issues (excessive boxing or large struct copies).

**Key concepts to understand:**
- Copy semantics for value types and reference semantics for reference types
- Why mutating a struct in a list requires re-assignment
- `readonly struct` and how it prevents defensive copies
- `in` parameters and when they help vs hurt performance
- Nullable value types (`int?`) and their boxing implications

**Resources to explore:**
- [ ] (to be filled)

---

### Immutability

**What it is:** A design principle where objects, once created, cannot be modified — any "change" produces a new object.

**Why it matters:** Immutable data is inherently thread-safe, easier to reason about, and allows sharing without defensive copying — critical in concurrent and distributed systems.

**Key concepts to understand:**
- `readonly` fields vs `init`-only properties in C#
- Immutable collections (ImmutableList, ImmutableDictionary) and their persistent data structure internals
- Structural sharing as a performance technique for immutable collections
- Why strings in .NET are immutable and the implications for concatenation
- Record types in C# and value-based equality

**Resources to explore:**
- [ ] (to be filled)

---

### Recursion

**What it is:** A programming technique where a function calls itself to solve a smaller instance of the same problem, with a base case to terminate.

**Why it matters:** Recursion is the natural solution to tree traversal, divide-and-conquer algorithms, and many parsing problems; understanding it is required for understanding how the call stack works.

**Key concepts to understand:**
- Base case and recursive case structure
- How each recursive call creates a new stack frame
- Stack overflow risk with deep recursion and how to convert to iteration
- Tail recursion and why most languages (including C#) don't optimize it
- Memoization as a bridge between recursion and dynamic programming

**Resources to explore:**
- [ ] (to be filled)

---

### Closures

**What it is:** A function that captures (closes over) variables from its enclosing scope, keeping them alive even after the enclosing function has returned.

**Why it matters:** Closures are the backbone of lambdas, LINQ, callbacks, and async state machines — misunderstanding them causes capture bugs and unexpected memory retention.

**Key concepts to understand:**
- How the C# compiler lifts captured variables to a heap-allocated display class
- The classic loop-capture bug and how `foreach` in C# 5+ avoids it
- Closures over `ref` parameters and why it's disallowed
- Memory implications: closures prevent captured objects from being GC'd
- Delegate caching: when lambdas are allocated and when they're cached as static fields

**Resources to explore:**
- [ ] (to be filled)

---

### Functional Programming Basics

**What it is:** A programming paradigm that treats computation as the evaluation of mathematical functions, emphasizing immutability, pure functions, and function composition over mutable state and imperative control flow.

**Why it matters:** Functional patterns make concurrent code safer and reasoning about data transformations easier; LINQ in C# is built on these principles.

**Key concepts to understand:**
- Pure functions: same input always produces same output, no side effects
- Higher-order functions: functions that take or return other functions
- Map, Filter, Reduce (Select, Where, Aggregate in LINQ)
- Function composition and pipelining
- Monadic patterns: Option/Maybe, Result/Either for explicit error handling
- Why immutability and pure functions enable parallelism

**Resources to explore:**
- [ ] (to be filled)

---

### Concurrency Basics

**What it is:** The ability of a program to have multiple computations in progress simultaneously, either interleaved on a single core or truly parallel on multiple cores.

**Why it matters:** Modern systems must handle many simultaneous requests; understanding concurrency is non-negotiable for writing correct, scalable server software.

**Key concepts to understand:**
- Concurrency vs parallelism (structuring work vs executing work simultaneously)
- Race conditions: when the outcome depends on the order of non-deterministic events
- Atomicity: operations that appear indivisible to other threads
- Visibility: when changes made by one thread become visible to others
- The memory model: CPU caches, reordering, and memory barriers
- Common primitives: locks (Monitor), Mutex, Semaphore, Interlocked operations

**Resources to explore:**
- [ ] (to be filled)

---

### Parallelism vs Async

**What it is:** Parallelism uses multiple threads/cores to do CPU-bound work simultaneously; async/await is a concurrency model for I/O-bound work that avoids blocking threads while waiting.

**Why it matters:** Using the wrong model is a common mistake: `Task.Run` for I/O wastes thread pool threads; blocking sync code in async contexts causes deadlocks.

**Key concepts to understand:**
- CPU-bound work: benefits from `Parallel.For`, PLINQ, `Task.Run`
- I/O-bound work: benefits from `async/await` without `Task.Run`
- Thread pool starvation: what happens when all threads are blocked on sync I/O
- `ConfigureAwait(false)` and when it's needed
- Why `async void` is dangerous outside of event handlers
- `ValueTask` for avoiding allocations on hot paths

**Resources to explore:**
- [ ] (to be filled)

---

### Serialization/Deserialization

**What it is:** The process of converting an in-memory object graph into a storable/transmittable byte sequence (serialization) and reconstructing it (deserialization).

**Why it matters:** Every API request, database write, and message queue interaction involves serialization — its performance and correctness characteristics directly affect system throughput and reliability.

**Key concepts to understand:**
- JSON (System.Text.Json vs Newtonsoft.Json: source generation, performance)
- Binary formats: Protobuf, MessagePack, CBOR — why they outperform JSON
- Schema evolution: adding/removing fields without breaking consumers
- `[JsonIgnore]`, `[JsonPropertyName]`, custom converters
- Circular reference handling and how to avoid object graph issues
- Deserialization vulnerabilities (mass assignment, type confusion)

**Resources to explore:**
- [ ] (to be filled)

---

### Binary Formats vs Text Formats

**What it is:** Text formats (JSON, XML, CSV) encode data as human-readable characters; binary formats (Protobuf, MessagePack, Avro) encode data as compact byte sequences.

**Why it matters:** At scale, the choice of serialization format significantly impacts CPU cost (parsing), network bandwidth, and storage size — Protobuf is typically 5-10x smaller and faster than JSON.

**Key concepts to understand:**
- Why JSON parsing is CPU-intensive (UTF-8 scanning, dynamic typing)
- Protobuf's wire format: field tags, varint encoding, length-delimited
- MessagePack: binary JSON with similar schema flexibility
- Avro and its schema-registry-based approach to schema evolution
- When human-readability justifies the overhead (debugging, public APIs)
- `Span<byte>` and zero-copy parsing patterns in .NET

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Data Structures](./02-data-structures.md)
- [Algorithms](./03-algorithms.md)
- [CLR Runtime Internals](../Phase-02-Deep-DotNet-Internals/01-clr-runtime-internals.md)
- [Async & Concurrency](../Phase-02-Deep-DotNet-Internals/02-async-concurrency.md)
