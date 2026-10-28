# Data Structures

> **Phase:** 1 — Core CS Foundations  
> **Status:** Not Started

## Overview

Data structures are the vocabulary of algorithm design. This section goes beyond memorizing APIs — it covers how each structure works internally, its performance characteristics, and when to reach for it in real system design. Every distributed systems concept (consistent hashing, bloom filters, LSM trees) builds on these foundations.

## Topics

### Arrays

**What it is:** A contiguous block of memory holding elements of the same type, providing O(1) random access by index.

**Why it matters:** Arrays are the most cache-friendly data structure; understanding them is the baseline for understanding all other structures.

**Key concepts to understand:**
- Cache lines (64 bytes) and why sequential access is dramatically faster than random access
- Array resizing in dynamic arrays (ArrayList, List<T>): amortized O(1) append
- Memory layout of arrays vs jagged arrays in .NET
- `ArraySegment<T>` and `Span<T>` for zero-copy slicing
- SIMD vectorization benefits on contiguous data

**Resources to explore:**
- [ ] (to be filled)

---

### Linked Lists

**What it is:** A sequence of nodes where each node holds a value and a pointer to the next (and optionally previous) node, providing O(1) insertion/deletion at a known position.

**Why it matters:** Linked lists model many real structures (LRU cache, undo/redo history); understanding pointer manipulation is prerequisite to understanding tree structures.

**Key concepts to understand:**
- Singly vs doubly linked lists: tradeoffs in pointer overhead vs traversal flexibility
- O(n) search: why linked lists lose to arrays for most workloads
- Cache unfriendliness: nodes are scattered in heap memory
- `LinkedList<T>` in .NET and when it's actually appropriate
- Skip lists: layered linked lists that achieve O(log n) search

**Resources to explore:**
- [ ] (to be filled)

---

### Hash Tables

**What it is:** A data structure that maps keys to values using a hash function to compute an array index, providing average O(1) get/set operations.

**Why it matters:** Hash tables are the most commonly used data structure in application code; understanding collision handling and load factors explains observed performance degradation.

**Key concepts to understand:**
- Hash functions: requirements (uniformity, determinism, avalanche effect)
- Collision resolution: chaining (linked list per bucket) vs open addressing (linear/quadratic probing)
- Load factor and rehashing: when and how the table grows
- `Dictionary<K,V>` internals in .NET: open addressing with chaining hybrid
- Hash collisions as a denial-of-service vector (HashDoS) and randomized hashing
- Consistent hashing for distributed key-value stores

**Resources to explore:**
- [ ] (to be filled)

---

### Trees

**What it is:** A hierarchical, acyclic data structure consisting of nodes connected by edges, with one root node and zero or more subtrees.

**Why it matters:** Trees model hierarchical data (file systems, XML/JSON, org charts) and underpin efficient search, storage, and indexing structures.

**Key concepts to understand:**
- Binary search tree (BST) invariant: left < root < right
- O(log n) average vs O(n) worst case for unbalanced BSTs
- Tree traversals: in-order, pre-order, post-order, level-order
- Why databases use B-Trees, not BSTs (disk page alignment, fan-out)
- Expression trees and how LINQ query providers use them

**Resources to explore:**
- [ ] (to be filled)

---

### AVL Trees

**What it is:** A self-balancing BST where the heights of the two child subtrees of any node differ by at most one, guaranteeing O(log n) operations.

**Why it matters:** AVL trees represent the core idea of balanced trees — understanding rotations here unlocks intuition for all self-balancing structures.

**Key concepts to understand:**
- Balance factor: height(right) - height(left), maintained in {-1, 0, 1}
- Four rotation cases: left-left, right-right, left-right, right-left
- Rebalancing on insert and delete: propagation up the tree
- AVL vs Red-Black: AVL is more rigidly balanced (faster lookups, slower writes)
- Real usage: AVL trees in some in-memory databases and language runtimes

**Resources to explore:**
- [ ] (to be filled)

---

### Red-Black Trees

**What it is:** A self-balancing BST with a coloring invariant (red/black nodes) that guarantees O(log n) operations with fewer rotations than AVL trees.

**Why it matters:** Red-Black trees are used in many standard library implementations (C++ STL map, Java TreeMap, Linux kernel's CFS scheduler) because of their balanced write performance.

**Key concepts to understand:**
- Five Red-Black invariants: root is black, no consecutive red nodes, equal black-depth, etc.
- Insert and delete fixup: rotation and recoloring cases
- Why Red-Black trees have at most 2x height difference between paths
- `SortedDictionary<K,V>` in .NET uses a Red-Black tree internally
- 2-3-4 trees as an isomorphic view of Red-Black trees

**Resources to explore:**
- [ ] (to be filled)

---

### Tries

**What it is:** A tree where each node represents a character, and paths from root to leaf spell out stored strings — enabling O(k) lookup (k = string length) independent of dictionary size.

**Why it matters:** Tries power autocomplete, spell checkers, IP routing tables, and any prefix-based search — they are significantly more efficient than hash maps for prefix queries.

**Key concepts to understand:**
- Compressed tries (Patricia/Radix tries): merging single-child chains
- Memory cost: naive tries use O(alphabet × nodes) space
- DAWG (Directed Acyclic Word Graph): suffix sharing for further compression
- Trie vs HashMap for string keys: trie wins on prefix queries, HashMap on exact lookups
- Applications: longest prefix match in IP routing (CIDR), DNS resolution

**Resources to explore:**
- [ ] (to be filled)

---

### Heaps / Priority Queues

**What it is:** A complete binary tree stored as an array where every parent is larger (max-heap) or smaller (min-heap) than its children, enabling O(1) peek and O(log n) insert/extract.

**Why it matters:** Priority queues are everywhere in systems: Dijkstra's algorithm, task schedulers, event simulation, timer wheels, and merge-k-sorted-lists all rely on efficient min-heap operations.

**Key concepts to understand:**
- Heap as an array: parent at i, children at 2i+1 and 2i+2 — no pointer overhead
- Heapify (sift-up, sift-down) and building a heap in O(n)
- `PriorityQueue<TElement, TPriority>` in .NET 6+
- d-ary heaps: higher fan-out reduces height, fewer comparisons for insert-heavy workloads
- Fibonacci heaps: amortized O(1) insert/decrease-key used in advanced graph algorithms

**Resources to explore:**
- [ ] (to be filled)

---

### Graphs

**What it is:** A set of vertices (nodes) connected by edges, which may be directed or undirected, weighted or unweighted — the most general data structure for modeling relationships.

**Why it matters:** Graphs model social networks, dependency graphs, network topology, state machines, and any relationship problem; most distributed systems topology problems reduce to graph problems.

**Key concepts to understand:**
- Adjacency matrix vs adjacency list: memory and access time tradeoffs
- Directed vs undirected, weighted vs unweighted
- Sparse vs dense graphs and their representation implications
- Strongly connected components (Tarjan's, Kosaraju's algorithms)
- Topological sort and its use in build systems and task scheduling
- Bipartite graphs and matching algorithms

**Resources to explore:**
- [ ] (to be filled)

---

### Bloom Filters

**What it is:** A probabilistic data structure using multiple hash functions and a bit array to test set membership in O(k) time, with no false negatives but a configurable false positive rate.

**Why it matters:** Bloom filters are used in databases (Cassandra, RocksDB) to avoid expensive disk lookups for non-existent keys, and in web caches to avoid caching one-hit wonders.

**Key concepts to understand:**
- False positive rate as a function of bit array size and number of hash functions
- Why deletions are not supported in standard Bloom filters (Counting Bloom filter as alternative)
- Optimal number of hash functions: k = (m/n) × ln(2)
- Space efficiency: a Bloom filter with 1% FPR uses ~10 bits per element vs ~64 bits for a hash set
- Scalable Bloom filters for unbounded data sets
- Cuckoo filters: support deletion, better cache performance

**Resources to explore:**
- [ ] (to be filled)

---

### Ring Buffers

**What it is:** A fixed-size circular array where head and tail pointers wrap around, enabling O(1) enqueue/dequeue without memory allocation — also called a circular buffer.

**Why it matters:** Ring buffers are the foundation of lock-free SPSC queues (LMAX Disruptor), kernel I/O buffers, audio/video streaming pipelines, and producer-consumer channels requiring bounded memory.

**Key concepts to understand:**
- Power-of-2 sizing to use bitwise AND for wrap-around instead of modulo
- Distinguishing full from empty: using a count vs sequence numbers
- LMAX Disruptor's ring buffer: cache-line padding to avoid false sharing
- `System.Threading.Channels` in .NET and its bounded channel implementation
- Lock-free SPSC (single-producer, single-consumer) variants

**Resources to explore:**
- [ ] (to be filled)

---

### LRU / LFU Cache Structures

**What it is:** LRU (Least Recently Used) evicts the item not accessed for the longest time; LFU (Least Frequently Used) evicts the item accessed fewest times — both implemented to achieve O(1) all operations.

**Why it matters:** Caching is one of the highest-leverage performance techniques; understanding cache eviction policies is essential for designing efficient caches in both application and infrastructure layers.

**Key concepts to understand:**
- LRU implementation: HashMap + doubly linked list for O(1) get, put, and eviction
- LFU implementation: HashMap + frequency buckets (min-heap or doubly linked list per frequency)
- Why pure LFU can be unfair to recently inserted items (LFU aging)
- TinyLFU (used in Caffeine, W-TinyLFU): frequency sketch + LRU window for admission
- Redis eviction policies: allkeys-lru, allkeys-lfu, volatile-lru, etc.
- `MemoryCache` in .NET: sliding vs absolute expiration

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Programming Fundamentals](./01-programming-fundamentals.md)
- [Algorithms](./03-algorithms.md)
- [Database Performance](../Phase-08-Performance-Engineering/02-database-performance.md)
- [Advanced Systems](../Phase-15-Advanced-Topics/01-advanced-systems.md)
