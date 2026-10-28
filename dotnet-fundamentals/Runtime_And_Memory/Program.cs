// ============================================================
// Entry point for the Runtime_And_Memory demos.
//   dotnet run -c Release -- 1      runtime basics
//   dotnet run -c Release -- 2      memory allocation
//   dotnet run -c Release -- 3      garbage collection
//   dotnet run -c Release -- 4      static class behaviour
//   dotnet run -c Release -- 5      when to use static
//   dotnet run -c Release -- all
// ============================================================

using RuntimeMemory.Concepts.GarbageCollection;
using RuntimeMemory.Concepts.MemoryAllocation;
using RuntimeMemory.Concepts.RuntimeBasics;
using RuntimeMemory.Concepts.StaticBehaviour;
using RuntimeMemory.Concepts.WhenToUseStatic;

var demos = new (string Key, string Title, Action Run)[]
{
    ("1", "Runtime basics",        RuntimeBasicsDemo.Run),
    ("2", "Memory allocation",     MemoryAllocationDemo.Run),
    ("3", "Garbage collection",    GarbageCollectionDemo.Run),
    ("4", "Static class behaviour", StaticBehaviourDemo.Run),
    ("5", "When to use static",    WhenToUseStaticDemo.Run),
};

var choice = args.Length > 0 ? args[0] : "";
var selected = choice == "all" ? demos : demos.Where(d => d.Key == choice).ToArray();

if (selected.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -c Release -- <1|2|3|4|5|all>");
    foreach (var d in demos) Console.WriteLine($"  {d.Key}  {d.Title}");
    return;
}

foreach (var d in selected)
{
    Console.WriteLine();
    Console.WriteLine($"==================== {d.Key}. {d.Title} ====================");
    d.Run();
}
