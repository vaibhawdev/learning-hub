# Learning Hub

Personal notes, code samples and POCs for learning software engineering in depth. Each top-level folder is one subject area, so new topics have an obvious place to go.

## Layout

| Folder | What's inside | Entry point |
|--------|---------------|-------------|
| [`dotnet-fundamentals/`](./dotnet-fundamentals/) | C# code and notes: OOP, SOLID, Dependency Injection, runtime and memory (JIT, stack/heap, GC, static classes) | [Runtime & Memory](./dotnet-fundamentals/Runtime_And_Memory/README.md), [DI README](./dotnet-fundamentals/DependencyInjection/README.md) |
| [`system-design/`](./system-design/) | Phased roadmap, from core CS to .NET internals, databases, networking, cloud, containers | [Phase 01](./system-design/Phase-01-Core-CS-Foundations/README.md) |
| [`distributed-systems/`](./distributed-systems/) | Reference guide: CAP, sharding, caching, messaging, Kubernetes, observability, DR | [README](./distributed-systems/README.md) |
| [`ai/`](./ai/) | Placeholder: LLMs, agents, RAG, MCP | |
| [`poc/`](./poc/) | Placeholder: small runnable proofs of concept | |

## system-design phases

| Phase | Topic |
|-------|-------|
| [01](./system-design/Phase-01-Core-CS-Foundations/) | Core CS foundations |
| [02](./system-design/Phase-02-Deep-DotNet-Internals/) | Deep .NET internals |
| [03](./system-design/Phase-03-Database-Engineering/) | Database engineering |
| [04](./system-design/Phase-04-Distributed-Systems/) | Distributed systems |
| [05](./system-design/Phase-05-Networking-And-Web/) | Networking and web |
| [06](./system-design/Phase-06-Cloud-And-Azure/) | Cloud and Azure |
| [07](./system-design/Phase-07-Containers-And-Kubernetes/) | Containers and Kubernetes |

Some notes link forward to phases that are not written yet (Phases 08, 09, 10, 13 and 15). Those links stay broken until the phases exist. Every other relative link in the repo resolves.

## Overlap to be aware of

`system-design/Phase-04-Distributed-Systems` and `Phase-07-Containers-And-Kubernetes` cover ground that `distributed-systems/` also covers, at different depth. They are kept separate on purpose. Read the phase notes for the learning path, and the `distributed-systems/` guide as the reference.

## Adding a new topic

1. Pick the folder that fits, or create a new top-level folder for a new subject (for example `ai/`, `security/`).
2. Give the folder a `README.md` with an index.
3. Add a row to the Layout table above.
4. Keep code samples runnable and put build output in `.gitignore`, not in the repo.
