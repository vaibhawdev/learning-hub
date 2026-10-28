// ============================================================
// GARBAGE COLLECTION — generations, LOH, roots, finalizers, IDisposable
// Objects are created in NoInlining helpers so no live frame references them at GC.Collect().
// ============================================================

using System.Runtime;
using System.Runtime.CompilerServices;

namespace RuntimeMemory.Concepts.GarbageCollection;

public static class GarbageCollectionDemo
{
    private static object? _sink;

    public static void Run()
    {
        GcConfiguration();
        GenerationPromotion();
        LargeObjectHeap();
        ShortLivedGarbageIsCheap();
        ReachabilityDecidesLifetime();
        FinalizerRunsOnAnotherThread();
        DisposeIsDeterministic();
        EventHandlerLeak();
    }

    // ── 1. What GC am I using? ────────────────────────────────────────────────
    private static void GcConfiguration()
    {
        Console.WriteLine("-- 1. GC configuration --");
        var info = GC.GetGCMemoryInfo();
        Console.WriteLine($"Server GC            : {GCSettings.IsServerGC}");
        Console.WriteLine($"Latency mode         : {GCSettings.LatencyMode}");
        Console.WriteLine($"Max generation       : {GC.MaxGeneration}");
        Console.WriteLine($"Memory GC may use    : {info.TotalAvailableMemoryBytes / (1024 * 1024)} MB");
        Console.WriteLine($"Managed heap now     : {GC.GetTotalMemory(false) / 1024} KB");
    }

    // ── 2. Survivors are promoted: gen0 -> gen1 -> gen2 ───────────────────────
    private static void GenerationPromotion()
    {
        Console.WriteLine("-- 2. Generations --");
        var o = new object();
        Console.WriteLine($"fresh object                 : gen {GC.GetGeneration(o)}");

        GC.Collect(0);   // collect gen0 only; survivors move to gen1
        Console.WriteLine($"survived a gen0 collection   : gen {GC.GetGeneration(o)}");

        GC.Collect(1);   // collect gen0+gen1; survivors move to gen2
        Console.WriteLine($"survived a gen1 collection   : gen {GC.GetGeneration(o)}");

        GC.KeepAlive(o); // keeps `o` reachable up to here so it is a genuine survivor
    }

    // ── 3. Big arrays skip gen0 and go to the Large Object Heap ───────────────
    private static void LargeObjectHeap()
    {
        Console.WriteLine("-- 3. Large Object Heap --");
        var small = new byte[80_000];
        var large = new byte[85_000];
        Console.WriteLine($"byte[80_000] starts in gen {GC.GetGeneration(small)}");
        Console.WriteLine($"byte[85_000] starts in gen {GC.GetGeneration(large)}   (LOH is collected together with gen2)");
        GC.KeepAlive(small);
        GC.KeepAlive(large);
    }

    // ── 4. Short-lived garbage mostly costs gen0 collections ──────────────────
    private static void ShortLivedGarbageIsCheap()
    {
        Console.WriteLine("-- 4. Churn of short-lived objects --");
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);

        for (int i = 0; i < 2_000_000; i++) _sink = new byte[100];

        Console.WriteLine($"2,000,000 x new byte[100] caused: gen0 +{GC.CollectionCount(0) - g0}, " +
                          $"gen1 +{GC.CollectionCount(1) - g1}, gen2 +{GC.CollectionCount(2) - g2}");
    }

    // ── 5. The GC frees what is UNREACHABLE, not what you stopped "using" ─────
    private static readonly List<object> StaticRoots = new();

    private static void ReachabilityDecidesLifetime()
    {
        Console.WriteLine("-- 5. Reachability --");

        var unreferenced = CreateWeak(rooted: false);
        var rooted       = CreateWeak(rooted: true);
        GC.Collect();
        Console.WriteLine($"no references left          : alive = {unreferenced.IsAlive}");
        Console.WriteLine($"held by a static list       : alive = {rooted.IsAlive}");

        StaticRoots.Clear();
        GC.Collect();
        Console.WriteLine($"after removing from the list: alive = {rooted.IsAlive}");
    }

    // NoInlining keeps the strong reference in a stack frame that is gone when we return.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeak(bool rooted)
    {
        var obj = new object();
        if (rooted) StaticRoots.Add(obj);
        return new WeakReference(obj);
    }

    // ── 6. Finalizers run on a dedicated thread, at an unpredictable time ─────
    private sealed class HasFinalizer
    {
        ~HasFinalizer() => Console.WriteLine($"  [finalizer] ran on managed thread {Environment.CurrentManagedThreadId}");
    }

    private static void FinalizerRunsOnAnotherThread()
    {
        Console.WriteLine("-- 6. Finalizer --");
        Console.WriteLine($"main thread is managed thread {Environment.CurrentManagedThreadId}");
        CreateAndAbandon();
        GC.Collect();                   // finds the object unreachable, queues it for finalization
        GC.WaitForPendingFinalizers();  // demo only: normally you never wait on the finalizer thread
    }

    // The object must be created in a frame that has already returned. If the caller itself
    // created it, unoptimised (tier-0) code can keep the temporary alive until the method ends.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandon() => _ = new HasFinalizer();

    // ── 7. IDisposable: deterministic cleanup, even when an exception is thrown ─
    private sealed class FileLikeResource : IDisposable
    {
        public void Dispose() => Console.WriteLine("  [Dispose] released immediately, no GC needed");
    }

    private static void DisposeIsDeterministic()
    {
        Console.WriteLine("-- 7. using / Dispose --");
        try
        {
            using var res = new FileLikeResource();
            Console.WriteLine("  working with the resource...");
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("  caught the exception (Dispose already ran before this line)");
        }
    }

    // ── 8. A classic managed "leak": a long-lived publisher keeps subscribers alive ─
    private sealed class Publisher
    {
        public event EventHandler? Changed;
        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
        public void UnsubscribeEveryone() => Changed = null;   // stands in for `-=` when you no longer hold the handler
    }

    private sealed class Subscriber
    {
        public void OnChanged(object? s, EventArgs e) { }
    }

    private static readonly Publisher LongLivedPublisher = new();   // static => lives for the whole process

    private static void EventHandlerLeak()
    {
        Console.WriteLine("-- 8. Event subscription leak --");

        var weak = Subscribe();
        GC.Collect();
        Console.WriteLine($"subscriber out of scope, still subscribed : alive = {weak.IsAlive}");

        LongLivedPublisher.UnsubscribeEveryone();
        GC.Collect();
        Console.WriteLine($"after unsubscribing                       : alive = {weak.IsAlive}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Subscribe()
    {
        var sub = new Subscriber();
        LongLivedPublisher.Changed += sub.OnChanged;   // the event's delegate list now references `sub`
        return new WeakReference(sub);
    }
}
