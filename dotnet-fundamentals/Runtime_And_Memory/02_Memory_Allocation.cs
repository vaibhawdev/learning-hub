// ============================================================
// MEMORY ALLOCATION — stack vs heap, value vs reference, boxing
// Measured with GC.GetAllocatedBytesForCurrentThread() so the numbers are real.
// ============================================================

using System.Collections;
using System.Runtime.CompilerServices;

namespace RuntimeMemory.Concepts.MemoryAllocation;

public static class MemoryAllocationDemo
{
    // Everything we measure is stored here. Why: modern JITs perform ESCAPE ANALYSIS and may
    // put an object that never leaves its method on the STACK (zero heap bytes). Storing the
    // object in a static field makes it "escape", so we measure a genuine heap allocation.
    private static object? _sink;
    private static int _fortyTwo = 42;

    public static void Run()
    {
        ValueVsReferenceCopySemantics();
        ObjectSizes();
        BoxingCost();
        StringConcatCost();
        StackAllocation();
        EscapeAnalysis();
    }

    // ── 1. Copy semantics: the practical difference ───────────────────────────
    private struct PointStruct { public int X; }
    private sealed class PointClass { public int X; }

    private static void ValueVsReferenceCopySemantics()
    {
        Console.WriteLine("-- 1. struct copies, class shares --");

        var s1 = new PointStruct { X = 1 };
        var s2 = s1;          // copies the whole value
        s2.X = 99;

        var c1 = new PointClass { X = 1 };
        var c2 = c1;          // copies only the reference; both point at ONE object
        c2.X = 99;

        Console.WriteLine($"struct: s1.X = {s1.X}   (unchanged, s2 was a copy)");
        Console.WriteLine($"class : c1.X = {c1.X}  (changed, c2 is the same object)");
    }

    // ── 2. How many bytes does one object really cost? ────────────────────────
    private static void ObjectSizes()
    {
        Console.WriteLine("-- 2. Heap bytes per allocation (64-bit) --");
        Console.WriteLine($"new object()                    : {Bytes(() => new object()),5} bytes");
        Console.WriteLine($"new PointClass() (one int)      : {Bytes(() => new PointClass()),5} bytes");
        Console.WriteLine($"boxed int                       : {Bytes(() => (object)_fortyTwo),5} bytes");
        Console.WriteLine($"new byte[0]                     : {Bytes(() => new byte[0]),5} bytes");
        Console.WriteLine($"new byte[100]                   : {Bytes(() => new byte[100]),5} bytes");
        Console.WriteLine($"new int[10]                     : {Bytes(() => new int[10]),5} bytes");
        Console.WriteLine($"new PointStruct[1000]           : {Bytes(() => new PointStruct[1000]),5} bytes");
        Console.WriteLine($"new string('x', 10)             : {Bytes(() => new string('x', 10)),5} bytes");
    }

    // Warm up once (JIT + delegate setup allocate too), then measure the second call.
    private static long Bytes(Func<object> alloc)
    {
        Measure(alloc);
        return Measure(alloc);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Measure(Func<object> alloc)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        _sink = alloc();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // ── 3. Boxing: a hidden allocation per value ──────────────────────────────
    private static void BoxingCost()
    {
        Console.WriteLine("-- 3. Boxing: 1000 ints into a collection --");

        long before = GC.GetAllocatedBytesForCurrentThread();
        _sink = FillArrayList(1000);
        long boxed = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        _sink = FillGenericList(1000);
        long generic = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"ArrayList (object, boxes each int) : {boxed,7} bytes");
        Console.WriteLine($"List<int> (no boxing)              : {generic,7} bytes");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArrayList FillArrayList(int n)
    {
        var list = new ArrayList(n);                 // capacity preset so growth does not skew the result
        for (int i = 0; i < n; i++) list.Add(i);     // each Add boxes i -> new heap object
        return list;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<int> FillGenericList(int n)
    {
        var list = new List<int>(n);
        for (int i = 0; i < n; i++) list.Add(i);     // ints stored inline in the backing array
        return list;
    }

    // ── 4. Strings are immutable: every += allocates a new string ─────────────
    private static void StringConcatCost()
    {
        Console.WriteLine("-- 4. Building a 1000-char string --");

        long before = GC.GetAllocatedBytesForCurrentThread();
        _sink = ConcatInLoop(1000);
        long concat = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        _sink = BuilderInLoop(1000);
        long builder = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"s += \"x\" in a loop : {concat,8} bytes");
        Console.WriteLine($"StringBuilder      : {builder,8} bytes");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ConcatInLoop(int n)
    {
        string s = "";
        for (int i = 0; i < n; i++) s += "x";
        return s;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string BuilderInLoop(int n)
    {
        var sb = new System.Text.StringBuilder(n);
        for (int i = 0; i < n; i++) sb.Append('x');
        return sb.ToString();
    }

    // ── 5. stackalloc: scratch memory with zero GC involvement ────────────────
    private static void StackAllocation()
    {
        Console.WriteLine("-- 5. stackalloc vs new int[] --");

        long before = GC.GetAllocatedBytesForCurrentThread();
        long a = SumOnStack();
        long onStack = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        long b = SumOnHeap();
        long onHeap = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"stackalloc int[256] : {onStack,5} heap bytes (sum {a})");
        Console.WriteLine($"new int[256]        : {onHeap,5} heap bytes (sum {b})");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long SumOnStack()
    {
        Span<int> buffer = stackalloc int[256];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = i;
        long sum = 0;
        foreach (var v in buffer) sum += v;
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long SumOnHeap()
    {
        var buffer = new int[256];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = i;
        long sum = 0;
        foreach (var v in buffer) sum += v;
        return sum;
    }

    // ── 6. Escape analysis: `new` does not always mean "heap" ─────────────────
    // AggressiveOptimization = compile fully optimised on the first call (skips tier-0),
    // because escape analysis only runs in optimised code.
    private static void EscapeAnalysis()
    {
        Console.WriteLine("-- 6. Does `new` always hit the heap? --");

        NonEscaping(1);   // first call compiles the method
        long before = GC.GetAllocatedBytesForCurrentThread();
        int r = NonEscaping(2);
        long nonEscaping = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        Escaping(2);
        long escaping = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"object used only inside the method : {nonEscaping,3} heap bytes (result {r})");
        Console.WriteLine($"object stored in a static field    : {escaping,3} heap bytes");
        Console.WriteLine("(first number depends on the runtime version - it can be 0 or 24)");
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int NonEscaping(int x)
    {
        var p = new PointClass { X = x };
        return p.X + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Escaping(int x)
    {
        _sink = new PointClass { X = x };
    }
}
