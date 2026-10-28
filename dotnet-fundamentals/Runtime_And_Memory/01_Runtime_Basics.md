# 01 — Runtime Basics

> **Core idea:** Your `.dll` does not contain machine code. It contains IL (a portable instruction set) plus metadata. The runtime turns IL into machine code, method by method, the first time each method is called.

Demo: [01_Runtime_Basics.cs](./01_Runtime_Basics.cs)

---

## ".NET Core" vs ".NET" vs ".NET Framework"

| Name | What it is |
|---|---|
| **.NET Framework** | The original Windows-only runtime (4.x). Maintenance mode. |
| **.NET Core** | The cross-platform rewrite (1.0 to 3.1). Its runtime is called **CoreCLR**. |
| **.NET** (5 and later) | .NET Core with the "Core" dropped. Same CoreCLR runtime. The demo prints `.NET 10.0.12`. |

When people say "dotnet core runtime" today they mean CoreCLR, shipped as part of .NET.

---

## From `dotnet run` to `Main`

```
 C# source
    │  Roslyn compiler (build time)
    ▼
 MyApp.dll  =  IL + metadata        MyApp.runtimeconfig.json   MyApp.deps.json
    │                                  (which runtime version,    (which assemblies,
    │                                   GC settings)               where they are)
    ▼
 Host (dotnet.exe / apphost)
    │  hostfxr    reads runtimeconfig.json, picks the installed runtime
    │  hostpolicy reads deps.json, builds the "trusted platform assemblies" list
    ▼
 CoreCLR starts
    │  loads System.Private.CoreLib (Object, String, List<T>, GC, threads...)
    │  creates the managed heap, starts the finalizer thread
    ▼
 Loads MyApp.dll, finds Main
    │  JIT compiles Main (IL -> machine code), runs it
    ▼
 Each new method you call: JIT compiles it on first call, then reuses the machine code
```

What the demo printed on this machine:

| Fact | Value |
|---|---|
| `typeof(object)` lives in | `System.Private.CoreLib`, not in your assembly |
| Assemblies available to the runtime (the TPA list) | 174 |
| Assemblies actually loaded when `Main` was already running | 8 |

**Assemblies load lazily.** Referencing a library does not load it. It loads when the first method that needs it runs. (The exact numbers vary by machine and runtime version.)

---

## The pieces of the runtime

| Piece | Job |
|---|---|
| **Host** | Finds the right runtime on disk and starts it |
| **Type loader** | Reads metadata and builds the in-memory representation of types |
| **JIT compiler** | IL to machine code |
| **Garbage collector** | Allocates the managed heap and reclaims it ([03](./03_Garbage_Collection.md)) |
| **Base class library** | `System.Private.CoreLib` and the rest of the framework |
| **Thread pool** | Reuses OS threads for `Task`s and async work |

---

## The JIT and tiered compilation

The demo times a method that has never been called, then calls it again:

| Call | Time | Methods JIT-compiled on this thread |
|---|---|---|
| 1st | about 57 µs | +1 |
| 2nd | about 0 µs | +0 |

The first call paid for compilation. The second reused the machine code. (Timings vary per run. The `+1` and `+0` are the point.)

**Tiered compilation** (on by default):

1. First call: compile quickly with few optimisations (**tier-0**). Startup is fast.
2. Methods that turn out to be hot are recompiled with full optimisation (**tier-1**).
3. Framework code ships **ReadyToRun** (pre-compiled), so it starts without waiting for the JIT.

Consequence you will meet in these notes: code running at tier-0 is unoptimised, and unoptimised code can keep local variables alive longer than you expect. That is one reason the GC demos ([03](./03_Garbage_Collection.md)) put allocations in separate helper methods.

The exact thresholds for promoting a method to tier-1 are runtime internals and have changed between versions, so these notes don't quote them.

---

## One process, one runtime

- A .NET process hosts **one** runtime instance. Your static fields, the GC heap and the thread pool belong to that process.
- **AppDomains for isolation are gone** in modern .NET. `AppDomain.CurrentDomain` still exists, but it is the only one. To load plug-ins in isolation use `AssemblyLoadContext`.
- Managed threads are backed by OS threads. In ASP.NET Core, requests run on thread-pool threads, so **many requests execute concurrently inside the same process and share the same static fields**. [04](./04_Static_Classes.md) and [05](./05_When_To_Use_Static.md) build on this.

---

## Ways to ship the runtime

| Mode | Runtime comes from | Notes |
|---|---|---|
| **Framework-dependent** (default) | Installed on the machine | Small output; target machine needs the runtime |
| **Self-contained** | Bundled with your app | Large output; no install needed |
| **ReadyToRun** (`PublishReadyToRun`) | Either of the above | Pre-compiled code, faster startup, larger files |
| **Native AOT** (`PublishAot`) | No JIT at all, compiled to native code ahead of time | Fastest startup, but restrictions (limited reflection and dynamic code) |

---

## Settings you will actually touch

These go in the `.csproj` and end up in `runtimeconfig.json`:

| Property | Effect |
|---|---|
| `ServerGarbageCollector` | Server GC (see [03](./03_Garbage_Collection.md)) |
| `ConcurrentGarbageCollection` | Background gen2 collections |
| `TieredCompilation` | Turn tiering off or on |

**Defaults differ by project type.** The console demo prints `Server GC? False`. A project created with `dotnet new web` (SDK `Microsoft.NET.Sdk.Web`) got `"System.GC.Server": true` in its generated `runtimeconfig.json` (checked on SDK 10.0.401). Do not assume one project type's default applies to the other.

---

## Self-check

1. Why does the second call to a method cost less than the first?
2. Where does `System.Object` live, and how do you know it is not in your own assembly?
3. You referenced a NuGet package, but it does not show up in `AppDomain.CurrentDomain.GetAssemblies()`. Is that a bug?
4. A web API and a console tool target the same .NET version. Do they use the same GC mode by default?
