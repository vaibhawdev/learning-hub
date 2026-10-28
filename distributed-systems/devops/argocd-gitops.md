# ArgoCD & GitOps

## What Is GitOps?

GitOps is an operational framework where:
- **Git is the single source of truth** for all infrastructure and application state
- **Desired state is declared** in Git (YAML manifests, Helm charts, Kustomize overlays)
- **An automated agent** (ArgoCD, Flux) continuously reconciles cluster state to match Git

```
Traditional (Push-based CI/CD):
Developer → Git push → CI pipeline → kubectl apply → Cluster

GitOps (Pull-based):
Developer → Git push → ArgoCD detects change → Syncs cluster to match Git
                                              ↑
                                    ArgoCD continuously watches Git
```

### GitOps Principles

1. **Declarative**: Describe desired state, not imperative steps
2. **Versioned and immutable**: All changes tracked in Git history
3. **Pulled automatically**: Software agents pull and apply changes
4. **Continuously reconciled**: Agents detect and correct drift between desired and actual state

### Benefits

- **Audit trail**: Every deployment is a Git commit with author, timestamp, and diff
- **Easy rollback**: Revert Git commit = rollback deployment
- **Drift detection**: ArgoCD alerts when cluster state differs from Git
- **Security**: No CI runner needs cluster credentials — ArgoCD runs inside the cluster
- **Disaster recovery**: Recreate an environment by applying the Git repository

---

## ArgoCD Architecture

```
Git Repository
     │
     │ (poll / webhook)
     ▼
┌────────────────────────────────────┐
│           ArgoCD                   │
│  ┌──────────┐  ┌────────────────┐  │
│  │  API     │  │  Application   │  │
│  │  Server  │  │  Controller    │  │
│  └──────────┘  └────────────────┘  │
│  ┌──────────┐  ┌────────────────┐  │
│  │  Repo    │  │  Redis (cache) │  │
│  │  Server  │  │                │  │
│  └──────────┘  └────────────────┘  │
└────────────────────────────────────┘
         │
         │ kubectl apply (in-cluster)
         ▼
   Kubernetes Cluster(s)
```

| Component | Role |
|-----------|------|
| **API Server** | REST/gRPC API, Web UI, CLI. Manages applications and RBAC. |
| **Application Controller** | Watches Kubernetes resources. Detects drift. Reconciles state. |
| **Repo Server** | Clones Git repos, renders manifests (Helm, Kustomize, plain YAML). |
| **Redis** | Caches rendered manifests and cluster state for performance. |

---

## Core Concepts

### Application

An ArgoCD Application defines:
- **Source**: Which Git repo, path, and revision to watch
- **Destination**: Which cluster and namespace to deploy to
- **Sync Policy**: Manual or automatic synchronization

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: myapp
  namespace: argocd
spec:
  project: default

  source:
    repoURL: https://github.com/myorg/myapp-config
    targetRevision: main
    path: k8s/production
    # For Helm charts:
    helm:
      valueFiles:
      - values-prod.yaml
      parameters:
      - name: image.tag
        value: "1.2.3"

  destination:
    server: https://kubernetes.default.svc
    namespace: production

  syncPolicy:
    automated:
      prune: true      # delete resources removed from Git
      selfHeal: true   # re-sync if cluster drifts from Git
    syncOptions:
    - CreateNamespace=true
    - PrunePropagationPolicy=foreground
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
```

### Sync Status

| Status | Meaning |
|--------|---------|
| **Synced** | Cluster matches Git |
| **OutOfSync** | Cluster has drifted from Git |
| **Unknown** | Cannot determine status |

### Health Status

| Status | Meaning |
|--------|---------|
| **Healthy** | All resources running correctly |
| **Progressing** | Deployment/rollout in progress |
| **Degraded** | Deployment failed, pods in CrashLoopBackOff |
| **Missing** | Resource exists in Git but not in cluster |
| **Suspended** | Application is paused |

---

## App of Apps Pattern

Instead of creating many Application resources manually, create one "parent" Application that manages other Applications:

```
App of Apps (apps.yaml in Git)
│
├── Application: myapp-frontend  → points to frontend chart
├── Application: myapp-backend   → points to backend chart
├── Application: myapp-database  → points to database chart
└── Application: myapp-redis     → points to redis chart
```

```yaml
# app-of-apps.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: apps
  namespace: argocd
spec:
  source:
    repoURL: https://github.com/myorg/gitops-repo
    path: apps/production         # directory containing Application YAMLs
    targetRevision: main
  destination:
    server: https://kubernetes.default.svc
    namespace: argocd
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

---

## ApplicationSet

Generates multiple Applications automatically from a template. Replaces manual App of Apps for large fleets:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: ApplicationSet
metadata:
  name: myapp-environments
spec:
  generators:
  # Generate one application per directory in the repo
  - git:
      repoURL: https://github.com/myorg/gitops-repo
      revision: main
      directories:
      - path: environments/*

  # OR generate per cluster
  - clusters:
      selector:
        matchLabels:
          environment: production

  template:
    metadata:
      name: '{{path.basename}}'
    spec:
      project: default
      source:
        repoURL: https://github.com/myorg/myapp
        path: '{{path}}'
        targetRevision: main
      destination:
        server: '{{server}}'
        namespace: '{{path.basename}}'
```

---

## Multi-Cluster Management

ArgoCD can manage multiple Kubernetes clusters from a single control plane:

```bash
# Add external cluster to ArgoCD
argocd cluster add my-production-cluster --name production

# Deploy to specific cluster
argocd app create myapp \
  --repo https://github.com/myorg/myapp \
  --path k8s \
  --dest-server https://my-production-cluster-api-server \
  --dest-namespace production
```

---

## Repository Structure Patterns

### Environment Directories

```
gitops-repo/
├── apps/              # ApplicationSet or App-of-Apps definitions
└── environments/
    ├── dev/
    │   ├── myapp/
    │   │   └── values.yaml
    ├── staging/
    │   ├── myapp/
    │   │   └── values.yaml
    └── production/
        ├── myapp/
        │   └── values.yaml
```

### Monorepo vs Polyrepo

| Approach | Description | Pros | Cons |
|----------|-------------|------|------|
| **Monorepo** | All service configs in one repo | Easy cross-service changes | Permissions harder to scope |
| **Polyrepo** | One repo per service | Team autonomy, clear ownership | Cross-cutting changes need multiple PRs |
| **Config repo** | Application code repo + separate config repo | Clean separation of app and infra | Two-repo workflow friction |

**Recommendation**: Separate application code repo from GitOps config repo. CI builds and pushes image, then updates image tag in config repo, triggering ArgoCD sync.

---

## Image Updater

ArgoCD Image Updater automatically updates the image tag in Git when a new image is pushed to the registry:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  annotations:
    argocd-image-updater.argoproj.io/image-list: myapp=myrepo/myapp
    argocd-image-updater.argoproj.io/myapp.update-strategy: semver
    argocd-image-updater.argoproj.io/myapp.allow-tags: regexp:^1\.[0-9]+\.[0-9]+$
    argocd-image-updater.argoproj.io/write-back-method: git
```

---

## ArgoCD vs Flux

Both are GitOps controllers. Key differences:

| Feature | ArgoCD | Flux v2 |
|---------|--------|---------|
| UI | Rich web UI | CLI-only (third-party UIs available) |
| Multi-cluster | Built-in | Via fleet management (Weave GitOps) |
| Application CRD | `Application` / `ApplicationSet` | `Kustomization` / `HelmRelease` |
| Sync model | Pull + webhook | Pull only |
| RBAC | Rich, SSO support | Kubernetes RBAC |
| Notification | Built-in (Slack, GitHub, etc.) | External (requires notification controller) |
| Complexity | More concepts to learn | Simpler, more UNIX-like |
| Adoption | Very high | High |

**Choose ArgoCD** if you need a UI, multi-cluster management, or are starting fresh.
**Choose Flux** if you prefer simpler CLI-based workflows or are in a GitOps-first team.

---

## CI/CD Integration

### Full GitOps Pipeline

```
Developer pushes code
        ↓
CI pipeline (GitHub Actions, GitLab CI, Jenkins):
  - Build & test
  - Build Docker image
  - Push image to registry (ECR/GCR)
  - Update image tag in GitOps repo (git commit)
        ↓
ArgoCD detects config repo change
        ↓
ArgoCD renders Helm chart with new image tag
        ↓
ArgoCD applies to Kubernetes cluster
        ↓
Health check passes → Deployment complete
```

### Updating Image Tag in GitOps Repo

```bash
# In CI pipeline (after docker push)
git clone https://github.com/myorg/gitops-repo
cd gitops-repo

# Update the image tag
yq e ".image.tag = \"${IMAGE_TAG}\"" -i environments/production/myapp/values.yaml

git config user.email "ci@myorg.com"
git config user.name "CI Bot"
git commit -am "chore: update myapp to ${IMAGE_TAG}"
git push
```

---

## Notifications

ArgoCD Notifications sends alerts to Slack, Teams, GitHub, PagerDuty, etc.:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: argocd-notifications-cm
data:
  trigger.on-sync-failed: |
    - when: app.status.operationState.phase in ['Error', 'Failed']
      send: [app-sync-failed]
  template.app-sync-failed: |
    message: |
      Application {{.app.metadata.name}} sync failed.
      Error: {{.app.status.operationState.message}}
  service.slack: |
    token: $slack-token
    username: ArgoCD
    icon: ":argo:"
```

---

## Security Best Practices

### Least Privilege

- Each team manages applications in their namespace only
- ArgoCD project restrictions: which repos and clusters each project can access

```yaml
apiVersion: argoproj.io/v1alpha1
kind: AppProject
metadata:
  name: team-payments
spec:
  sourceRepos:
  - https://github.com/myorg/payments-*   # only payments repos
  destinations:
  - namespace: payments-*                  # only payments namespaces
    server: https://kubernetes.default.svc
  clusterResourceWhitelist: []             # no cluster-level resources
  namespaceResourceWhitelist:
  - group: apps
    kind: Deployment
  - group: ""
    kind: Service
```

### SSO Integration

ArgoCD supports OIDC, LDAP, GitHub OAuth, Google, Okta via Dex.

### Repo Server Isolation

ArgoCD Repo Server executes Helm/Kustomize plugins. Run it in an isolated pod with limited permissions.

---

## Production Checklist

- [ ] App of Apps or ApplicationSet manages all applications (no manual Application creation)
- [ ] `syncPolicy.automated.selfHeal: true` to auto-correct drift
- [ ] `syncPolicy.automated.prune: true` to remove deleted resources
- [ ] Separate Git repo for GitOps config (not mixed with application code)
- [ ] Image tag updates via automated commit (not manual)
- [ ] ArgoCD SSO configured (disable local admin user in production)
- [ ] AppProject restrictions per team/environment
- [ ] Notifications configured for sync failures and health degradation
- [ ] ArgoCD itself managed by ArgoCD (dogfood GitOps)
- [ ] etcd backup includes ArgoCD Application state
- [ ] Resource hooks for database migrations (`pre-sync` hooks)

---

## Next Steps

- [CI/CD Patterns](./ci-cd-patterns.md) — pipeline design for GitOps workflows
- [Helm Charts](./helm-charts.md) — packaging what ArgoCD deploys
- [Kubernetes](../containerization/kubernetes.md) — the platform being deployed to
