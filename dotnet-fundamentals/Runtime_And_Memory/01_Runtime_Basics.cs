// ============================================================
// RUNTIME BASICS — what is actually running your C# code?
// Key idea: your .dll holds IL, not machine code. The runtime JIT-compiles
// each method the first time it is called.
// ============================================================

using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RuntimeMemory.Concepts.RuntimeBasics;

public static class RuntimeBasicsDemo
{
    public static void Run()
    {
        WhoAmIRunningOn();
        WhereBaseTypesLive();
        WhatIsLoaded();
        JitCompilesOnFirstCall();
    }

    // ── 1. Which runtime is this? ─────────────────────────────────────────────
    private static void WhoAmIRunningOn()
    {
        Console.WriteLine("-- 1. Runtime identity --");
        Console.WriteLine($"Framework        : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Runtime id       : {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"Process arch     : {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"OS               : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Logical CPUs     : {Environment.ProcessorCount}");
        Console.WriteLine($"Server GC?       : {GCSettings.IsServerGC}");
        Console.WriteLine($"GC latency mode  : {GCSettings.LatencyMode}");
    }

    // ── 2. System.Object does not live in your assembly ───────────────────────
    private static void WhereBaseTypesLive()
    {
        Console.WriteLine("-- 2. Where do base types come from? --");
        var coreLib = typeof(object).Assembly;
        Console.WriteLine($"typeof(object) is in : {coreLib.GetName().Name}");
        Console.WriteLine($"  loaded from        : {coreLib.Location}");

        var mine = Assembly.GetExecutingAssembly();
        Console.WriteLine($"My code is in        : {mine.GetName().Name}  ({Path.GetFileName(mine.Location)})");
    }

    // ── 3. Trusted platform assemblies vs actually loaded ─────────────────────
    private static void WhatIsLoaded()
    {
        Console.WriteLine("-- 3. Assemblies --");
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
        var available = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Length;
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Length;
        Console.WriteLine($"Available to the runtime (TPA list) : {available}");
        Console.WriteLine($"Actually loaded right now           : {loaded}  <- assemblies load lazily");
    }

    // ── 4. First call pays the JIT; second call does not ──────────────────────
    private static void JitCompilesOnFirstCall()
    {
        Console.WriteLine("-- 4. JIT on first call --");

        long methodsBefore = JitInfo.GetCompiledMethodCount(currentThread: true);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int a = NeverCalledBefore(1);
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        long methodsAfterFirst = JitInfo.GetCompiledMethodCount(currentThread: true);

        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        int b = NeverCalledBefore(2);
        long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
        long methodsAfterSecond = JitInfo.GetCompiledMethodCount(currentThread: true);

        Console.WriteLine($"1st call: {Micros(t0, t1),8:F1} us, methods JIT-compiled on this thread: +{methodsAfterFirst - methodsBefore}");
        Console.WriteLine($"2nd call: {Micros(t2, t3),8:F1} us, methods JIT-compiled on this thread: +{methodsAfterSecond - methodsAfterFirst}");
        Console.WriteLine($"(results {a}, {b} - printed so the calls are not optimised away)");
        Console.WriteLine($"Total JIT time so far in this process: {JitInfo.GetCompilationTime().TotalMilliseconds:F1} ms");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NeverCalledBefore(int x) => x * 31 + 7;

    private static double Micros(long start, long end)
        => (end - start) * 1_000_000.0 / System.Diagnostics.Stopwatch.Frequency;
}
