// ============================================================
// STATIC CLASSES — what the runtime actually does with them
// Every claim in 04_Static_Classes.md that is checkable is checked here.
// ============================================================

using System.Reflection;
using System.Runtime.CompilerServices;

namespace RuntimeMemory.Concepts.StaticBehaviour;

public static class StaticBehaviourDemo
{
    public static void Run()
    {
        WhatTheCompilerEmits();
        StaticConstructorTiming();
        BeforeFieldInitTiming();
        InitializationRunsOnceEvenWhenRaced();
        FailedInitializationIsPermanent();
        StaticFieldsAreSharedPerClosedType();
        ThreadStaticInitializerRunsOnce();
        StaticFieldsAreGcRoots();
    }

    private static void Log(string msg) => Console.WriteLine($"  {msg}");

    // ── 1. A static class is just an abstract + sealed class ──────────────────
    private static class MathHelpers
    {
        public static int Double(int x) => x * 2;
    }

    private static void WhatTheCompilerEmits()
    {
        Console.WriteLine("-- 1. What `static class` compiles to --");
        var t = typeof(MathHelpers);
        Console.WriteLine($"IsAbstract = {t.IsAbstract}, IsSealed = {t.IsSealed}  (abstract+sealed = cannot be instantiated or inherited)");
        Console.WriteLine($"Instance constructors: {t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length}");
    }

    // ── 2. Explicit static constructor: runs at first access, exactly once ────
    private static class WithStaticCtor
    {
        public static readonly string Tag;
        static WithStaticCtor() { Log("[cctor] WithStaticCtor initialised"); Tag = "ready"; }
        public static void Touch() { }
    }

    private static void StaticConstructorTiming()
    {
        Console.WriteLine("-- 2. Explicit static constructor --");
        Log("before first use");
        WithStaticCtor.Touch();
        Log("after Touch()   <- cctor ran just before Touch() executed");
        WithStaticCtor.Touch();
        Log("after 2nd Touch() <- no second [cctor] line");
    }

    // ── 3. Field initialisers only => `beforefieldinit`: timing is unspecified ─
    private static class WithFieldInitialiser
    {
        public static readonly string Tag = Init();
        private static string Init() { Log("[initialiser] WithFieldInitialiser initialised"); return "ready"; }
        public static void Touch() { }
    }

    private static void BeforeFieldInitTiming()
    {
        Console.WriteLine("-- 3. Field initialiser only (beforefieldinit) --");
        Log("calling Touch() - touches no static field");
        WithFieldInitialiser.Touch();
        Log("Touch() returned");
        ReadTheField();
        Log("ReadTheField() returned");
    }

    // The spec only promises: initialised at or before the FIRST ACCESS TO A STATIC FIELD.
    // Where exactly depends on the JIT, so we do not draw conclusions from the order of lines.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ReadTheField()
    {
        Log("ReadTheField() entered");
        _ = WithFieldInitialiser.Tag;
    }

    // ── 4. Initialisation is thread-safe and happens once ─────────────────────
    private static class RaceTarget
    {
        public static int InitCount;
        static RaceTarget() { Interlocked.Increment(ref InitCount); Thread.Sleep(50); }
        public static void Touch() { }
    }

    private static void InitializationRunsOnceEvenWhenRaced()
    {
        Console.WriteLine("-- 4. 8 threads touch the type at the same moment --");
        using var gate = new ManualResetEventSlim(false);
        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() => { gate.Wait(); RaceTarget.Touch(); })).ToList();
        threads.ForEach(t => t.Start());
        gate.Set();
        threads.ForEach(t => t.Join());
        Console.WriteLine($"static constructor ran {RaceTarget.InitCount} time(s)");
    }

    // ── 5. If initialisation throws, the type is unusable for the process ─────
    private static int _fragileInitAttempts;

    private static class Fragile
    {
        public static readonly int Value = Compute();
        private static int Compute()
        {
            Interlocked.Increment(ref _fragileInitAttempts);
            throw new InvalidOperationException("config file missing");
        }
    }

    private static void FailedInitializationIsPermanent()
    {
        Console.WriteLine("-- 5. Failed static initialisation --");
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try { _ = Fragile.Value; }
            catch (TypeInitializationException ex)
            {
                Console.WriteLine($"attempt {attempt}: {ex.GetType().Name} -> inner: {ex.InnerException?.Message}");
            }
        }
        Console.WriteLine($"initialiser body executed {_fragileInitAttempts} time(s) across 2 accesses -> the failure is cached and re-thrown");
    }

    // ── 6. Each closed generic type has its OWN statics ───────────────────────
    private static class Counter<T> { public static int Hits; }

    private static void StaticFieldsAreSharedPerClosedType()
    {
        Console.WriteLine("-- 6. Statics in generic types --");
        Counter<int>.Hits += 3;
        Counter<string>.Hits += 1;
        Console.WriteLine($"Counter<int>.Hits = {Counter<int>.Hits}, Counter<string>.Hits = {Counter<string>.Hits}  (separate fields)");
    }

    // ── 7. [ThreadStatic] = one copy per thread, initialiser runs once ────────
    private static class PerThread
    {
        [ThreadStatic] public static int Value = 5;
    }

    private static void ThreadStaticInitializerRunsOnce()
    {
        Console.WriteLine("-- 7. [ThreadStatic] with an initialiser --");
        Console.WriteLine($"thread {Environment.CurrentManagedThreadId} (first to touch it): Value = {PerThread.Value}");
        int other = -1;
        var t = new Thread(() => other = PerThread.Value);
        t.Start(); t.Join();
        Console.WriteLine($"a second thread sees                  : Value = {other}  (the `= 5` ran only for the first thread)");
    }

    // ── 8. Static fields are GC roots: whatever they reference cannot be freed ─
    private static class LeakyCache
    {
        public static readonly List<byte[]> Items = new();
    }

    private static void StaticFieldsAreGcRoots()
    {
        Console.WriteLine("-- 8. Static fields keep objects alive --");
        var weak = AddToCache();
        GC.Collect();
        Console.WriteLine($"no local variable references it, static list does : alive = {weak.IsAlive}");

        LeakyCache.Items.Clear();
        GC.Collect();
        Console.WriteLine($"after clearing the static list                     : alive = {weak.IsAlive}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddToCache()
    {
        var payload = new byte[1024];
        LeakyCache.Items.Add(payload);
        return new WeakReference(payload);
    }
}
