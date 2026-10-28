# Kubernetes (K8s)

## What Is Kubernetes?

Kubernetes is an open-source container orchestration platform. It automates the deployment, scaling, scheduling, networking, and self-healing of containerized applications across a cluster of machines.

```
Without Kubernetes:
Manual: which host? how many? what if it crashes? how to update?

With Kubernetes:
You declare: "Run 5 replicas of this container, update without downtime"
K8s handles: scheduling, rescheduling on failure, rolling updates, networking
```

Originally built at Google (based on Borg), Kubernetes is now maintained by the CNCF and has become the de-facto standard for container orchestration.

---

## Architecture

```
                      Control Plane
┌─────────────────────────────────────────────┐
│  API Server    etcd    Scheduler    Controller│
│  (REST API)  (state)  (placement)   Manager  │
└─────────────────────┬───────────────────────┘
                      │ kubelet communicates up
         ─────────────┼─────────────
        │              │             │
   ┌────┴───┐    ┌─────┴──┐    ┌─────┴──┐
   │ Node 1  │    │ Node 2  │    │ Node 3  │
   │ kubelet │    │ kubelet │    │ kubelet │
   │ kube-   │    │ kube-   │    │ kube-   │
   │ proxy   │    │ proxy   │    │ proxy   │
   │ [pods]  │    │ [pods]  │    │ [pods]  │
   └─────────┘    └────────┘    └─────────┘
```

### Control Plane Components

| Component | Role |
|-----------|------|
| **API Server** | The central REST API. All commands go through it. Stateless — scales horizontally. |
| **etcd** | Distributed key-value store holding all cluster state. The single source of truth. |
| **Scheduler** | Assigns pods to nodes based on resource requests, affinity rules, taints, etc. |
| **Controller Manager** | Runs controllers that reconcile actual state to desired state (ReplicaSet, Deployment, etc.) |
| **Cloud Controller Manager** | Integrates with cloud provider APIs (load balancers, storage, node provisioning). |

### Node Components

| Component | Role |
|-----------|------|
| **kubelet** | Agent on each node. Ensures containers in pods are running. Reports status to API server. |
| **kube-proxy** | Maintains iptables/IPVS rules for service networking on each node. |
| **Container Runtime** | Runs containers. containerd (default), CRI-O, or Docker (deprecated in K8s 1.24+). |

---

## Core Objects

### Pod

The smallest deployable unit. One or more containers sharing the same network namespace and storage volumes.

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp
spec:
  containers:
  - name: app
    image: myapp:1.0.0
    ports:
    - containerPort: 8080
    resources:
      requests:
        cpu: "250m"
        memory: "256Mi"
      limits:
        cpu: "500m"
        memory: "512Mi"
```

**Never create standalone Pods in production** — use Deployments. A standalone pod is not rescheduled if the node dies.

### Deployment

Manages a ReplicaSet, which manages pods. Handles rolling updates and rollbacks.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  replicas: 3
  selector:
    matchLabels:
      app: myapp
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  template:
    metadata:
      labels:
        app: myapp
    spec:
      containers:
      - name: app
        image: myapp:1.0.0
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
```

### Service

Stable network endpoint for accessing a set of pods. Pods come and go; a Service provides a stable IP/DNS name.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: myapp
spec:
  selector:
    app: myapp
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP  # or NodePort, LoadBalancer
```

| Service Type | Reachable From | Use Case |
|-------------|---------------|---------|
| `ClusterIP` | Inside cluster only | Internal service-to-service |
| `NodePort` | Any node's IP + port | Development, simple external access |
| `LoadBalancer` | External via cloud LB | Production external traffic |
| `ExternalName` | DNS alias to external service | Access external services by internal name |

### ConfigMap and Secret

```yaml
# ConfigMap: non-sensitive config
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-config
data:
  APP_ENV: production
  LOG_LEVEL: info

# Secret: sensitive data (base64 encoded, not encrypted by default!)
apiVersion: v1
kind: Secret
metadata:
  name: db-credentials
type: Opaque
data:
  username: dXNlcg==    # base64("user")
  password: cGFzc3dvcmQ=  # base64("password")
```

**Important**: Kubernetes Secrets are base64-encoded, not encrypted. Use sealed-secrets, External Secrets Operator, or Vault for production secret management.

### Ingress

Routes external HTTP/HTTPS traffic to services based on host/path rules.

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: myapp
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.example.com
    secretName: api-tls
  rules:
  - host: api.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: myapp
            port:
              number: 80
```

Requires an **Ingress Controller** (NGINX, Traefik, HAProxy, AWS ALB, etc.) to be running.

### Namespace

Logical isolation within a cluster. Teams, environments, or applications get separate namespaces.

```yaml
# Common namespace strategy:
production      # prod workloads
staging         # staging workloads
monitoring      # Prometheus, Grafana
cert-manager    # certificate management
ingress-nginx   # ingress controller
```

---

## Stateful Workloads

### StatefulSet

For stateful applications (databases, Kafka, ZooKeeper) that need:
- Stable pod identity (`pod-0`, `pod-1`, `pod-2`)
- Stable network names (`pod-0.service.namespace.svc.cluster.local`)
- Ordered deployment and scaling
- Persistent storage per pod (via VolumeClaimTemplates)

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
spec:
  serviceName: postgres
  replicas: 3
  volumeClaimTemplates:
  - metadata:
      name: data
    spec:
      accessModes: [ReadWriteOnce]
      resources:
        requests:
          storage: 100Gi
```

### PersistentVolume and PersistentVolumeClaim

```yaml
# PVC: claim storage
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: db-storage
spec:
  accessModes:
  - ReadWriteOnce
  resources:
    requests:
      storage: 50Gi
  storageClassName: standard-ssd
```

StorageClass provisions volumes dynamically from the cloud provider (EBS, GCE PD, Azure Disk).

---

## Scheduling and Placement

### Resource Requests and Limits

```yaml
resources:
  requests:      # Guaranteed minimum (scheduler uses this for placement)
    cpu: "250m"  # 250 millicores = 0.25 CPU
    memory: "256Mi"
  limits:        # Hard maximum (OOM kill if exceeded for memory)
    cpu: "1000m"
    memory: "512Mi"
```

**Always set both requests and limits.** Without requests, pods can be scheduled on overloaded nodes. Without limits, one runaway pod can consume all node resources.

### Node Affinity and Taints

```yaml
# Run only on nodes labeled with "gpu=true"
spec:
  affinity:
    nodeAffinity:
      requiredDuringSchedulingIgnoredDuringExecution:
        nodeSelectorTerms:
        - matchExpressions:
          - key: gpu
            operator: In
            values: ["true"]

# Taint a node for GPU-only workloads
kubectl taint nodes gpu-node-1 gpu=true:NoSchedule

# Tolerate the taint to run on that node
spec:
  tolerations:
  - key: gpu
    value: "true"
    effect: NoSchedule
```

### Pod Disruption Budget

Ensures a minimum number of pods remain available during disruptions (node drains, upgrades):

```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: myapp-pdb
spec:
  minAvailable: 2  # or maxUnavailable: 1
  selector:
    matchLabels:
      app: myapp
```

---

## Autoscaling

### Horizontal Pod Autoscaler (HPA)

Scales replicas based on CPU, memory, or custom metrics:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: myapp
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: myapp
  minReplicas: 2
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: External
    external:
      metric:
        name: kafka_consumer_lag
      target:
        type: AverageValue
        averageValue: 1000
```

### Vertical Pod Autoscaler (VPA)

Adjusts CPU/memory requests based on actual usage. Requires pod restart — use for development or in conjunction with HPA.

### Cluster Autoscaler

Automatically adds or removes worker nodes based on pod scheduling demand. Cloud-provider-specific (AWS, GCP, Azure).

### KEDA (Kubernetes Event-Driven Autoscaling)

Scales pods based on external event sources (Kafka lag, queue depth, HTTP request rate, Prometheus metrics):

```yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: kafka-consumer
spec:
  scaleTargetRef:
    name: my-kafka-consumer
  minReplicaCount: 0  # scale to zero when idle
  maxReplicaCount: 30
  triggers:
  - type: kafka
    metadata:
      bootstrapServers: kafka:9092
      consumerGroup: my-group
      topic: orders
      lagThreshold: "100"
```

---

## Health Probes

```yaml
livenessProbe:
  # Kills and restarts container if this fails
  httpGet:
    path: /healthz
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  failureThreshold: 3

readinessProbe:
  # Removes pod from Service endpoints if this fails
  # (traffic stops, pod stays alive)
  httpGet:
    path: /ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
  failureThreshold: 3

startupProbe:
  # For slow-starting apps: disables liveness until app is ready
  httpGet:
    path: /healthz
    port: 8080
  failureThreshold: 30
  periodSeconds: 10
```

---

## Network Policies

By default, all pods can communicate with all other pods. Network Policies restrict traffic:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: db-policy
spec:
  podSelector:
    matchLabels:
      app: postgres
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: myapi   # only myapi can reach postgres
    ports:
    - port: 5432
```

Requires a CNI plugin that supports NetworkPolicy (Calico, Cilium, Weave).

---

## RBAC (Role-Based Access Control)

```yaml
# Role: permissions within a namespace
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  namespace: production
  name: pod-reader
rules:
- apiGroups: [""]
  resources: ["pods"]
  verbs: ["get", "list", "watch"]

# RoleBinding: grant role to user/service account
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: pod-reader-binding
  namespace: production
subjects:
- kind: ServiceAccount
  name: monitoring-agent
roleRef:
  kind: Role
  name: pod-reader
  apiGroup: rbac.authorization.k8s.io
```

**Principle of least privilege**: Grant only what's needed. Avoid `cluster-admin` bindings for workloads.

---

## Deployment Strategies

### Rolling Update (Default)

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1          # one extra pod during update
    maxUnavailable: 0    # zero pods down at any time
```

New pods come up before old ones go down. Zero-downtime by default.

### Blue-Green Deployment

Run two identical environments (blue = current, green = new). Switch traffic by updating Service selector.

```bash
# Green deployment is live with new version
kubectl patch service myapp -p '{"spec":{"selector":{"version":"green"}}}'
# Instant traffic switch; blue is standby for rollback
```

### Canary Deployment

Route a small percentage of traffic to the new version:

```yaml
# 90% → stable deployment (90 replicas)
# 10% → canary deployment (10 replicas)
# Traffic split by pod count ratio
```

Use a service mesh (Istio) or Ingress controller for precise percentage-based splitting without depending on replica counts.

---

## Production Checklist

- [ ] Resource requests and limits set on all containers
- [ ] Liveness, readiness, and startup probes configured
- [ ] Pod Disruption Budgets defined for all critical workloads
- [ ] RBAC configured; no cluster-admin for workloads
- [ ] Network Policies defined (default-deny pattern)
- [ ] Secrets managed via External Secrets Operator or Vault (not plain K8s Secrets)
- [ ] Pod Anti-Affinity to spread replicas across nodes
- [ ] HPA configured for variable-load workloads
- [ ] Node labels and taints for workload isolation (GPU, database, etc.)
- [ ] Image pull policy: `IfNotPresent` with pinned tags
- [ ] Namespace resource quotas to prevent one team starving another
- [ ] Security Context: `runAsNonRoot: true`, `readOnlyRootFilesystem: true`
- [ ] Ingress with TLS termination and cert-manager
- [ ] etcd backups automated
- [ ] Multi-AZ node placement for HA control plane and worker nodes

---

## Managed Kubernetes Services

| Service | Provider | Notes |
|---------|----------|-------|
| **EKS** | AWS | Most feature-complete, deep AWS integration |
| **GKE** | Google | Autopilot mode (serverless), best K8s ops tooling |
| **AKS** | Azure | Good for Azure shops; Windows node support |
| **DOKS** | DigitalOcean | Simple, cheaper for small workloads |
| **Rancher** | Open-source | Manage multiple clusters across clouds |
| **k3s** | Lightweight | Edge, IoT, dev environments |
| **kind** | Local dev | Kubernetes in Docker — CI testing |
| **minikube** | Local dev | Single-node local cluster |

---

## Next Steps

- [Helm Charts](../devops/helm-charts.md) — packaging and templating K8s manifests
- [ArgoCD & GitOps](../devops/argocd-gitops.md) — deploying to Kubernetes via Git
- [Observability](../16-observability.md) — monitoring a Kubernetes cluster
- [Service Mesh](../14-service-mesh.md) — Istio on top of Kubernetes
