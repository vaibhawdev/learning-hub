# Security in Distributed Systems

## The Threat Model Has Changed

In a monolith, you trusted everything inside the process boundary. In a distributed system:
- Services call each other over a network — any hop can be intercepted
- Many more attack surfaces: each service, each API, each container, each queue
- More people (and CI/CD robots) have access to parts of the system
- A compromised container can move laterally to other services

The guiding principle: **Zero Trust** — never trust, always verify. Authenticate and authorize every request regardless of where it originates.

---

## Zero Trust Architecture

```
Traditional (Castle-and-Moat):
Outside │ Firewall │ Inside (trusted)
        │          │ Service A → Service B: trusted, no auth

Zero Trust:
All traffic is untrusted, regardless of network location.
Service A → Service B: must authenticate + be authorized
```

Zero trust requires:
1. **Identity for every workload** (not just users)
2. **mTLS** between services (both sides present certificates)
3. **Least-privilege access** — services can only access what they need
4. **Continuous verification** — token expiry, cert rotation
5. **Encrypted data in transit and at rest**

---

## Mutual TLS (mTLS)

Regular TLS: Server proves its identity to the client.
mTLS: Both server and client prove their identities via certificates.

```
Client cert ─────────────────────────────────────────► Server
            ◄──────────────────── Server cert ─────────
            ◄──────────── Encrypted channel established
```

### In Kubernetes

A service mesh (Istio, Linkerd) provides automatic mTLS without application code changes:

```yaml
# Istio: enforce strict mTLS in production namespace
apiVersion: security.istio.io/v1beta1
kind: PeerAuthentication
metadata:
  name: default
  namespace: production
spec:
  mtls:
    mode: STRICT
```

### Certificate Management

**cert-manager** (Kubernetes) automatically issues and renews TLS certificates:

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: myapp-tls
spec:
  secretName: myapp-tls
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
  dnsNames:
  - api.example.com
```

Certificates rotate automatically before expiry.

---

## Secrets Management

### The Problem with Kubernetes Secrets

Kubernetes Secrets are base64-encoded, not encrypted. Anyone with `kubectl get secret` access sees the value. By default they are stored unencrypted in etcd.

```bash
kubectl get secret db-credentials -o jsonpath='{.data.password}' | base64 -d
# → mypassword (anyone with RBAC access can do this)
```

### HashiCorp Vault

The standard for secrets management in distributed systems.

```
Application → Vault → Returns: database credentials with TTL
                             → auto-renews or rotates credentials
                             → audit log of every access
```

**Key features:**
- **Dynamic secrets**: Vault creates short-lived, unique credentials on demand (e.g., a unique Postgres user that expires in 1 hour)
- **Secret leasing**: Credentials expire and must be renewed
- **Audit log**: Every secret read/write is logged
- **PKI**: Issue internal TLS certificates
- **Transit engine**: Encrypt/decrypt data without Vault storing the secret

```hcl
# Vault policy: payment service can only read payment secrets
path "secret/data/payment-service/*" {
  capabilities = ["read"]
}

# Dynamic database credentials
vault read database/creds/payment-service-role
# Key             Value
# username        v-payment-xXyY123
# password        A1b2C3d4...
# lease_duration  1h
```

**Vault Agent**: Sidecar that authenticates to Vault and writes secrets to a file or environment variable — the application reads from the filesystem and never talks to Vault directly.

### External Secrets Operator (ESO)

Syncs secrets from external secret stores (AWS Secrets Manager, GCP Secret Manager, HashiCorp Vault, Azure Key Vault) into Kubernetes Secrets automatically:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: db-credentials
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: vault-backend
    kind: ClusterSecretStore
  target:
    name: db-credentials     # creates/updates this K8s Secret
  data:
  - secretKey: password      # K8s secret key
    remoteRef:
      key: secret/payment-service/db  # Vault path
      property: password
```

### Sealed Secrets

Encrypt secrets and store them safely in Git. Only the Sealed Secrets controller (running in the cluster) can decrypt them.

```bash
kubeseal --format yaml < secret.yaml > sealed-secret.yaml
git add sealed-secret.yaml   # safe to commit
```

---

## Authentication and Authorization

### Authentication (AuthN) — Who are you?

**Service-to-service**: mTLS (the certificate identity).

**User-to-service**: JWT tokens issued by an identity provider (Auth0, Keycloak, Okta, Cognito).

```
User → Login → Identity Provider → JWT (signed)
User → API → API Gateway → Validate JWT (signature + expiry) → Forward claims
```

### Authorization (AuthZ) — What can you do?

#### RBAC (Role-Based Access Control)

```
User → Role → Permissions

User Alice → role:admin → permissions: [read, write, delete]
User Bob → role:reader → permissions: [read]
```

Kubernetes RBAC:
```yaml
# Role: can read pods in monitoring namespace
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  namespace: monitoring
  name: pod-reader
rules:
- apiGroups: [""]
  resources: ["pods", "pods/log"]
  verbs: ["get", "list", "watch"]
```

#### ABAC (Attribute-Based Access Control)

Policies based on attributes of users, resources, and environment:

```
Policy: allow if user.department == resource.department AND time.hour >= 9 AND time.hour <= 17
```

More flexible than RBAC; used in OPA (Open Policy Agent).

#### Open Policy Agent (OPA)

Policy-as-code. Write policies in Rego language; OPA evaluates them:

```rego
# Only allow access from the same namespace
allow {
  input.source.namespace == input.destination.namespace
}

# Admins can access everything
allow {
  input.user.role == "admin"
}
```

OPA integrates with Kubernetes (admission controller), Istio (authorization), API gateways.

---

## Network Security

### Kubernetes NetworkPolicy

Default: all pods can communicate with all pods. NetworkPolicy restricts this:

```yaml
# Default deny all ingress + egress in a namespace
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-all
spec:
  podSelector: {}  # matches all pods
  policyTypes:
  - Ingress
  - Egress

---
# Allow specific traffic
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-payment-to-db
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
          app: payment-service
    ports:
    - protocol: TCP
      port: 5432
```

Requires a CNI with NetworkPolicy support: **Calico**, **Cilium**, Weave.

### Cilium (eBPF-Based)

Cilium provides NetworkPolicy plus L7 policy enforcement (HTTP method, path, gRPC service):

```yaml
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
spec:
  ingress:
  - fromEndpoints:
    - matchLabels:
        app: frontend
    toPorts:
    - ports:
      - port: "8080"
        protocol: TCP
      rules:
        http:
        - method: "GET"
          path: "/api/public/.*"   # frontend can only GET public paths
```

---

## Container Security

### Seccomp and AppArmor

Restrict system calls containers can make:

```yaml
spec:
  securityContext:
    seccompProfile:
      type: RuntimeDefault   # apply default seccomp profile
  containers:
  - securityContext:
      allowPrivilegeEscalation: false
      runAsNonRoot: true
      runAsUser: 1000
      readOnlyRootFilesystem: true
      capabilities:
        drop:
        - ALL
        add:
        - NET_BIND_SERVICE   # only if port < 1024 needed
```

### Pod Security Admission (PSA)

Kubernetes built-in policy enforcement (replaced PodSecurityPolicy in 1.25+):

```yaml
# Label namespace to enforce restricted security standard
kubectl label namespace production \
  pod-security.kubernetes.io/enforce=restricted \
  pod-security.kubernetes.io/warn=restricted
```

Standards: `privileged` (no restrictions), `baseline` (common restrictions), `restricted` (most secure).

### Image Security

```bash
# Scan images in CI
trivy image myapp:1.0.0 --exit-code 1 --severity CRITICAL

# Verify image signature before deployment (Cosign)
cosign verify --certificate-identity=ci@myorg.com \
  --certificate-oidc-issuer=https://token.actions.githubusercontent.com \
  myapp:sha-abc123

# Only allow signed images (OPA + Cosign admission webhook)
```

---

## Audit Logging

All security-relevant events must be logged immutably:

- Authentication success/failure
- Authorization decisions (access denied)
- Secret access (Vault audit log)
- Kubernetes API calls (K8s audit log)
- Privileged operations (kubectl exec, port-forward)

```yaml
# Kubernetes audit policy
apiVersion: audit.k8s.io/v1
kind: Policy
rules:
- level: Metadata
  resources:
  - group: ""
    resources: ["secrets", "configmaps"]
- level: Request
  users: ["system:serviceaccount:kube-system:*"]
- level: RequestResponse
  verbs: ["create", "delete", "update"]
  resources:
  - group: ""
    resources: ["pods"]
```

---

## OWASP Top 10 in Distributed Systems

| Vulnerability | Distributed System Risk | Mitigation |
|--------------|------------------------|------------|
| Injection | SQL, LDAP, command injection in microservices | Parameterized queries, input validation at service boundary |
| Broken Authentication | JWT without expiry, weak signing keys | Short TTL tokens, RS256 over HS256, key rotation |
| Sensitive Data Exposure | Secrets in logs, unencrypted inter-service traffic | Structured logging (no PII in logs), mTLS everywhere |
| XML/JSON Injection | Malformed payloads forwarded downstream | Input validation at ingress, schema validation |
| Security Misconfiguration | Default credentials, open ports, permissive RBAC | Hardened K8s defaults, PSA, NetworkPolicy |
| SSRF | Services fetching URLs from user input to internal services | Allowlist outbound destinations, no internal metadata access |

---

## Production Checklist

- [ ] mTLS enforced between all services (Istio STRICT mode or equivalent)
- [ ] Secrets managed via Vault or cloud secrets manager (not K8s Secrets in plaintext)
- [ ] External Secrets Operator for K8s Secret population
- [ ] Kubernetes RBAC follows least-privilege (no `cluster-admin` for workloads)
- [ ] NetworkPolicy: default-deny, explicit allow rules per service
- [ ] Container security context: non-root, read-only filesystem, dropped capabilities
- [ ] Image signature verification at admission
- [ ] Vulnerability scanning in CI pipeline with fail-on-critical
- [ ] Audit logging for K8s API, Vault, and application access
- [ ] Pod Security Admission set to `restricted` for production namespaces
- [ ] Secrets rotated regularly (Vault TTL or scheduled rotation)
- [ ] No sensitive data in logs (PII, tokens, passwords)
- [ ] WAF in front of API Gateway for public endpoints

---

## Next Steps

- [Service Mesh](./14-service-mesh.md) — mTLS and authorization policies at the mesh layer
- [Kubernetes](./containerization/kubernetes.md) — RBAC, Network Policies, PSA
- [API Gateway](./15-api-gateway.md) — AuthN/AuthZ at the edge
