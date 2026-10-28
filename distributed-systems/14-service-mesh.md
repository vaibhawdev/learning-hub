# Service Mesh

## What Is a Service Mesh?

A service mesh is a dedicated infrastructure layer that handles service-to-service communication in a microservices architecture. Instead of each service implementing its own retry logic, circuit breakers, mTLS, and observability, the service mesh provides these capabilities transparently through **sidecar proxies** injected alongside each service.

```
Without Service Mesh:
Service A → (retry? auth? tls?) → Service B

With Service Mesh:
Service A → Sidecar A → [encrypted, retried, observed] → Sidecar B → Service B
                  ↑                                              ↑
           Envoy proxy                                    Envoy proxy
         (same pod, port)                               (same pod, port)
```

---

## Core Capabilities

| Capability | What It Provides |
|-----------|-----------------|
| **mTLS** | Mutual TLS between all services — encrypted + authenticated by default |
| **Traffic management** | Load balancing, retries, timeouts, circuit breaking, traffic splitting |
| **Observability** | Automatic metrics (latency, error rate, throughput) and traces for all service calls |
| **Service discovery** | Dynamic routing without hardcoded service IPs |
| **Security policy** | Enforce which services can communicate with which (authorization policies) |
| **Canary deployments** | Precise traffic splitting (5% to new version) |
| **Fault injection** | Test resilience by injecting artificial delays and errors |

---

## Architecture: The Sidecar Pattern

```
┌─────────────────────────────────────────┐
│              Pod                         │
│  ┌──────────────┐  ┌─────────────────┐  │
│  │  Application │  │  Envoy Sidecar  │  │
│  │  Container   │  │  (injected)     │  │
│  └──────────────┘  └─────────────────┘  │
│          │              ↑     ↓          │
│   localhost:8080     iptables rules      │
│                    intercept all traffic │
└─────────────────────────────────────────┘
```

The sidecar is injected automatically by the mesh control plane via a Kubernetes mutating webhook. The application is unmodified — it thinks it's talking directly to other services, but all traffic is intercepted and handled by Envoy.

### Control Plane vs Data Plane

| | Data Plane | Control Plane |
|-|------------|---------------|
| **What** | Sidecar proxies (Envoy) on every pod | Central management (Istiod, Linkerd controller) |
| **Role** | Handle actual traffic, enforce policies | Configure the proxies, observe aggregate state |
| **Scale** | Grows with number of pods | Independent, typically small cluster |

---

## Istio

Istio is the most feature-rich and widely used service mesh. It uses Envoy as the data plane and `istiod` as the control plane.

### Installation

```bash
istioctl install --set profile=default

# Enable auto-injection in a namespace
kubectl label namespace production istio-injection=enabled
```

### Traffic Management

#### VirtualService

Defines how traffic is routed to a service:

```yaml
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: myapp
spec:
  hosts:
  - myapp
  http:
  - match:
    - headers:
        x-version:
          exact: "v2"
    route:
    - destination:
        host: myapp
        subset: v2
  - route:
    - destination:
        host: myapp
        subset: v1
      weight: 90
    - destination:
        host: myapp
        subset: v2
      weight: 10
    timeout: 30s
    retries:
      attempts: 3
      perTryTimeout: 10s
      retryOn: 5xx,reset,connect-failure
```

#### DestinationRule

Defines load balancing and circuit breaker behavior:

```yaml
apiVersion: networking.istio.io/v1alpha3
kind: DestinationRule
metadata:
  name: myapp
spec:
  host: myapp
  trafficPolicy:
    connectionPool:
      tcp:
        maxConnections: 100
      http:
        http1MaxPendingRequests: 50
        http2MaxRequests: 1000
    outlierDetection:                    # Circuit breaker
      consecutive5xxErrors: 5
      interval: 30s
      baseEjectionTime: 30s
      maxEjectionPercent: 50
    loadBalancer:
      simple: LEAST_CONN
  subsets:
  - name: v1
    labels:
      version: v1
  - name: v2
    labels:
      version: v2
```

#### Gateway

Istio Ingress Gateway for external traffic:

```yaml
apiVersion: networking.istio.io/v1alpha3
kind: Gateway
metadata:
  name: myapp-gateway
spec:
  selector:
    istio: ingressgateway
  servers:
  - port:
      number: 443
      name: https
      protocol: HTTPS
    tls:
      mode: SIMPLE
      credentialName: myapp-tls
    hosts:
    - api.example.com
```

### mTLS

```yaml
# Enforce strict mTLS in a namespace
apiVersion: security.istio.io/v1beta1
kind: PeerAuthentication
metadata:
  name: default
  namespace: production
spec:
  mtls:
    mode: STRICT   # all traffic must be mTLS; no plain TCP
```

### Authorization Policies

```yaml
# Only allow payments service to call orders service
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
  name: orders-policy
  namespace: production
spec:
  selector:
    matchLabels:
      app: orders
  action: ALLOW
  rules:
  - from:
    - source:
        principals:
        - cluster.local/ns/production/sa/payments-service
    to:
    - operation:
        methods: ["POST"]
        paths: ["/api/v1/orders"]
```

### Fault Injection (Chaos Testing)

```yaml
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
spec:
  http:
  - fault:
      delay:
        percentage:
          value: 10.0
        fixedDelay: 5s   # 10% of requests delayed 5 seconds
      abort:
        percentage:
          value: 5.0
        httpStatus: 503  # 5% of requests return 503
    route:
    - destination:
        host: myapp
```

---

## Linkerd

A simpler, lighter alternative to Istio. Written in Rust (proxy) and Go (control plane).

### Key Differences from Istio

| Feature | Istio | Linkerd |
|---------|-------|---------|
| Proxy | Envoy (C++) | Linkerd-proxy (Rust) |
| Memory per proxy | 50–100MB | 10–20MB |
| Configuration complexity | High | Low |
| Feature set | Very rich | Core features, opinionated |
| WASM extensibility | ✓ | ✗ |
| Multi-cluster | ✓ | ✓ (simpler) |
| mTLS | ✓ | ✓ (automatic) |
| Protocol detection | Manual hints | Automatic |

**Choose Linkerd** if you want simpler operations and lower resource overhead with a subset of Istio's features.
**Choose Istio** if you need advanced traffic management, WASM extensions, or have complex routing requirements.

### Linkerd Installation

```bash
linkerd install --crds | kubectl apply -f -
linkerd install | kubectl apply -f -
linkerd inject deployment.yaml | kubectl apply -f -
```

---

## Envoy Proxy

Envoy is the underlying data plane proxy used by Istio, AWS App Mesh, and many other service meshes. Understanding Envoy helps debug service mesh issues.

### Key Concepts

- **Listeners**: Accept incoming connections on a port
- **Routes**: Match traffic and decide how to handle it
- **Clusters**: Upstream service endpoints
- **Filters**: Process traffic (HTTP filters, network filters)

```bash
# Inspect Envoy config (via Istio)
istioctl proxy-config all myapp-pod-xyz

# View listener configuration
istioctl proxy-config listeners myapp-pod-xyz

# View routing table
istioctl proxy-config routes myapp-pod-xyz

# View cluster health
istioctl proxy-config clusters myapp-pod-xyz
```

---

## Observability in the Mesh

A service mesh automatically generates the "golden signals" for every service:

| Signal | Tool | Notes |
|--------|------|-------|
| **Latency** | Prometheus → Grafana | P50/P90/P99 per service pair |
| **Traffic** | Prometheus | Requests/second, broken by route |
| **Error rate** | Prometheus | HTTP 5xx rate per service |
| **Saturation** | Prometheus | Connection pool usage |
| **Traces** | Jaeger / Zipkin | Distributed trace with service hops |

Istio ships with Prometheus, Grafana, Kiali (service topology), and Jaeger integrations.

---

## Service Mesh vs API Gateway

Often confused — they serve different purposes:

| | Service Mesh | API Gateway |
|-|-------------|------------|
| **Position** | Inside the cluster (east-west traffic) | Edge of the cluster (north-south traffic) |
| **Audience** | Service-to-service | External clients |
| **Auth** | mTLS, service identity | API keys, OAuth, JWT |
| **Focus** | Reliability, observability | Rate limiting, authentication, routing |
| **Examples** | Istio, Linkerd | Kong, NGINX, AWS API Gateway |

Use both: API Gateway for external traffic, Service Mesh for internal communication.

---

## Things to Watch in Production

| Concern | What to Check |
|---------|--------------|
| Sidecar CPU/memory overhead | ~50–100MB RAM per pod with Istio Envoy |
| Connection pool exhaustion | `upstream_cx_overflow` in Envoy stats |
| mTLS breaking health checks | Configure liveness/readiness probes for plaintext |
| Control plane connectivity | Proxies can't get config updates if istiod is down |
| Envoy version drift | Proxies and control plane version mismatch → config issues |
| Trace sampling rate | 100% sampling at high volume is expensive; use 1–5% |
| CRD version upgrades | Istio CRD changes between versions require migration |

---

## Production Checklist

- [ ] mTLS in STRICT mode for all production namespaces
- [ ] AuthorizationPolicy on all services (default-deny + explicit allows)
- [ ] Retries configured with idempotency in mind (GET, not POST)
- [ ] Circuit breaker (outlierDetection) on all DestinationRules
- [ ] Timeouts explicitly set on VirtualServices
- [ ] Envoy metrics exported to Prometheus
- [ ] Distributed tracing sampling rate configured (not 100%)
- [ ] Kiali or similar topology visualization in place
- [ ] Linkerd/Istio control plane in HA mode (3+ replicas)
- [ ] Sidecar resource requests/limits set to prevent noisy neighbor

---

## Next Steps

- [API Gateway](./15-api-gateway.md) — external traffic handling that pairs with service mesh
- [Observability](./16-observability.md) — the metrics and traces the mesh generates
- [Security](./18-security.md) — mTLS and zero-trust networking
