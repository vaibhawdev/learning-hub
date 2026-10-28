# Algorithms

> **Phase:** 1 — Core CS Foundations  
> **Status:** Not Started

## Overview

Algorithms are the problem-solving patterns that transform data structures into solutions. This section covers not just how algorithms work but how to analyze and compare them — skills directly applicable to performance engineering, system design, and technical interviews at senior/staff levels.

## Topics

### Sorting Algorithms

**What it is:** Algorithms that rearrange a sequence of elements into a defined order, ranging from simple O(n²) sorts to optimal O(n log n) comparison-based sorts.

**Why it matters:** Sorting is a building block for searching, deduplication, and data processing pipelines; understanding tradeoffs guides choices in high-performance contexts.

**Key concepts to understand:**
- QuickSort: in-place, cache-friendly, O(n log n) average, O(n²) worst — pivot selection matters
- MergeSort: stable, O(n log n) guaranteed, O(n) extra space — preferred for linked lists and external sort
- HeapSort: in-place, O(n log n) guaranteed, poor cache performance
- TimSort: hybrid merge/insertion sort used in .NET Array.Sort and Python — optimized for real-world partially sorted data
- Counting/Radix/Bucket sort: O(n) non-comparison sorts for integer or bounded-alphabet data
- Stability: whether equal elements maintain relative order — critical for multi-key sorts

**Resources to explore:**
- [ ] (to be filled)

---

### Searching Algorithms

**What it is:** Algorithms to locate an element within a data structure, ranging from O(n) linear scan to O(log n) binary search to O(1) hash lookup.

**Why it matters:** Choosing the right search strategy — based on data size, structure, and query frequency — directly impacts application latency.

**Key concepts to understand:**
- Linear search: O(n), no preconditions, works on unsorted data
- Sentinel search: eliminates bounds check per iteration for minor speedup
- Interpolation search: O(log log n) for uniformly distributed sorted data
- Exponential search: useful when the array size is unknown
- When hash-based lookup is preferable over binary search

**Resources to explore:**
- [ ] (to be filled)

---

### Binary Search

**What it is:** An algorithm that finds a target in a sorted array by repeatedly halving the search space, achieving O(log n) time.

**Why it matters:** Binary search extends far beyond arrays — it's used to search answer spaces, find insertion points, and solve optimization problems where "is X feasible?" can be answered efficiently.

**Key concepts to understand:**
- Loop invariant formulation: what the [lo, hi] range represents at each step
- Off-by-one errors: the most common binary search bug — lower vs upper bound variants
- Finding first/last occurrence of a duplicate value
- Binary search on the answer: finding minimum k such that f(k) is true
- `Array.BinarySearch` in .NET and its return value contract (bitwise complement for not found)
- Application in database index range scans

**Resources to explore:**
- [ ] (to be filled)

---

### Dynamic Programming

**What it is:** An optimization technique that solves problems by breaking them into overlapping subproblems, storing results to avoid redundant computation (memoization or tabulation).

**Why it matters:** DP is the key to solving a broad class of optimization and counting problems that appear in text processing, resource allocation, and graph problems in real systems.

**Key concepts to understand:**
- Optimal substructure: a problem's optimal solution is composed of optimal sub-solutions
- Overlapping subproblems: distinct from divide-and-conquer where subproblems don't repeat
- Top-down (memoization) vs bottom-up (tabulation): tradeoffs in clarity vs stack usage
- State space design: identifying the minimum state needed to characterize a subproblem
- Classic problems: Knapsack, Longest Common Subsequence, Edit Distance, Matrix Chain Multiplication
- Space optimization: rolling array technique to reduce O(n²) space to O(n)

**Resources to explore:**
- [ ] (to be filled)

---

### Greedy Algorithms

**What it is:** Algorithms that build a solution by always making the locally optimal choice at each step, without reconsidering past choices.

**Why it matters:** Many scheduling, networking, and optimization problems have greedy solutions that are provably optimal — recognizing when greedy works saves the complexity of DP.

**Key concepts to understand:**
- Greedy choice property: a globally optimal solution can be assembled from locally optimal choices
- Proving greedy correctness: exchange argument — any other choice leads to equal or worse result
- Classic problems: Activity Selection, Huffman Coding, Dijkstra's (greedy relaxation), Prim's MST
- When greedy fails: coin change with arbitrary denominations, TSP
- Greedy approximation algorithms for NP-hard problems

**Resources to explore:**
- [ ] (to be filled)

---

### Backtracking

**What it is:** A recursive algorithm that builds solutions incrementally and abandons a partial solution ("backtracks") as soon as it determines it cannot lead to a valid or optimal solution.

**Why it matters:** Backtracking solves constraint satisfaction problems (sudoku, n-queens, regex matching) and is the basis for many search-based optimization algorithms.

**Key concepts to understand:**
- Pruning: eliminating branches early based on constraints — the key to backtracking efficiency
- State space tree: visualizing the search as a tree of choices
- Classic problems: N-Queens, Sudoku Solver, Subset Sum, Permutations/Combinations
- Backtracking vs BFS/DFS: backtracking is a form of DFS with pruning
- Branch and bound: backtracking with bound functions for optimization problems

**Resources to explore:**
- [ ] (to be filled)

---

### Graph Traversal

**What it is:** Algorithms for systematically visiting every vertex in a graph — Breadth-First Search (BFS) explores by level, Depth-First Search (DFS) explores by depth.

**Why it matters:** Graph traversal is the foundation for shortest paths, connectivity analysis, cycle detection, topological sorting, and many distributed systems algorithms.

**Key concepts to understand:**
- BFS: queue-based, O(V+E), finds shortest path in unweighted graphs, level-order traversal
- DFS: stack/recursion-based, O(V+E), used for cycle detection, SCC, topological sort
- Discovery and finish times in DFS and their meaning (back edges, cross edges, tree edges)
- Connected components in undirected graphs via BFS/DFS
- Bipartite checking, cycle detection using visited-states (white/gray/black)
- Iterative DFS to avoid stack overflow on large graphs

**Resources to explore:**
- [ ] (to be filled)

---

### Dijkstra's Algorithm

**What it is:** A greedy shortest-path algorithm for graphs with non-negative edge weights, using a priority queue to always extend the shortest known path.

**Why it matters:** Dijkstra underpins GPS routing, network routing protocols (OSPF), and game AI pathfinding — understanding it is essential for any network or infrastructure engineering role.

**Key concepts to understand:**
- Relaxation: updating a node's distance if a shorter path through a neighbor is found
- Priority queue implementation: binary heap gives O((V+E) log V), Fibonacci heap gives O(E + V log V)
- Lazy deletion variant: insert duplicate entries instead of decrease-key
- Why negative edges break Dijkstra (use Bellman-Ford instead)
- Bidirectional Dijkstra for large graphs: meets-in-the-middle optimization
- A* as a heuristic-guided generalization of Dijkstra

**Resources to explore:**
- [ ] (to be filled)

---

### A* (A-Star)

**What it is:** A best-first search algorithm that uses a heuristic h(n) to estimate distance to the goal, combining actual cost g(n) and heuristic f(n) = g(n) + h(n) to guide search.

**Why it matters:** A* is the industry standard for pathfinding in maps, games, and robotics because it finds optimal paths faster than Dijkstra when a good heuristic is available.

**Key concepts to understand:**
- Admissibility: h(n) must never overestimate actual cost — guarantees optimality
- Consistency (monotonicity): h(n) ≤ cost(n, n') + h(n') — ensures nodes aren't re-opened
- Common heuristics: Manhattan distance (grid), Euclidean distance, Haversine (geo)
- Weighted A*: multiply heuristic by w > 1 for faster but suboptimal paths
- Memory-bounded A*: IDA* (iterative deepening) trades time for memory
- JPS (Jump Point Search): A* optimization for uniform-cost grids

**Resources to explore:**
- [ ] (to be filled)

---

### Topological Sorting

**What it is:** A linear ordering of vertices in a DAG (Directed Acyclic Graph) such that for every edge u→v, u appears before v — meaningful only when no cycles exist.

**Why it matters:** Topological sort is used in build systems (Makefile dependencies), package managers (npm install order), task scheduling, and database query planners.

**Key concepts to understand:**
- Kahn's algorithm: BFS-based, uses in-degree tracking, detects cycles if not all nodes processed
- DFS-based algorithm: nodes added to result stack on finish time
- Why cycles make topological sort impossible (detect using DFS gray nodes)
- Application in CI/CD pipeline stage ordering
- Critical path analysis: longest path in a DAG for project scheduling

**Resources to explore:**
- [ ] (to be filled)

---

### String Matching Algorithms

**What it is:** Algorithms for finding occurrences of a pattern string within a text string, ranging from naive O(nm) to O(n+m) via preprocessing.

**Why it matters:** String matching appears in log search, URL routing, full-text search, DNA sequencing, and network intrusion detection — efficient algorithms are critical at scale.

**Key concepts to understand:**
- Naive algorithm: O(nm) with early termination opportunities
- KMP (Knuth-Morris-Pratt): O(n+m) using failure function to skip redundant comparisons
- Boyer-Moore: O(n/m) best case using bad-character and good-suffix heuristics — practical fastest for large alphabets
- Rabin-Karp: rolling hash for O(n+m) average, useful for multi-pattern search
- Aho-Corasick: trie + failure links for simultaneous multi-pattern matching in O(n+z)
- Suffix arrays and suffix trees for advanced pattern matching and substring problems

**Resources to explore:**
- [ ] (to be filled)

---

### Time Complexity

**What it is:** A measure of how the running time of an algorithm scales with input size n, expressed using Big-O notation to describe upper bounds.

**Why it matters:** Time complexity analysis lets you predict algorithm behavior at scale before running code — essential for making informed design decisions.

**Key concepts to understand:**
- Big-O, Big-Ω, Big-Θ: upper bound, lower bound, tight bound — and when each is appropriate
- Common complexities: O(1), O(log n), O(n), O(n log n), O(n²), O(2ⁿ), O(n!)
- Practical constants: O(n log n) with a large constant can be slower than O(n²) for small n
- Worst case vs average case vs expected case
- Multivariate complexity: algorithms on graphs are O(V+E), not just O(n)
- P vs NP: the distinction between polynomial and exponential classes

**Resources to explore:**
- [ ] (to be filled)

---

### Space Complexity

**What it is:** A measure of the amount of memory an algorithm uses relative to input size, including input storage, auxiliary space, and call stack depth.

**Why it matters:** Memory is often the binding constraint in production systems — choosing an in-place algorithm over one requiring O(n) auxiliary space can be the difference between a feasible and infeasible solution.

**Key concepts to understand:**
- Input space vs auxiliary space: some analyses include input, others only auxiliary
- In-place algorithms: O(1) or O(log n) auxiliary space (e.g., quicksort, heapsort)
- Recursion stack depth: DFS on a tree uses O(h) stack space where h is height
- Space-time tradeoffs: memoization trades space for time
- Streaming algorithms: process data in O(1) or O(log n) space without storing input
- Memory hierarchy awareness: cache lines, TLB misses, and their effect on practical performance

**Resources to explore:**
- [ ] (to be filled)

---

### Amortized Complexity

**What it is:** A method of analyzing algorithm cost that averages the worst-case cost of a sequence of operations, showing that occasional expensive operations are subsidized by many cheap ones.

**Why it matters:** Amortized analysis correctly explains why dynamic arrays, splay trees, and union-find have better practical performance than worst-case analysis suggests.

**Key concepts to understand:**
- Aggregate method: total cost / number of operations
- Accounting method: assign credits to cheap operations to pay for future expensive ones
- Potential method: define a potential function that captures "stored work"
- Dynamic array resizing: amortized O(1) append even though occasional resize is O(n)
- Union-Find with path compression + union by rank: amortized O(α(n)) per operation
- Splay trees: amortized O(log n) per operation despite O(n) worst case

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Programming Fundamentals](./01-programming-fundamentals.md)
- [Data Structures](./02-data-structures.md)
- [Advanced Systems](../Phase-15-Advanced-Topics/01-advanced-systems.md)
- [Application Performance](../Phase-08-Performance-Engineering/01-application-performance.md)
