# 02 — Memory Allocation

> **Core idea:** A running .NET program uses three kinds of memory: the **stack** (per thread, freed automatically on return), the **managed heap** (freed by the GC), and **unmanaged memory** (freed by you). Knowing which one a value lands in tells you what it costs.

Demo: [02_Memory_Allocation.cs](./02_Memory_Allocation.cs)

---

## The three places

| | Stack | Managed heap | Unmanaged memory |
|---|---|---|---|
| **Scope** | One per thread | One per process, shared | Anywhere (OS, native libraries) |
| **Holds** | Method frames: locals, arguments, return addresses | Objects, arrays, strings, boxed values | File handles, native buffers, GDI objects... |
| **Allocated by** | Method entry (moving a pointer) | `new` | `Marshal.AllocHGlobal`, native APIs |
| **Freed by** | Method return (moving the pointer back) | **Garbage collector** ([03](./03_Garbage_Collection.md)) | **You**: `Dispose`, `SafeHandle`, `Free` |
| **Size limit** | Small and fixed per thread (OS-dependent). Overflow kills the process and cannot be caught | Grows as needed | Whatever the OS allows |

The stack needs no collector: returning from a method reclaims the whole frame at once. The heap needs one because object lifetimes are not tied to method calls.

---

## "Value types live on the stack" is a myth

The accurate rule: **a value lives wherever the thing containing it lives.**

| You write | Where the bytes are |
|---|---|
| `int x = 5;` as a local variable | In the stack frame |
| `var p = new PointClass();` | Object on the **heap**. The reference `p` is in the stack frame |
| A `struct` field inside a class | Inside the **heap** object, next to its other fields |
| `new PointStruct[1000]` | One heap array with all 1000 structs inline (4,024 bytes: 24 overhead + 1000 × 4) |
| `object o = 42;` (boxing) | A new heap object that holds a copy of 42 |
| A local captured by a lambda | Moved into a heap **closure object** |
| `stackalloc int[256]` | Stack |

### Copy semantics (demo 1)

```csharp
var s2 = s1;  s2.X = 99;   // struct: s1.X is still 1, s2 was a copy
var c2 = c1;  c2.X = 99;   // class : c1.X is now 99, both names point at one object
```

Pick `struct` for small, immutable, value-like data. Pick `class` when identity or sharing matters.

---

## What one object costs

Measured with `GC.GetAllocatedBytesForCurrentThread()` on a 64-bit process (demo 2):

| Allocation | Heap bytes |
|---|---|
| `new object()` | 24 |
| class with one `int` field | 24 |
| boxed `int` | 24 |
| `new byte[0]` | 24 |
| `new byte[100]` | 128 |
| `new int[10]` | 64 |
| `new string('x', 10)` | 48 |

What the numbers say:

- Every heap object carries about 16 bytes of bookkeeping (header plus type pointer) and the minimum object is **24 bytes**. A `class` with a single `int` costs the same as `new object()`.
- An array adds a length field: `byte[0]` is already 24 bytes.
- Sizes are rounded up to a multiple of 8: `24 + 100 = 124` became **128**.

Millions of tiny objects therefore cost far more memory than their payload suggests.

---

## `new` does not always mean heap (escape analysis)

Modern JITs can place an object on the **stack** when they can prove it never escapes the method. Demo 6 on .NET 10.0.12:

| Case | Heap bytes |
|---|---|
| `new PointClass()` used only inside the method | **0** |
| `new PointClass()` stored in a static field | 24 |

The same demo built in **Debug** printed 24 for the first case, because Debug turns the optimisation off. So `new` is a request, not a promise. Two consequences:

1. **Benchmark carefully.** A naive allocation test can report 0 bytes because the object never escaped. The demos store results in a static `_sink` field so they measure real allocations.
2. This is **runtime-version dependent** and only happens in optimised code. Do not write code that relies on it. Write clear code and measure.

---

## Boxing: a hidden allocation

Boxing copies a value type into a new heap object so it can be used as `object` (or as an interface). Demo 3, filling a collection with 1000 ints:

| Collection | Heap bytes |
|---|---|
| `ArrayList` (stores `object`, boxes every int) | **32,056** |
| `List<int>` (ints stored inline in the backing array) | **4,056** |

About 8× more memory, and 1000 extra objects for the GC to track.

**Where boxing hides:**

- Non-generic collections (`ArrayList`, `Hashtable`)
- Passing a struct to a parameter typed `object`
- Casting a struct to an interface without a generic constraint

Prefer generics (`List<int>`, `Dictionary<K,V>`) so value types are stored without boxing.

---

## Strings are immutable

Every `+=` creates a whole new string and abandons the old one. Building a 1000-character string (demo 4):

| Approach | Heap bytes |
|---|---|
| `s += "x"` in a loop | **1,025,976** |
| `StringBuilder` | 4,096 |

About 1 MB of garbage to build a 2 KB string. The cost grows with the square of the length. Use `StringBuilder` (or `string.Join`, `string.Create`) for loops. A handful of `+` operations outside a loop is fine.

---

## `stackalloc` and `Span<T>`: scratch memory without the GC

Demo 5, a 256-int scratch buffer:

| Approach | Heap bytes |
|---|---|
| `Span<int> b = stackalloc int[256]` | **0** |
| `new int[256]` | 1,048 (24 + 1024) |

Good for small, short-lived buffers (parsing, formatting). Keep stack buffers small (hundreds of bytes to a few KB), since the stack is limited. For larger or longer-lived buffers use `ArrayPool<T>.Shared`.

---

## ❌ / ✅ Quick guide

| ❌ Costly | ✅ Better |
|---|---|
| `ArrayList`, `object` parameters for numbers | `List<int>`, generics |
| `s += x` inside a loop | `StringBuilder` |
| Allocating a new small buffer on every call | `stackalloc` with `Span<T>`, or `ArrayPool<T>` |
| A `class` for a tiny, short-lived value | A small immutable `struct` or `readonly record struct` |
| Guessing | Measure: `GC.GetAllocatedBytesForCurrentThread`, BenchmarkDotNet `[MemoryDiagnoser]`, `dotnet-counters` |

---

## Self-check

1. A `struct` has an `int` field and sits inside a class. Stack or heap?
2. Why does `new object()` report 24 bytes when it holds no data?
3. Why can an allocation test report 0 bytes, and how do you make it honest?
4. Name three places boxing sneaks in.
5. Who frees the stack, the managed heap and a native file handle?
