# Tech Lead / SSE Playbook for Production Incidents

Debugging is half of an incident. The other half is **coordination, communication, risk control and follow-through**. This is where leads add most value, and where most incidents actually get worse. Pair with [01-debugging-process.md](./01-debugging-process.md).

---

## 1. Your Job Changes During an Incident

| If you are... | Your job | Not your job |
|---------------|----------|--------------|
| **Incident Commander (IC)** | Keep the whole picture, assign threads, make decisions, own timeline, call escalations | Typing commands / deep-diving code |
| **Investigator (SSE)** | Test hypotheses, report evidence in the channel, propose (not unilaterally apply) risky changes | Messaging stakeholders |
| **Communications lead** | Status updates on a fixed cadence to stakeholders/customers | Debugging |
| **Scribe** | Timestamped log of facts, decisions, actions | Opinions |

- On small teams one person wears several hats. **If you are the senior person and also debugging, hand off the IC role explicitly**: "I'm diving into the DB; Priya is IC." An IC who debugs stops commanding.
- The IC role is assigned, not assumed. Say it out loud in the channel: "I'm IC."

---

## 2. Severity and Escalation

Define this **before** an incident. Example matrix (adapt to your org):

| Sev | Definition | Response | Update cadence |
|-----|-----------|----------|----------------|
| **SEV1** | Core flow down or data loss/corruption/security breach; most users affected | Page everyone needed now; IC + comms; exec notified | Every 15-30 min |
| **SEV2** | Major degradation; significant subset affected; workaround may exist | Page on-call + owning team; IC | Every 30-60 min |
| **SEV3** | Minor degradation, limited users | Work in business hours / on-call | At milestones |
| **SEV4** | Cosmetic / no user impact | Backlog | n/a |

**Escalate early.** Escalation is not an admission of failure. Rules:
- Unknown cause after **15 min** on SEV1 means bring in the next expert or the dependency owner.
- Any suspicion of **data loss, data corruption, or a security issue** means SEV1 and notify security/legal/DPO per policy.
- Dependency owned by another team/vendor: page them with evidence (graphs, trace IDs, timestamps), not "it seems slow".
- **Over-declare, then downgrade.** Under-declaring costs more.

---

## 3. Communication

### 3.1 Principles
- **One channel** (e.g., `#inc-2026-10-18-checkout`) as source of truth. Side conversations go there too.
- **Facts vs. hypotheses**, labelled: "FACT: p99 on payments 4s since 14:02. HYPOTHESIS: DB pool exhaustion."
- Post **what you're about to do** before doing it, then the result. This prevents two people changing production at once.
- Don't speculate publicly about root cause or blame in customer/exec comms.

### 3.2 Status update template (internal/stakeholder)
```
[SEV2] Checkout intermittently failing (since 14:02 UTC)
Impact:   ~8% of checkout attempts return error; EU users mostly
Status:   Mitigating. Rolled back release 4.12 at 14:31; error rate falling
Cause:    Under investigation (suspect: new inventory call without timeout)
Next:     Confirm recovery at 14:50; root-cause analysis after stable
Next update: 15:00 UTC
IC: <name>   Channel: #inc-...
```

### 3.3 Customer-facing rules
- State **impact and status**, not internal cause.
- Don't promise an ETA you can't back. Give the **next update time** instead.
- Close the loop when resolved; follow up after the postmortem if customers were materially affected.

### 3.4 Handling pressure
Stakeholders asking "ETA?" every five minutes is normal. Buffer it: the comms lead answers, investigators aren't interrupted. Give a fixed cadence ("next update at :30") and hold to it.

---

## 4. Decision-Making Under Uncertainty

Use these when the data is incomplete (it always is).

**Reversibility test.** Prefer reversible actions (rollback, flag off, scale up, traffic shift). Irreversible ones (data deletes, schema drops, manual DB edits, replaying queues) need a second person and a written rollback/verification plan.

**Cost-of-waiting test.** Is damage accumulating (data corruption, money, customers) or stable? If accumulating, bias to fast mitigation; if stable, bias to gathering evidence.

**Mitigate vs. diagnose.** If a safe mitigation exists (rollback), do it, then diagnose with the pressure off. Exception: if mitigation destroys unique evidence, capture the evidence first (dumps, logs, `kubectl logs --previous`, DB snapshots).

**Time-box hypotheses.** "We give this theory 10 minutes." Avoid tunnel vision. Assign a person to argue the opposite case ("what if it's not the DB?").

**Avoid common cognitive traps:**
| Trap | Counter |
|------|---------|
| Anchoring on the first theory | Keep 2-3 live hypotheses |
| Confirmation bias (only look for supporting data) | Write the prediction that would *disprove* the theory |
| Recency bias ("it must be the last deploy") | Check, but also verify it matches timing exactly. Correlation is not causation |
| Streetlight effect (look where the dashboards are) | Ask what you *can't* see; check missing telemetry |
| Hero mode | Pull in help; sleep-deprived fixes break things |
| Sunk cost | If a line of investigation hasn't paid in the time box, switch |

---

## 5. Production Access and Safe Debugging

### 5.1 Principles
1. **Read-only first.** Observe before touching.
2. **Least privilege & audit:** use break-glass access that is logged; no shared credentials.
3. **Never test a hypothesis by changing production on a whim.** Reversible, announced, single-change.
4. **Protect data:** logs and dumps contain PII/secrets. Heap dumps can contain tokens/passwords. Handle per policy, store securely, delete after.
5. **Two-person rule** for destructive operations (DELETE/UPDATE on prod data, queue purge, failover, index drops).

### 5.2 Safe techniques in production
| Goal | Safe technique |
|------|----------------|
| See more detail | **Dynamic log level** raise for one service/request (with trace-ID or user filter), time-limited |
| Reproduce on real traffic | **Traffic mirroring / shadow traffic** to a sandbox instance; **replay** captured requests against staging |
| Isolate a bad node | Cordon/drain; remove from LB; keep it for forensic analysis |
| Test a fix | **Canary** a single instance/1% traffic with the fix |
| Run an ad-hoc query | Use a **read replica**, `SET TRANSACTION READ ONLY`, row limits, statement timeouts. Never a long query on the primary at peak |
| Inspect a live process | Sampling profiler / ephemeral debug container; avoid attaching a debugger that pauses threads |
| Check if a theory holds | Look in **historical** data (metrics/traces) before running anything new |

### 5.3 Things that look harmless but are dangerous
- Attaching a debugger or taking a **full heap dump** on a struggling node (pauses the process; may trigger failover or OOM)
- `SELECT *` on a big table, or a missing `WHERE`, or running an `EXPLAIN ANALYZE` on a destructive statement (it executes it)
- Restarting all instances at once (cold caches plus thundering herd)
- **Scaling out** against a saturated shared dependency
- "Quick fix" hot-patching directly on a host (config drift, lost at next deploy)
- Purging a queue to "clear the backlog" (you delete business data)
- Replaying a DLQ without idempotency verification (duplicate side effects)

---

## 6. Change Control Around Incidents

- **Freeze** unrelated deploys while a SEV1/2 is open.
- Every mitigation/fix goes through the **smallest possible, reviewed** change, even if the review is a 2-minute live pairing in the incident channel.
- **Record every production change** in the incident timeline (who, what, when, how to revert).
- After resolution: **backport** any manual fix into code/config/IaC; otherwise the next deploy reverts it.
- **Roll forward vs. roll back:** roll back by default. Roll forward only if rollback is impossible (irreversible migration, data already transformed) and the fix is tiny and well understood.
- **Database migrations are the hard case:** expand/contract (additive first, remove later) means code rollback stays safe. A destructive migration shipped with the code makes rollback impossible.

---

## 7. Reproduction and Root Cause Techniques (Beyond "Look at Logs")

| Technique | Use when |
|-----------|----------|
| **Bisect changes** | A regression exists; binary-search commits/deploys/config versions (canary each) |
| **Differential debugging** | Compare healthy vs. unhealthy instance/request/region: config, version, data, headers, node |
| **Timeline reconstruction** | Merge deploys, flags, config, alerts, traffic, dependency events on one time axis. Find what happened *first* |
| **Fault injection / chaos** | Heisenbug or "only in prod": inject latency/errors in staging to reproduce the failure mode |
| **Load test with realistic shape** | Saturation/tail issues; use **open-loop** load, production-like data size, and cache state |
| **Record & replay** | Data-dependent bugs; replay the exact failing request/message in isolation |
| **Binary search the call graph** | Disable/stub dependencies (via flags) until the symptom disappears |
| **Deterministic simulation / model checking (TLA+, Jepsen-style)** | Consensus, replication, ordering bugs that no one can reproduce |
| **Add instrumentation, then wait** | Can't reproduce; ship targeted logs/metrics/traces now to catch it next time. This is a valid outcome, not a failure |
| **Five Whys, but systemic** | Post-fix; keep asking until you reach a missing control (no timeout, no alert), not a person |

**Evidence log template** (keep in the incident doc):
```
Time (UTC) | Observation (fact)             | Source (link/query)   | Hypothesis affected
14:02      | payments p99 0.4s -> 4s        | Grafana panel X       | H1 (DB), H3 (dependency)
14:05      | DB CPU flat, pool wait rising   | Dashboard Y           | supports H2 (pool exhaustion)
```

---

## 8. Reading the System Like a Lead

Questions to ask in the first 10 minutes. They are often more useful than typing commands:

1. What changed? (deploys, flags, config, schema, infra, traffic, partner, certificates, quotas, DST/dates)
2. When exactly did it start, and is it still getting worse, flat, or recovering?
3. Who/what is affected, and who/what is *not* affected? (The negative space narrows the search faster.)
4. What does the dependency graph look like from the failing endpoint?
5. What is the **cheapest safe mitigation** and what would make it unsafe?
6. Do we have the telemetry to answer our top hypothesis? If not, how do we get it fast?
7. What's the worst this could be (data loss/security)? Have we ruled that out?
8. Is anyone else already changing something?

---

## 9. After the Incident: Quality of Follow-Through

### 9.1 Blameless postmortem checklist
- [ ] Written within ~5 business days; reviewed by people *outside* the incident
- [ ] **Timeline** with detection time, time-to-acknowledge, time-to-mitigate, time-to-resolve
- [ ] **Impact** quantified (users, requests, revenue, SLO budget burned, data affected)
- [ ] **Root cause(s) + contributing factors** (a single root cause is a red flag)
- [ ] **What went well** (to preserve) and **where we got lucky** (to fix)
- [ ] **Why wasn't this caught earlier?** (tests, review, canary, alerting, load testing)
- [ ] **Action items:** owner, due date, priority, tracked in the backlog, classified as **detect / mitigate / prevent**
- [ ] No action item of the form "be more careful" or "add more review": fix the system, not the people

### 9.2 Action item quality
| Weak | Strong |
|------|--------|
| "Improve monitoring" | "Add alert: p99 > 800ms for 5 min on `/checkout`, route to on-call, runbook linked, by 11/01 (owner: A)" |
| "Be careful with config" | "Validate config schema in CI; roll config via canary with auto-rollback on 5xx > 2%" |
| "Add timeouts" | "Set connect 200ms / read 1s on inventory client; add circuit breaker; add test that fails without timeout" |

### 9.3 Error budget policy
If SLO burn exceeds the budget, **reliability work gets priority over features** until restored. This only works if agreed with product/management *before* the incident.

### 9.4 Close the loop
- Review action items in a recurring meeting until done; unfinished incident follow-ups are the leading indicator of repeat incidents.
- Update runbooks, dashboards, and alerts that failed you.
- Share a short summary org-wide; repeat patterns across teams are how you find systemic problems.
- Track incident metrics over time: **MTTD, MTTA, MTTR, incident count by cause, repeat-incident rate, % of incidents detected by alerts vs. customers**.

---

## 10. On-Call Health and Team Practice

- **Handoffs:** end-of-shift note: open issues, flaky alerts, risky deploys, what to watch.
- **Follow-the-sun or rotations** that avoid chronic overnight burden; compensate and rest after a major incident.
- **Shadowing / reverse shadowing:** new on-call engineers shadow first, then lead with a senior backing.
- **Runbooks must be tested** (game days). An untested runbook is fiction.
- **Alert quality reviews:** every page should be actionable and urgent. Delete or demote the rest. Track pages per shift.
- **Game days / failure drills:** kill a dependency, expire a cert in staging, fail over a DB, then time the response and fix gaps.
- **Mentoring juniors in incidents:** have them narrate their reasoning, assign them evidence-gathering threads, and debrief afterwards. Incidents are the best training you have.
- **Prevent the lone hero:** if one person always fixes it, you have a knowledge SPOF (document, rotate, pair).

---

## 11. Design Review Questions That Prevent Incidents (Lead Checklist)

Ask of every new service/feature before it ships:

- What happens when each dependency is **slow**? **Down**? **Returns garbage**?
- Where are the **timeouts, retries (with backoff/jitter/budget), circuit breakers, bulkheads**?
- Is each operation **idempotent**? What if the client retries after a timeout?
- What is the **rollback plan**, including data/schema? Is the migration expand/contract?
- How does it behave under **2x and 10x load**? What is shed first?
- What's the **SLO**, and which **alerts** and **dashboards** prove it? Is there a runbook?
- Can we **trace one request** end-to-end (IDs propagated through async hops)?
- What are the **hot keys / hot partitions** and unbounded queues/caches?
- What are the **cold-start and full-outage recovery** behaviours (dependency ordering, thundering herd)?
- Who **owns** it, and who is on call?
- Are **certs/secrets/tokens** rotated automatically and monitored for expiry?
- What's the **blast radius** of a bad deploy? Is it canaried and feature-flagged?
