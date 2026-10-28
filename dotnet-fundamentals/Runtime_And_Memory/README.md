# Runtime & Memory

How a .NET program actually runs: what the runtime does with your code, where objects live, how memory is reclaimed, and what `static` really means.

> Verified on .NET 10.0.12 (SDK 10.0.401), Windows x64, 2026-10-29. Byte counts assume a 64-bit process and can differ by runtime version. The numbers quoted in these notes come from runs of the demos in this folder. Timings and race-condition counts vary per run. Compiler error codes and the ASP.NET Core GC default were checked against the SDK and are labelled as such.

## Reading order

| # | Notes | Demo | Question it answers |
|---|-------|------|---------------------|
| 01 | [Runtime Basics](./01_Runtime_Basics.md) | [.cs](./01_Runtime_Basics.cs) | What runs my C#? What happens between `dotnet run` and `Main`? |
| 02 | [Memory Allocation](./02_Memory_Allocation.md) | [.cs](./02_Memory_Allocation.cs) | Stack or heap? What does an object cost? What is boxing? |
| 03 | [Garbage Collection](./03_Garbage_Collection.md) | [.cs](./03_Garbage_Collection.cs) | How is memory freed? Generations, LOH, finalizers, `IDisposable`, leaks |
| 04 | [Static Classes](./04_Static_Classes.md) | [.cs](./04_Static_Classes.cs) | What does the runtime do with `static`? Initialisation, lifetime, GC roots |
| 05 | [When To Use Static](./05_When_To_Use_Static.md) | [.cs](./05_When_To_Use_Static.cs) | Static vs singleton vs DI. Where static helps and where it hurts |

## Run the demos

```powershell
cd dotnet-fundamentals/Runtime_And_Memory
dotnet run -c Release -- 3        # one demo: 1, 2, 3, 4 or 5
dotnet run -c Release -- all
```

**Use `-c Release`.** I diffed a full Debug run against a full Release run: exactly one result changes. The escape-analysis demo in [02](./02_Memory_Allocation.md) prints 24 heap bytes in Debug instead of 0, because Debug turns off the optimisation that moves the object to the stack. Every GC and `WeakReference` demo printed the same in both, because they allocate inside `[MethodImpl(NoInlining)]` helpers whose frames are gone before `GC.Collect()` runs. Release is still the right default for anything you measure. [03](./03_Garbage_Collection.md) explains the helpers.

The project file only covers this folder. It does not pull in the Dependency Injection samples next door.

## Related

- Syllabus (outline only, to be filled in): [CLR Runtime Internals](../../system-design/Phase-02-Deep-DotNet-Internals/01-clr-runtime-internals.md). These notes are the worked explanations for part of it.
- Static state versus injected services: [DI service lifetimes](../DependencyInjection/02_ServiceLifetimes/README.md) and [DI gotchas](../DependencyInjection/06_Gotchas/README.md).
- Why hidden static dependencies hurt: [Dependency Inversion](../SOLID_Principles/05_Dependency_Inversion.md).
