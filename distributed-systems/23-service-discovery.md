# Service Discovery & Configuration Management

> **License quick-ref** — Consul (HashiCorp): BSL 1.1 since Aug 2023 (was Apache-2; not OSI-approved). OpenTofu-compatible alternative: **Serf** (MPL-2). Etcd: Apache-2 (open-source, CNCF). Eureka (Netflix): Apache-2. Zookeeper: Apache-2. Spring Cloud Config: Apache-2. Kubernetes built-in DNS: Apache-2. Nacos (Alibaba): Apache-2.

---

## The Problem Service Discovery Solves

In a static world, you hardcode IP addresses. In a distributed system:
- Services scale up and down — IP addresses change constantly
- Pods are ephemeral — a restarted pod gets a new IP
- Blue-green deploys swap the backend completely
- Multi-region setups have different endpoints per region

Service discovery is the mechanism by which services find each other dynamically.

---

## Two Models of Service Discovery

### Client-Side Discovery

The client queries a **service registry** directly, picks an instance, and connects.

```
Client → Service Registry → Gets list of healthy instances
Client → (load balance locally) → Chosen instance
```

**Pros**: Client controls load balancing (round-robin, least-connections, zone-aware).
**Cons**: Each client must implement registry lookup + load balancing logic. Language-specific library required.

**Examples**: Netflix Eureka (client-side), Consul (with client library).

### Server-Side Discovery

The client makes a request to a **load balancer**. The LB queries the registry and routes to a healthy instance.

```
Client → Load Balancer → (queries registry) → Healthy instance
```

**Pros**: Clients are simple — no registry knowledge needed. Works for any language.
**Cons**: Load balancer is an additional hop.

**Examples**: AWS ELB + Route 53, Kubernetes Services + kube-proxy, Nginx + Consul.

---

## DNS-Based Service Discovery (Kubernetes)

Kubernetes includes a built-in DNS service (CoreDNS) that provides service discovery via DNS names.

### DNS Naming Convention

```
<service-name>.<namespace>.svc.cluster.local

Examples:
payment-service.production.svc.cluster.local  → ClusterIP of payment-service
postgres.databases.svc.cluster.local          → ClusterIP of postgres service
```

Within the same namespace, short names work:
```
http://payment-service/api   → resolves to payment-service.production.svc.cluster.local
```

### StatefulSet DNS

StatefulSet pods get stable DNS names:
```
<pod-name>.<service-name>.<namespace>.svc.cluster.local

postgres-0.postgres-headless.production.svc.cluster.local
postgres-1.postgres-headless.production.svc.cluster.local
```

Useful for cluster databases (Cassandra, Kafka, PostgreSQL replication) that need to address specific instances.

### Headless Service

A service with `clusterIP: None` does not proxy traffic. DNS returns the individual pod IPs directly:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: kafka-headless
spec:
  clusterIP: None
  selector:
    app: kafka
```

```bash
nslookup kafka-headless.production.svc.cluster.local
# Returns: 10.0.0.5, 10.0.0.6, 10.0.0.7  (all pod IPs)
```

---

## Consul

HashiCorp Consul provides service discovery + health checking + distributed key-value store + service mesh.

> **License note**: Consul changed from Apache-2 to BSL 1.1 in August 2023. BSL restricts competing SaaS products but is generally usable for self-hosted production workloads. Check your use case against the BSL terms. For a fully open-source alternative, see **OpenBao** (Vault fork) or use etcd for K/V and Kubernetes DNS for discovery.

### Architecture

```
┌──────────────────────────────────────────────┐
│  Consul Cluster (3 or 5 servers for HA)       │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│  │ Server 1 │ │ Server 2 │ │ Server 3 │      │
│  │ (leader) │ │          │ │          │      │
│  └──────────┘ └──────────┘ └──────────┘      │
└───────────────────────┬──────────────────────┘
                        │ Raft consensus
         ───────────────┼───────────────
        │               │               │
   ┌────┴────┐    ┌─────┴───┐    ┌──────┴──┐
   │ Agent   │    │ Agent   │    │ Agent   │
   │ (client)│    │ (client)│    │ (client)│
   │ Node 1  │    │ Node 2  │    │ Node 3  │
   └─────────┘    └─────────┘    └─────────┘
```

Each node runs a Consul agent. Clients register services with their local agent. Servers maintain the cluster state via Raft.

### Service Registration

```json
// service.json
{
  "service": {
    "name": "payment-service",
    "tags": ["v2", "production"],
    "port": 8080,
    "check": {
      "http": "http://localhost:8080/health",
      "interval": "10s",
      "timeout": "2s",
      "deregister_critical_service_after": "30s"
    }
  }
}
```

```bash
consul services register service.json
```

### Service Lookup (DNS)

```bash
# Consul DNS at port 8600
dig @127.0.0.1 -p 8600 payment-service.service.consul

# With tag filter
dig @127.0.0.1 -p 8600 v2.payment-service.service.consul

# Via HTTP API
curl http://localhost:8500/v1/health/service/payment-service?passing
```

### Key-Value Store

```bash
# Store config
consul kv put config/payment-service/timeout 30

# Read config
consul kv get config/payment-service/timeout

# Watch for changes (long-polling)
consul watch -type=key -key=config/payment-service/timeout /usr/bin/restart-service.sh
```

### Health Checking

```json
"checks": [
  {
    "id": "api",
    "name": "HTTP API",
    "http": "http://localhost:8080/health",
    "interval": "10s"
  },
  {
    "id": "db",
    "name": "Database connection",
    "args": ["psql", "-U", "app", "-c", "SELECT 1"],
    "interval": "30s"
  }
]
```

---

## etcd

etcd is a distributed key-value store used as the backend for Kubernetes cluster state. Also usable standalone for service discovery and distributed configuration.

> **License**: Apache-2.0 — fully open-source, CNCF project.

### Use in Kubernetes

etcd stores all Kubernetes objects (pods, services, secrets, configmaps). The API server reads/writes etcd directly. Never access etcd directly in production — only via the API server.

### Standalone Use

```bash
# Write
etcdctl put /services/payment/v1 '{"host": "10.0.0.5", "port": 8080}'

# Read
etcdctl get /services/payment/v1

# Watch for changes
etcdctl watch /config/payment --prefix

# Atomic update (compare-and-swap)
etcdctl txn <<EOF
compare:
  mod("/config/timeout", "=", "5")
success:
  put /config/timeout "10"
failure:
  get /config/timeout
EOF
```

### Leader Election with etcd

```python
# python-etcd3
import etcd3

client = etcd3.client()

# Try to become leader via lease
lease = client.lease(ttl=10)
is_leader = client.put_if_not_exists('/leader/payment-service', 'node-1', lease=lease)

if is_leader:
    print("I am the leader")
    lease.refresh_loop()   # keep alive
```

---

## Netflix Eureka

Client-side service discovery — each service registers itself, and clients query the registry.

> **License**: Apache-2.0 — fully open-source.

```java
// Producer: mark as Eureka client
@SpringBootApplication
@EnableEurekaClient
public class PaymentService { ... }

// Consumer: discover via Feign or RestTemplate with load balancing
@FeignClient(name = "payment-service")  // uses Eureka to discover
public interface PaymentClient {
    @GetMapping("/api/charge")
    PaymentResult charge(@RequestBody ChargeRequest request);
}
```

**Eureka vs Consul**: Consul has health checking, multi-datacenter, and K/V store. Eureka is simpler, pure Java, tight Spring integration. Consul is better for polyglot environments. Eureka is maintenance-only (Netflix moved on).

---

## Configuration Management

Separate from service discovery but often handled by the same infrastructure. Config management provides:
- Centralized storage of application config
- Environment-specific values (dev/staging/prod)
- Runtime config changes without redeploy
- Secrets management (see [Security](./18-security.md) for secrets-specific tooling)

### Approaches

| Approach | Tool | Notes |
|----------|------|-------|
| Environment variables | K8s ConfigMap → env | Simple; requires pod restart on change |
| Mounted files | K8s ConfigMap → volume | Pod sees changes within ~60s without restart |
| Push from registry | Consul KV, etcd | Service watches for changes, reconfigures |
| Pull from server | Spring Cloud Config, Nacos | Service polls config server on startup + refresh |
| Feature flags | LaunchDarkly, Unleash | Runtime behavior control without redeploy |

### Spring Cloud Config Server

```yaml
# config-server: serves configs from Git repo
spring:
  cloud:
    config:
      server:
        git:
          uri: https://github.com/myorg/config-repo
          search-paths: "{application}"

# Client app: fetches config at startup
spring:
  application:
    name: payment-service
  config:
    import: "configserver:http://config-server:8888"
```

Config files in Git: `payment-service-production.yaml`, `payment-service-staging.yaml`.

### Nacos (Alibaba)

Service discovery + config management in one, popular in Chinese tech companies' Java ecosystems.

> **License**: Apache-2.0 — open-source.

---

## Service Mesh as Service Discovery

Modern service meshes (Istio, Linkerd) include service discovery via their control plane:

```
Pilot (Istio) → Reads K8s Services → Configures Envoy sidecars with endpoints
Envoy sidecar → Routes based on service name, not hardcoded IP
```

If you're running a service mesh, dedicated service discovery tools (Consul, Eureka) are often redundant for within-cluster traffic.

---

## Production Checklist

- [ ] Services registered with health checks (not just "is process running?")
- [ ] Dead services deregistered automatically (deregister_critical_service_after)
- [ ] DNS TTL short enough for fast failover (5–30 seconds)
- [ ] Consul / etcd cluster has 3 or 5 nodes (quorum-based HA)
- [ ] Service registry backups automated
- [ ] Client-side timeout when registry is unreachable (fallback to cached endpoints)
- [ ] Config changes tested in staging before applying to production
- [ ] Runtime config changes audited (who changed what, when)
- [ ] Secrets never in config management (use Vault / ESO)

---

## Next Steps

- [Security](./18-security.md) — Vault for secrets separate from config
- [Kubernetes](./containerization/kubernetes.md) — ConfigMaps and built-in DNS discovery
- [Service Mesh](./14-service-mesh.md) — Istio as an alternative service discovery layer
