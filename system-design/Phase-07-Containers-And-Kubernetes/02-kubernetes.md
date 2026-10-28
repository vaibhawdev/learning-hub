# Kubernetes

> **Phase:** 7 — Containers and Kubernetes  
> **Status:** Not Started

## Overview

Kubernetes is the standard platform for running containerized workloads at scale. This section covers Kubernetes from basic primitives to production operations — scheduling, autoscaling, networking, security, and operator patterns — the knowledge needed to run reliable services on AKS or any Kubernetes distribution.

## Topics

### Pods

**What it is:** The smallest deployable unit in Kubernetes — a group of one or more containers sharing the same network namespace, storage volumes, and lifecycle.

**Why it matters:** Pods define the co-location boundary and resource allocation unit — understanding when to put containers in the same pod vs separate pods affects networking, scaling, and fault isolation.

**Key concepts to understand:**
- Init containers: run to completion before app containers start — for setup tasks (DB migrations, config fetching)
- Sidecar containers: helper containers sharing the pod network (log shippers, Envoy proxy, secret rotators)
- Pod lifecycle: Pending → Running → Succeeded/Failed
- `terminationGracePeriodSeconds`: how long Kubernetes waits for graceful shutdown before SIGKILL
- Resource requests and limits: requests for scheduling decisions; limits enforced by cgroups
- Liveness, readiness, and startup probes: health checks that control restart and traffic behavior

**Resources to explore:**
- [ ] (to be filled)

---

### Deployments

**What it is:** A Kubernetes resource that manages a ReplicaSet, providing declarative updates with rollout strategies, rollback capabilities, and scaling for stateless workloads.

**Why it matters:** Deployments are how you run production stateless services — understanding rollout strategies and health checks prevents broken deployments from affecting production traffic.

**Key concepts to understand:**
- Rolling update strategy: replace pods gradually — `maxSurge` and `maxUnavailable` control pace
- Recreate strategy: delete all old pods, create new — causes downtime, useful when in-place update is impossible
- `kubectl rollout undo`: roll back to previous deployment revision
- Deployment revision history: `revisionHistoryLimit` controls how many old ReplicaSets are retained
- Pod disruption budget (PDB): minimum available pods during voluntary disruptions (node drain, cluster upgrade)
- `progressDeadlineSeconds`: fail the rollout if it doesn't complete within this time

**Resources to explore:**
- [ ] (to be filled)

---

### StatefulSets

**What it is:** A Kubernetes workload API for stateful applications — providing stable pod identities, ordered deployment, and persistent volume claims that stick to specific pods.

**Why it matters:** Databases, message queues, and other stateful workloads need stable identities and persistent storage — StatefulSets provide these guarantees that Deployments cannot.

**Key concepts to understand:**
- Stable pod names: `pod-0`, `pod-1`, `pod-2` — predictable DNS names for peer discovery
- Ordered startup/shutdown: pods start in order (0, 1, 2...) and shut down in reverse
- Persistent Volume Claims: each pod gets its own PVC that persists across pod restarts
- Headless service: ClusterIP=None — enables direct pod DNS resolution for peer discovery
- StatefulSet updates: rolling updates with per-pod readiness wait
- Use cases: Cassandra, ZooKeeper, Kafka, Redis Cluster — any clustered stateful software

**Resources to explore:**
- [ ] (to be filled)

---

### Services

**What it is:** A Kubernetes abstraction that provides a stable network endpoint (IP and DNS name) for a set of pods selected by label selector — independent of pod IP changes.

**Why it matters:** Pod IPs are ephemeral — Services provide the stable endpoint that enables service discovery and load balancing within a Kubernetes cluster.

**Key concepts to understand:**
- ClusterIP (default): stable virtual IP only reachable within the cluster — uses kube-proxy iptables/IPVS rules
- NodePort: exposes service on each node's IP at a static port — for development, not production
- LoadBalancer: provisions a cloud load balancer (Azure LB) — standard for external traffic ingress
- Headless service (ClusterIP=None): returns pod IPs directly via DNS — for StatefulSets and custom discovery
- `ExternalName`: DNS CNAME to an external service — for migrating external services into Kubernetes
- Session affinity: `service.spec.sessionAffinity: ClientIP` for sticky sessions (use sparingly)

**Resources to explore:**
- [ ] (to be filled)

---

### Ingress

**What it is:** A Kubernetes API object that manages external HTTP/HTTPS access to cluster services, providing host-based and path-based routing, SSL termination, and load balancing.

**Why it matters:** Ingress enables one external IP to route to many services based on hostname or path — essential for multi-service deployments without a separate load balancer per service.

**Key concepts to understand:**
- Ingress controller: the actual implementation (NGINX, Traefik, AGIC) that watches Ingress resources
- TLS termination: `spec.tls` with a secret containing the certificate — traffic decrypted at ingress
- Path types: `Exact`, `Prefix`, `ImplementationSpecific`
- Annotations: controller-specific configuration (rate limits, timeouts, redirects) via annotations
- cert-manager: automatic TLS certificate provisioning from Let's Encrypt for Ingress resources
- AGIC (Application Gateway Ingress Controller): uses Azure Application Gateway as the Ingress implementation

**Resources to explore:**
- [ ] (to be filled)

---

### ConfigMaps

**What it is:** Kubernetes API objects for storing non-sensitive configuration data as key-value pairs, mountable as environment variables or files into pods.

**Why it matters:** ConfigMaps decouple configuration from container images, enabling environment-specific configuration without rebuilding images.

**Key concepts to understand:**
- `envFrom: configMapRef`: inject all ConfigMap keys as environment variables
- `env.valueFrom.configMapKeyRef`: inject a specific key as an environment variable
- Volume mount: mount ConfigMap data as files — useful for configuration files
- Immutable ConfigMaps: prevent accidental modification — `immutable: true`
- ConfigMap update and rolling restart: changes to ConfigMaps require pod restart to take effect (unless using volume mounts with live reload)
- Size limit: ConfigMaps are limited to 1MB — use volume mounts for large configuration

**Resources to explore:**
- [ ] (to be filled)

---

### Secrets

**What it is:** Kubernetes API objects for storing sensitive data (passwords, tokens, certificates) — stored base64-encoded in etcd and injectable into pods as environment variables or files.

**Why it matters:** Kubernetes Secrets are only marginally more secure than ConfigMaps by default — understanding their limitations and proper integration with external secret stores (Key Vault) is critical for security.

**Key concepts to understand:**
- Default encoding: base64, not encryption — secrets are readable by anyone with etcd access
- etcd encryption at rest: Kubernetes can be configured to encrypt secrets in etcd
- External Secrets Operator: sync secrets from Key Vault, AWS Secrets Manager, etc. into Kubernetes Secrets
- Azure Key Vault Provider for Secrets Store CSI Driver: mount Key Vault secrets as files or env vars
- RBAC for Secrets: limit secret read access to specific service accounts — use `Role`/`RoleBinding`
- Never log secret values: avoid `printenv` or debug outputs that could expose secret values

**Resources to explore:**
- [ ] (to be filled)

---

### HPA / VPA

**What it is:** Horizontal Pod Autoscaler (HPA) scales pod replicas based on metrics; Vertical Pod Autoscaler (VPA) adjusts pod resource requests/limits based on observed usage.

**Why it matters:** Autoscaling is fundamental to cloud cost efficiency and reliability — HPA handles traffic spikes; VPA right-sizes resource requests to prevent OOM kills and overprovisioning.

**Key concepts to understand:**
- HPA: `kubectl autoscale deployment myapp --min=2 --max=10 --cpu-percent=70`
- Custom metrics HPA: scale on business metrics (queue depth, request rate) via Prometheus Adapter or KEDA
- KEDA (Kubernetes Event-Driven Autoscaling): scale to zero + scale on Azure Service Bus, Event Hub, Redis
- VPA modes: Off (recommend only), Initial (set on creation), Auto (update running pods)
- VPA + HPA conflict: don't use both on CPU — VPA changes requests, HPA reacts to them — use HPA on CPU, VPA on memory
- Cluster autoscaler: scales node count when pods are unschedulable or nodes are underutilized

**Resources to explore:**
- [ ] (to be filled)

---

### Scheduling

**What it is:** The Kubernetes scheduler assigns pods to nodes based on resource requirements, node affinity/anti-affinity rules, taints/tolerations, and topology constraints.

**Why it matters:** Understanding scheduling enables spreading workloads across zones for HA, co-locating related services for performance, and isolating workloads onto dedicated node pools.

**Key concepts to understand:**
- Resource requests: scheduler places pods on nodes with sufficient available CPU and memory
- Node affinity: prefer or require pods to run on nodes with specific labels
- Pod affinity/anti-affinity: co-locate or spread pods based on pod labels (zone spread)
- Taints and tolerations: nodes can repel pods unless they tolerate the taint — for dedicated node pools
- Pod topology spread constraints: ensure pods spread across zones or nodes — for HA
- Priority classes: high-priority pods can preempt low-priority ones when resources are scarce

**Resources to explore:**
- [ ] (to be filled)

---

### Operators

**What it is:** Kubernetes extension pattern that encodes domain-specific operational knowledge as custom controllers that manage custom resources — automating complex stateful workload management.

**Why it matters:** Operators bring the "Day 2" operations knowledge for databases, message queues, and ML workloads into Kubernetes — eliminating manual operational runbooks.

**Key concepts to understand:**
- Custom Resource Definition (CRD): extend Kubernetes API with domain-specific resource types
- Controller loop: watch resource state → compute desired state → reconcile difference
- Operator SDK / Kubebuilder: scaffolding and libraries for writing operators in Go
- Well-known operators: Prometheus Operator, cert-manager, Strimzi (Kafka), MongoDB Community Operator
- Operator lifecycle: Leader election for HA operators, garbage collection of owned resources
- Writing .NET Operators: KubeOps library for implementing operators in C#

**Resources to explore:**
- [ ] (to be filled)

---

### Helm

**What it is:** The Kubernetes package manager — Helm charts package Kubernetes manifests as versioned, parameterizable templates that can be installed, upgraded, and rolled back.

**Why it matters:** Helm is the standard way to distribute and deploy Kubernetes applications — understanding charts, values, and releases enables reliable, reproducible application deployment.

**Key concepts to understand:**
- Chart: directory of templates + `Chart.yaml` metadata + `values.yaml` defaults
- Release: an installed instance of a chart with specific values — can have multiple releases of the same chart
- `helm install`, `helm upgrade --install`, `helm rollback`: lifecycle commands
- Values override: `-f custom-values.yaml` or `--set key=value` for environment-specific configuration
- `helm template`: render templates without installing — for debugging or GitOps
- Dependency management: `Chart.lock` and subcharts for composing complex deployments

**Resources to explore:**
- [ ] (to be filled)

---

### Service Mesh

**What it is:** An infrastructure layer for handling service-to-service communication in Kubernetes, implemented as sidecar proxies (Envoy/Linkerd) that handle mTLS, retries, circuit breaking, and distributed tracing.

**Why it matters:** Service meshes remove networking resilience and observability concerns from application code — services gain automatic mTLS, retries, and traces without code changes.

**Key concepts to understand:**
- Istio: most full-featured, high configurability, higher complexity and resource cost
- Linkerd: lightweight, focused on security and observability, low overhead
- Sidecar injection: Kubernetes mutating webhook automatically adds Envoy sidecar to pods
- mTLS: all pod-to-pod communication encrypted and mutually authenticated
- VirtualService and DestinationRule (Istio): traffic management for canary deployments and circuit breaking
- Ambient mesh (Istio 1.15+): eliminates per-pod sidecar in favor of node-level proxy — lower overhead

**Resources to explore:**
- [ ] (to be filled)

---

### Kubernetes Networking

**What it is:** The networking model in Kubernetes — every pod gets its own IP, all pods can reach all other pods directly, and services provide stable virtual IPs.

**Why it matters:** Understanding the Kubernetes networking model — particularly how kube-proxy, CNI plugins, and network policies interact — is essential for debugging connectivity and enforcing network security.

**Key concepts to understand:**
- Pod network: flat L3 network — all pods can communicate without NAT (by default)
- CNI plugins: Azure CNI, Calico, Cilium, Flannel — implement pod network connectivity
- kube-proxy: programs iptables/IPVS rules for Service virtual IPs
- Network Policies: firewall rules for pod-to-pod traffic — default deny all ingress/egress
- Cilium eBPF networking: replaces kube-proxy with kernel-level eBPF rules — higher performance, richer policy
- DNS: kube-dns / CoreDNS resolves `service.namespace.svc.cluster.local` to Service ClusterIPs

**Resources to explore:**
- [ ] (to be filled)

---

### Kubernetes Security

**What it is:** The set of controls for securing a Kubernetes cluster — RBAC for API access, Pod Security Standards for workload hardening, network policies for traffic isolation, and supply chain security for images.

**Why it matters:** Kubernetes clusters are high-value targets — a misconfigured cluster can expose all workloads and secrets; securing it requires deliberate configuration at multiple layers.

**Key concepts to understand:**
- RBAC: `Role` + `RoleBinding` (namespace) or `ClusterRole` + `ClusterRoleBinding` (cluster) for API access control
- Pod Security Standards: Privileged / Baseline / Restricted — enforced via admission controller
- Service Account token projection: short-lived, audience-bound tokens instead of long-lived secrets
- Admission controllers: ValidatingWebhook, MutatingWebhook — enforce policies on resource creation
- Image signature verification: cosign + Sigstore for verifying image provenance
- Secret encryption: encrypt etcd secrets at rest using KMS provider (Azure Key Vault)

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Docker](./01-docker.md)
- [AKS](../Phase-06-Cloud-And-Azure/01-azure-core.md)
- [Cloud Security](../Phase-10-Security-Engineering/03-cloud-security.md)
- [Observability](../Phase-09-Reliability-And-Observability/01-observability.md)
