# Debugging Guide: Production Issues in Distributed Systems

For senior engineers (SSE) and tech leads who own production systems and on-call.

| # | Doc | Use it for |
|---|-----|-----------|
| 01 | [Debugging process](./01-debugging-process.md) | The 8-step method, signal funnel, symptom table, anti-patterns, incident checklist |
| 02 | [Problems in distributed systems](./02-problems-in-distributed-systems.md) | Catalogue of failure modes: what, why, how it shows up, mitigation |
| 03 | [Metrics & terminology](./03-metrics-and-terminology.md) | p50/p99, SLI/SLO, golden signals, consistency, resilience, observability glossary |
| 04 | [Tech lead playbook](./04-tech-lead-playbook.md) | IC role, severity/escalation, comms templates, safe prod access, change control, postmortems, on-call health, design-review questions |
| 05 | [Triage cheat sheets](./05-triage-cheatsheets.md) | HTTP 5xx meanings, timeouts, TCP, DB, runtime, queues, Kubernetes, "what changed" checklist |

**During an incident:** 01 (process) → 05 (lookup) → 04 §2-5 (roles, comms, safety).
**After:** 04 §9. **Before (design/review):** 04 §11, 02.
Scenario-level commands live in [../22-debugging-distributed-systems.md](../22-debugging-distributed-systems.md).
