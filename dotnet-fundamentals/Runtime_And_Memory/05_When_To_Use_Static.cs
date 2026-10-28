// ============================================================
// WHEN TO USE STATIC — and what to use instead
// ============================================================

using System.Runtime.CompilerServices;

namespace RuntimeMemory.Concepts.WhenToUseStatic;

public static class WhenToUseStaticDemo
{
    public static void Run()
    {
        MutableStaticStateIsARace();
        StaticLambdasAvoidClosureAllocations();
        HiddenStaticDependencyVsInjectedOne();
        StaticVersusSingleton();
    }

    // ── 1. GOOD static: pure functions. Wrong static: shared mutable state ─────
    private static class HitCounter
    {
        public static int Unsafe;
        public static int Safe;
    }

    private static void MutableStaticStateIsARace()
    {
        Console.WriteLine("-- 1. Shared mutable static under concurrency --");
        const int n = 1_000_000;

        Parallel.For(0, n, _ =>
        {
            HitCounter.Unsafe++;                          // read-modify-write, NOT atomic
            Interlocked.Increment(ref HitCounter.Safe);   // atomic
        });

        Console.WriteLine($"CPUs: {Environment.ProcessorCount}, expected {n}");
        Console.WriteLine($"plain static ++     : {HitCounter.Unsafe}  (lost {n - HitCounter.Unsafe})");
        Console.WriteLine($"Interlocked         : {HitCounter.Safe}");
        Console.WriteLine("In a web app every request is a concurrent caller of your static fields.");
    }

    // ── 2. `static` on a lambda: guaranteed no closure, no per-call allocation ─
    private static void StaticLambdasAvoidClosureAllocations()
    {
        Console.WriteLine("-- 2. static lambda vs capturing lambda (1000 calls) --");
        const int calls = 1000;

        // Each lambda site caches its delegate on first use, so run each loop once to warm up.
        StaticLambdaLoop(1);
        PlainNonCapturingLambdaLoop(1);
        CapturingLambdaLoop(1);

        long before = GC.GetAllocatedBytesForCurrentThread();
        StaticLambdaLoop(calls);
        long noCapture = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        PlainNonCapturingLambdaLoop(calls);
        long plain = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        CapturingLambdaLoop(calls);
        long capture = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"static lambda               : {noCapture,7} bytes total, {noCapture / (double)calls,5:F1} per call");
        Console.WriteLine($"non-static, captures nothing: {plain,7} bytes total, {plain / (double)calls,5:F1} per call  (compiler caches it too)");
        Console.WriteLine($"capturing lambda            : {capture,7} bytes total, {capture / (double)calls,5:F1} per call  (closure object + new delegate each time)");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PlainNonCapturingLambdaLoop(int calls)
    {
        for (int i = 0; i < calls; i++) Apply(x => x + 1);          // no `static` keyword, but nothing captured
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StaticLambdaLoop(int calls)
    {
        for (int i = 0; i < calls; i++) Apply(static x => x + 1);   // no captures: one cached delegate reused forever
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CapturingLambdaLoop(int calls)
    {
        for (int i = 0; i < calls; i++) Apply(x => x + i);          // captures i: closure + delegate every iteration
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Apply(Func<int, int> f) => f(1);

    // ── 3. Static hard-wires a dependency; an abstraction lets tests control it ─
    private static class GreeterStatic
    {
        public static string Greet() => DateTime.UtcNow.Hour < 12 ? "Good morning" : "Good afternoon";  // untestable at 9am vs 3pm
    }

    private sealed class Greeter(TimeProvider clock)
    {
        public string Greet() => clock.GetUtcNow().Hour < 12 ? "Good morning" : "Good afternoon";
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static void HiddenStaticDependencyVsInjectedOne()
    {
        Console.WriteLine("-- 3. Testability: DateTime.UtcNow vs TimeProvider --");
        Console.WriteLine($"static version  : '{GreeterStatic.Greet()}'  (depends on when you run it; no way to force the other branch)");
        Console.WriteLine($"injected 09:00  : '{new Greeter(new FixedTime(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))).Greet()}'");
        Console.WriteLine($"injected 15:00  : '{new Greeter(new FixedTime(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero))).Greet()}'");
        Console.WriteLine("TimeProvider is the framework's own answer to static DateTime.UtcNow.");
    }

    // ── 4. Static class vs singleton instance ─────────────────────────────────
    private interface IIdGenerator { int Next(); }

    private sealed class SequentialIds : IIdGenerator
    {
        private int _last;
        public int Next() => Interlocked.Increment(ref _last);
    }

    private static class IdGeneratorStatic
    {
        private static int _last;
        public static int Next() => Interlocked.Increment(ref _last);
    }

    private sealed class OrderService(IIdGenerator ids)
    {
        public string Place() => $"order-{ids.Next()}";
    }

    private sealed class StubIds : IIdGenerator
    {
        public int Next() => 7;
    }

    private static void StaticVersusSingleton()
    {
        Console.WriteLine("-- 4. Static class vs singleton instance --");
        var singleton = new SequentialIds();                    // in ASP.NET Core: services.AddSingleton<IIdGenerator, SequentialIds>()
        Console.WriteLine($"singleton via interface : {new OrderService(singleton).Place()}, {new OrderService(singleton).Place()}");
        Console.WriteLine($"swapped for a stub      : {new OrderService(new StubIds()).Place()}   (same OrderService, different behaviour)");
        Console.WriteLine($"static class            : order-{IdGeneratorStatic.Next()}   (callers cannot be given a different implementation)");
    }
}
