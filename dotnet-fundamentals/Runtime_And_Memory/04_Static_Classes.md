# 04 — Static Classes

> **Core idea:** A static class is not a special kind of object. It is a class that **cannot have instances**, so everything in it belongs to the *type*. Static *state* lives for the life of the process, is shared by every caller, and is a GC root.

Demo: [04_Static_Classes.cs](./04_Static_Classes.cs)

---

## The `static` keyword, mapped

`static` means "belongs to the type, not to an instance" everywhere. It shows up in several places:

| Where | Meaning |
|---|---|
| `static class Foo` | Container for static members only. Cannot be instantiated or inherited |
| `static` member in a normal class | One copy shared by all instances (`static int _count`) |
| `static Foo() { }` | **Static constructor**: runs once to initialise the type |
| `static readonly` field | Set once at type initialisation, then never reassigned |
| `static` local function / lambda | Cannot capture locals, so it cannot accidentally allocate a closure |
| `static abstract` interface member (C# 11) | Contract on the *type* (generic math, `INumber<T>`) |
| `using static System.Math;` | Import static members so you can call `Sqrt(...)` unqualified |

---

## What the compiler enforces

A static class compiles to a class that is both **abstract and sealed**. Demo 1 confirms it:

```
IsAbstract = True, IsSealed = True
Instance constructors: 0
```

Real compiler errors, produced while preparing these notes (.NET SDK 10.0.401):

| You try | Error |
|---|---|
| `static class A : IFoo` | CS0714: static classes cannot implement interfaces |
| `static class A : Base` | CS0713: static classes must derive from object |
| `class B : A` (A is static) | CS0709: cannot derive from static class |
| An instance field or method in a static class | CS0708: cannot declare instance members in a static class |
| An instance constructor in a static class | CS0710: static classes cannot have instance constructors |
| `new A()` | CS0712: cannot create an instance of the static class |
| `A v = null;` | CS0723: cannot declare a variable of static type |
| `List<A>` | CS0718: static types cannot be used as type arguments |

**What follows:** a static class has no `this`, so it cannot implement an interface, cannot be passed around, cannot be substituted in a test, and cannot be registered in DI as a service. That is where most of its downsides come from.

> Extension methods must live in a non-generic, top-level static class. That is a legitimate and common use of one. Compiler-checked: a nested static class gives CS1109, a generic static class or a non-static class gives CS1106. (C# 14 adds `extension` blocks as a newer syntax for the same idea; I did not test that here.)

---

## Initialisation: when does static state get set up?

Two forms, with **different guarantees**:

```csharp
class A { static A() { ... } }                   // explicit static constructor  -> "precise"
class B { static readonly string X = Init(); }   // field initialiser only       -> "beforefieldinit"
```

| | Explicit static constructor | Field initialiser only |
|---|---|---|
| When | Just before the first instance is created or first static member is accessed | **Any time** before the first access to a static *field* (the runtime chooses) |
| Calling a static *method* that touches no static field | Triggers it | May not trigger it |

Demo 2, explicit constructor:

```
  before first use
  [cctor] WithStaticCtor initialised
  after Touch()   <- cctor ran just before Touch() executed
  after 2nd Touch() <- no second [cctor] line
```

Demo 3, field initialiser only. `Touch()` (no static field access) did not trigger it. Reading the field did:

```
  Touch() returned
  ReadTheField() entered
  [initialiser] WithFieldInitialiser initialised
```

**In this run it was lazy, but the language only promises "at or before first field access".** A different JIT, tier or runtime version may run it earlier. Never rely on initialisation order between types, and never put side effects (I/O, logging, registering things) in a static initialiser.

### Guarantees you can rely on

| Guarantee | Evidence |
|---|---|
| **Runs exactly once**, thread-safely | Demo 4: 8 threads raced to touch the type, the constructor ran **1** time |
| **A failure is permanent** for the process | Demo 5: below |

Demo 5, an initialiser that throws:

```
attempt 1: TypeInitializationException -> inner: config file missing
attempt 2: TypeInitializationException -> inner: config file missing
initialiser body executed 1 time(s) across 2 accesses -> the failure is cached and re-thrown
```

The initialiser ran once. The second access did not retry. It re-threw the cached failure. **Every later use of that type fails for the life of the process.** The real cause hides in `InnerException`.

Because the runtime holds a lock during type initialisation, avoid blocking calls, waiting on other threads, or cyclic dependencies between static initialisers. They can deadlock.

---

## Where static data lives, and for how long

- Static fields belong to the **type** (and, for generics, to each **closed type**). They are created at type initialisation and live until the process ends. The exception is types loaded into a collectible `AssemblyLoadContext`, which is advanced and rare.
- For a `static` field holding a reference type: the **reference** sits in the type's static storage, and the **object** lives on the managed heap.
- Static fields are **GC roots** ([03](./03_Garbage_Collection.md)). Whatever they reference cannot be collected.

Demo 8:

```
no local variable references it, static list does : alive = True
after clearing the static list                     : alive = False
```

**Practical meaning:** a `static Dictionary<,>` used as a cache with no eviction is a memory leak by design. So is a static event with subscribers that never unsubscribe.

### Generic statics are per closed type (demo 6)

```
Counter<int>.Hits = 3, Counter<string>.Hits = 1   (separate fields)
```

`Counter<int>` and `Counter<string>` are different types and each has its own statics. This surprises people who expect "one static per class definition".

### `[ThreadStatic]`: one copy per thread (demo 7)

```csharp
[ThreadStatic] public static int Value = 5;
```
```
thread 2 (first to touch it): Value = 5
a second thread sees         : Value = 0
```

The `= 5` initialiser ran **only for the first thread** to touch the type. Every other thread starts at `default`. For per-thread state with real initialisation use `ThreadLocal<T>`, and for state that must flow with `async`/`await` use `AsyncLocal<T>`.

---

## Static means shared by everyone, simultaneously

In a web app every request runs on its own thread-pool thread in the **same process**, so every request reads and writes the **same static fields**. Demo (in [05](./05_When_To_Use_Static.md)): 8 CPUs, 1,000,000 increments of a plain static `int`:

```
plain static ++ : 344910  (lost 655090)
Interlocked     : 1000000
```

Static *mutable* state needs synchronisation (`Interlocked`, `lock`, concurrent collections), or it should not exist.

---

## Is a static method faster?

A static call skips passing `this`, but the difference is negligible in real programs. Do not choose `static` for speed. The reasons to choose it are design reasons, covered in [05](./05_When_To_Use_Static.md).

---

## Self-check

1. Why can a static class not implement an interface? Which error code do you get?
2. An explicit static constructor and a field initialiser both exist in different classes. Which one gives you a precise "runs right before first use" guarantee?
3. A static initialiser throws once on startup. What happens on the next 1000 calls that touch the type?
4. `Counter<int>.Hits` is 3. What is `Counter<long>.Hits`?
5. You add items to a `static List<T>` and never remove them. Why is the GC unable to help?
6. A `[ThreadStatic]` field has an initialiser. Why do some threads see `0`?
