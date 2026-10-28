# Disaster Recovery & High Availability

> **License quick-ref** — Velero (K8s backup): Apache-2. Chaos Monkey/Chaos Toolkit: Apache-2. Litmus Chaos: Apache-2. Gremlin: commercial SaaS. AWS Backup / Azure Backup / GCP Backup: managed SaaS (pay-per-use). Patroni (PostgreSQL HA): MIT. pgBackRest: MIT.

---

## Key Metrics: RTO and RPO

Every DR plan must start with agreeing on these two numbers with stakeholders:

| Metric | Definition | Example |
|--------|-----------|---------|
| **RTO** (Recovery Time Objective) | Maximum acceptable downtime after a disaster | 4 hours — service must be restored within 4 hours |
| **RPO** (Recovery Point Objective) | Maximum acceptable data loss | 1 hour — we can afford to lose up to 1 hour of data |

These numbers drive all architectural decisions:

| RTO Target | Architecture Required |
|-----------|----------------------|
| Hours | Single-region with backups |
| Minutes | Active-passive multi-AZ |
| Seconds | Active-active multi-region |
| Near-zero | Active-active with global load balancing |

**Lower RTO/RPO = higher cost**. The business must make this trade-off explicitly.

---

## High Availability Patterns

### Single AZ (No HA)

Single datacenter/availability zone. One failure domain.

- **RPO**: Hours (backup restore time)
- **RTO**: Hours to days
- **Use for**: Development, cost-sensitive non-critical services

### Multi-AZ Active-Passive

Primary runs in AZ1, standby in AZ2. On failure, failover to standby.

```
AZ1: Primary DB + App     ← serves all traffic
AZ2: Standby DB (synced)  ← idle, ready for failover
```

- **RPO**: Near zero (synchronous replication) to minutes (async)
- **RTO**: Minutes (automatic) to 1 hour (manual)
- **Used by**: AWS RDS Multi-AZ, PostgreSQL Patroni

### Multi-AZ Active-Active

Traffic distributed across multiple AZs simultaneously. Any AZ can fail without downtime.

```
AZ1: App + DB replica     ← serves 50% of traffic
AZ2: App + DB replica     ← serves 50% of traffic
Load Balancer routes to both; either can handle all traffic
```

- **RPO**: Near zero
- **RTO**: Near zero (automatic rerouting)
- **Used by**: Kubernetes with multi-AZ node pools, Cassandra, DynamoDB

### Multi-Region Active-Passive

One region serves all traffic; another is a hot standby.

```
US-East: Active (all traffic)
US-West: Passive (ready to serve, data replicated)

DNS failover: Route 53 health check → if US-East fails, point to US-West
```

- **RPO**: Minutes (cross-region replication lag)
- **RTO**: Minutes to 30 minutes (DNS propagation + warmup)

### Multi-Region Active-Active

Traffic served from multiple regions simultaneously. Users routed to nearest region.

```
US-East: 40% of traffic    EU-West: 35% of traffic    AP-Southeast: 25%
All regions accept writes; conflict resolution required (CRDTs or last-write-wins)
```

- **RPO**: Near zero
- **RTO**: Near zero
- **Cost**: Very high (data transfer, replication complexity)
- **Used by**: Global companies (Google, Netflix), databases: Cassandra, CockroachDB, Spanner, DynamoDB Global Tables

---

## Database Backup Strategies

### PostgreSQL

```bash
# pgBackRest: enterprise-grade backup for PostgreSQL
# Continuous WAL archiving (near-zero RPO)
[global]
repo1-type=s3
repo1-s3-bucket=mycompany-pg-backups
repo1-s3-region=us-east-1

[mydb]
pg1-path=/var/lib/postgresql/data
pg1-user=postgres

# Full backup
pgbackrest --stanza=mydb backup --type=full

# Incremental backup
pgbackrest --stanza=mydb backup --type=incr

# Point-in-time recovery
pgbackrest --stanza=mydb --delta restore \
  --target="2026-06-06 14:00:00" \
  --target-action=promote
```

```bash
# pg_dump for logical backups (small DBs, cross-version migration)
pg_dump -Fc mydb > backup.dump           # compressed
pg_restore -d newdb backup.dump          # restore
```

### MongoDB

```bash
# mongodump / mongorestore
mongodump --uri="mongodb+srv://..." --archive=backup.gz --gzip
mongorestore --archive=backup.gz --gzip --drop

# MongoDB Atlas: automated point-in-time recovery up to 1-second precision
# Oplog-based PITR built into Atlas clusters
```

### Redis

```bash
# RDB snapshot (point-in-time)
redis-cli SAVE        # synchronous
redis-cli BGSAVE      # background

# AOF (Append Only File) — near-zero RPO
# redis.conf:
appendonly yes
appendfsync everysec  # fsync every second (balance of perf vs durability)

# Backup: copy /var/lib/redis/dump.rdb and appendonly.aof to S3
```

---

## Kubernetes Backup with Velero

Velero backs up Kubernetes resources and persistent volume data.

> **License**: Apache-2.0 — fully open-source (VMware/Broadcom).

```bash
# Install
velero install \
  --provider aws \
  --plugins velero/velero-plugin-for-aws:v1.9.0 \
  --bucket my-velero-backups \
  --backup-location-config region=us-east-1 \
  --snapshot-location-config region=us-east-1

# Backup entire cluster
velero backup create full-backup --include-namespaces='*'

# Backup specific namespace
velero backup create production-backup --include-namespaces production

# Restore
velero restore create --from-backup production-backup

# Schedule daily backups at 2am UTC
velero schedule create daily --schedule="0 2 * * *" \
  --include-namespaces production \
  --ttl 720h     # keep for 30 days
```

### What Velero Backs Up

- All Kubernetes resource definitions (Deployments, Services, ConfigMaps, Secrets, etc.)
- PersistentVolume snapshots (via cloud provider snapshot API)
- **Does NOT backup**: database data (use DB-native backup for that)

---

## Chaos Engineering

Testing DR by deliberately breaking things in production (or production-like environments) before a real failure does it unexpectedly.

> **"Hope is not a strategy. Test your assumptions about failure."**

### Principles (Chaos Monkey / Netflix)

1. Build a hypothesis: "If Pod X crashes, traffic routes to Pod Y without user impact"
2. Run the experiment in production or a production-replica environment
3. Measure the blast radius
4. Fix weaknesses discovered
5. Automate the experiments

### Tools

| Tool | License | What It Does |
|------|---------|-------------|
| **Chaos Monkey** | Apache-2 | Randomly terminates EC2 instances (Netflix) |
| **Chaos Toolkit** | Apache-2 | General-purpose, plugin-based chaos framework |
| **Litmus Chaos** | Apache-2 | Kubernetes-native, CNCF project |
| **Chaos Mesh** | Apache-2 | Kubernetes chaos via CRDs, rich fault types |
| **Gremlin** | Commercial | Managed chaos platform, enterprise features |
| **AWS Fault Injection Simulator** | AWS managed | Native AWS chaos experiments |

### Common Experiments

```yaml
# Chaos Mesh: kill 50% of payment-service pods
apiVersion: chaos-mesh.org/v1alpha1
kind: PodChaos
metadata:
  name: payment-pod-kill
spec:
  action: pod-kill
  mode: FixedPercent
  value: "50"
  selector:
    namespaces: [production]
    labelSelectors:
      app: payment-service
  scheduler:
    cron: "@every 1h"    # run every hour in staging

---
# Network partition: add 200ms latency to payment-service calls
apiVersion: chaos-mesh.org/v1alpha1
kind: NetworkChaos
metadata:
  name: network-delay
spec:
  action: delay
  mode: all
  selector:
    labelSelectors:
      app: payment-service
  delay:
    latency: "200ms"
    correlation: "100"
    jitter: "50ms"

---
# Stress: CPU spike on inventory service
apiVersion: chaos-mesh.org/v1alpha1
kind: StressChaos
spec:
  mode: all
  selector:
    labelSelectors:
      app: inventory-service
  stressors:
    cpu:
      workers: 4
      load: 80   # 80% CPU
  duration: "5m"
```

### Experiments to Run Before Production

| Experiment | What It Tests |
|-----------|--------------|
| Kill one pod | Readiness + replica failover |
| Kill all pods of a service | Dependency resilience (circuit breaker) |
| Kill a node | Pod rescheduling speed |
| Add 500ms latency between services | Timeout configuration correctness |
| Fill disk on a database node | Disk pressure handling |
| Kill the primary database | Failover time and correctness |
| Exhaust connection pool | Connection pool behavior under pressure |
| Kill Kafka brokers | Consumer rebalancing behavior |

---

## Multi-Region Data Replication

### PostgreSQL Cross-Region

```
Primary (US-East)
  └─ Streaming replication → Replica (US-West) [read-only]
  └─ WAL archiving via pgBackRest → S3 (us-west-2)
```

Promote replica to primary on region failure. Update DNS. RPO = replication lag (usually <1s).

### CockroachDB (Multi-Region Native)

```sql
-- Set primary region
ALTER DATABASE mydb SET PRIMARY REGION "us-east1";
ALTER DATABASE mydb ADD REGION "europe-west1";
ALTER DATABASE mydb ADD REGION "asia-northeast1";

-- Table: data pinned to its "home" region
ALTER TABLE users SET LOCALITY REGIONAL BY ROW;
-- Each row has a hidden 'crdb_region' column; writes go to user's home region
```

### Global CDN for Static Assets

Static assets (images, JS, CSS) replicated via CDN (CloudFront, Fastly, Cloudflare). No application work required — CDN handles replication automatically across edge nodes.

---

## DNS Failover

```
Route 53 health check:
Primary: api.example.com → us-east-1 LB (TTL: 60s)
  Health check: GET /health every 10s
  → If 3 consecutive failures: failover to us-west-2 LB

failover record: api.example.com → us-west-2 LB (only active when primary fails)
```

**TTL considerations**: Low TTL (30–60s) means faster failover but more DNS queries. High TTL (300s+) means clients cache the failing address longer during a failover.

---

## Recovery Testing

**Untested backups are not backups.** Run recovery tests on a schedule:

```yaml
# Automated recovery test (CronJob)
apiVersion: batch/v1
kind: CronJob
metadata:
  name: backup-recovery-test
spec:
  schedule: "0 3 * * 0"  # weekly Sunday 3am
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: test
            image: myapp-db-tools
            command:
            - /scripts/test-restore.sh
            # Script: restore last backup to a test DB, run integrity checks
```

### Runbook Template

Every production system must have a runbook for:
- Primary database failure → promotion procedure, step by step
- Region failure → DNS failover steps, verification steps
- Full cluster loss → how to recreate from scratch using Velero + DB backups
- Compromised credentials → rotation procedure

---

## Production Checklist

- [ ] RTO and RPO documented and agreed with stakeholders
- [ ] Automated database backups with retention policy
- [ ] Backup restoration tested on a schedule (not just "backup exists")
- [ ] Multi-AZ deployment for all production workloads
- [ ] PodDisruptionBudgets on all critical deployments
- [ ] Database HA configured (RDS Multi-AZ, Patroni, Atlas, etc.)
- [ ] Kubernetes cluster backup via Velero (namespaces + PV snapshots)
- [ ] DNS failover configured with appropriate TTL
- [ ] Chaos experiments run in staging for every major service
- [ ] Runbooks for each failure scenario, tested with game days
- [ ] On-call rotation with escalation paths defined
- [ ] Post-incident review process documented

---

## Next Steps

- [Observability](./16-observability.md) — detect failures before they become disasters
- [Kubernetes](./containerization/kubernetes.md) — multi-AZ node pools, PodDisruptionBudgets
- [Design Patterns](./19-design-patterns.md) — circuit breaker and bulkhead limit disaster blast radius
- [Debugging](./22-debugging-distributed-systems.md) — what to do after an incident
