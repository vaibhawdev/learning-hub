# 03 — Garbage Collection

> **Core idea:** The GC does not free objects you "stopped using". It frees objects that are **unreachable** from any root. If something still references an object, even by accident, it lives.

Demo: [03_Garbage_Collection.cs](./03_Garbage_Collection.cs)

---

## What counts as reachable

The GC starts from **roots** and follows references. Everything it reaches is alive. Everything else is garbage.

| Root | Example |
|---|---|
| Stack locals and arguments (and CPU registers) | A variable in a running method |
| **Static fields** | `static List<Order> _cache` |
| GC handles | Pinned objects, `GCHandle`, interop |
| The finalization queue | Objects waiting for their finalizer to run |

Demo 5 shows reachability deciding lifetime:

| Situation | Still alive after `GC.Collect()`? |
|---|---|
| No references left | **No** |
| Held by a static list | **Yes** |
| Removed from the static list | **No** |

---

## Generations

Most objects die young, so the GC splits the heap by age and collects the young part often.

| Generation | Contains | Collected |
|---|---|---|
| **gen0** | Newly allocated objects | Very often, cheap |
| **gen1** | Survivors of one gen0 collection | Less often. A buffer between young and old |
| **gen2** | Long-lived objects, plus the Large Object Heap | Rarely, expensive |

Survivors are **promoted**. Demo 2 forces it:

```
fresh object                 : gen 0
survived a gen0 collection   : gen 1
survived a gen1 collection   : gen 2
```

Demo 4 allocates 2,000,000 short-lived `byte[100]` objects:

```
gen0 +40, gen1 +0, gen2 +0
```

Short-lived garbage mostly costs cheap gen0 collections. The expensive collections happen when lots of data survives into gen2.

### What a collection does

1. Briefly **suspends** managed threads (stop-the-world).
2. **Marks** everything reachable from the roots.
3. Reclaims the rest by **compacting** (moving survivors together and fixing references) or **sweeping** (recording the free gaps). The GC decides which.
4. Resumes threads.

Gen2 collections can run mostly on a **background thread** (concurrent GC), so the app pauses less. The demo prints latency mode `Interactive`, which is the default with concurrent GC on. Running with `DOTNET_gcConcurrent=0` made it print `Batch` instead (checked).

---

## The Large Object Heap (LOH)

Objects of **85,000 bytes or more** are allocated on the LOH instead of gen0 (demo 3):

```
byte[80_000] starts in gen 0
byte[85_000] starts in gen 2   (LOH is collected together with gen2)
```

- It is collected only with gen2, so large short-lived arrays cause expensive gen2 collections.
- It is not compacted by default, so it can **fragment**.
- Remedies: reuse big buffers with `ArrayPool<T>`, avoid growing huge `List<T>`/`MemoryStream` repeatedly, stream instead of loading everything.

---

## Workstation vs Server GC

| | Workstation | Server |
|---|---|---|
| Heaps | One | One per core, with dedicated GC threads |
| Optimised for | Responsiveness on a desktop or small box | Throughput on a many-core server |
| Memory use | Lower | Higher |

Defaults differ by project type. The console demo printed `Server GC: False`. A `dotnet new web` project got `"System.GC.Server": true` in its generated `runtimeconfig.json` (see [01](./01_Runtime_Basics.md)). Check `GCSettings.IsServerGC` in the process you care about.

---

## Finalizers: a safety net, not a cleanup plan

```csharp
~HasFinalizer() { ... }   // runs on the dedicated finalizer thread
```

Demo 6 prints the thread that ran it:

```
main thread is managed thread 2
  [finalizer] ran on managed thread 1
```

- It runs on **another thread**, at a time you don't control.
- An object with a finalizer cannot be freed at the first GC that finds it unreachable. It is queued for finalization first, so it survives at least one extra collection and gets promoted. [Mechanism from the docs, not measured in the demo.]
- Finalizers are not guaranteed to run when the process exits on modern .NET. [Documented behaviour, not demonstrated here.]

Write a finalizer only when you directly own an unmanaged resource, and even then prefer a `SafeHandle`.

---

## `IDisposable`: deterministic cleanup

Release resources when you say so, not when the GC gets around to it. `using` calls `Dispose` even if an exception is thrown (demo 7):

```
  working with the resource...
  [Dispose] released immediately, no GC needed
  caught the exception (Dispose already ran before this line)
```

Rules of thumb:

- If you create an `IDisposable` (stream, `DbConnection`, `HttpResponseMessage`), put it in a `using`.
- If your class **owns** an `IDisposable` field, your class should implement `IDisposable` and dispose it.
- `Dispose` frees resources. It does **not** free managed memory. The GC still does that.

---

## Managed "leaks": memory the GC is right not to free

A leak in .NET is almost always **something still references the object**. Demo 8 here (event subscription) and demo 8 in [04](./04_Static_Classes.md) (static collection) show the two classic shapes:

| Cause | Why the object stays alive |
|---|---|
| **Static collection that only grows** (an unbounded cache) | The static field is a root, so everything inside survives |
| **Event subscription never removed** | The publisher's delegate list references the subscriber. A long-lived publisher keeps every subscriber alive |
| Long-lived timer or background task capturing a big object | The closure references it |
| `Task` that never completes | Whatever it captured stays rooted |

Demo 8:

```
subscriber out of scope, still subscribed : alive = True
after unsubscribing                       : alive = False
```

Fixes: unsubscribe (`-=`), bound your caches (`IMemoryCache` with size and expiry), use `WeakReference` when you genuinely want "keep it only if someone else does".

---

## Don't call `GC.Collect()` in production

The demos call it only to make the invisible visible. In real code it forces an expensive collection and fights the GC's own tuning. Fix the cause instead: allocate less, release references, reuse buffers.

---

## Why the demos look the way they do

Two hard-won details:

1. **Release vs Debug.** A full Debug run printed the same results as a Release run for every GC demo. Only the escape-analysis demo in [02](./02_Memory_Allocation.md) changed (24 bytes instead of 0). That is because the GC demos create their objects in helpers that have already returned. I did not test a variant that creates the object in the calling method under Debug.
2. **Allocations live in `[MethodImpl(NoInlining)]` helpers.** In the finalizer demo, an object created directly in the calling method was *not* finalized during the demo section (its `[finalizer]` line appeared much later, during the next demo). Moving the creation into a helper that has already returned fixed it. The likely cause is that unoptimised tier-0 code kept the temporary alive until the caller returned. Treat that as the likely reason, not a proof.

Lesson: **what is reachable is decided by the compiled code, and it may differ from what you read in the source.**

---

## Diagnosing GC problems

| Symptom | Where to look |
|---|---|
| High `% time in GC`, latency spikes | `dotnet-counters`: gen0/gen1/gen2 collection counts, allocation rate |
| Memory keeps growing | `dotnet-gcdump` or a profiler: *who is rooting these objects?* |
| LOH growth, many gen2 collections | Large arrays or strings created repeatedly |
| Need numbers in code | `GC.CollectionCount(n)`, `GC.GetGCMemoryInfo()`, `GC.GetAllocatedBytesForCurrentThread()` |

---

## Self-check

1. An object has no local variable pointing at it. Why might it still be alive?
2. Why is a gen0 collection cheap and a gen2 collection expensive?
3. A `byte[100_000]` is allocated in a loop and discarded each time. Which generation pays for it?
4. When does `Dispose` run, and when does the finalizer run? Which one can you rely on?
5. A long-lived service subscribes to a static event in every request and never unsubscribes. What happens to memory over time?
