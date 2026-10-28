# CI/CD Patterns

## What Is CI/CD?

**Continuous Integration (CI)**: Every code change is automatically built, tested, and validated. Catches bugs early.

**Continuous Delivery (CD)**: Every validated change is automatically deployable to production with one click.

**Continuous Deployment**: Every validated change is automatically deployed to production without human approval.

```
Code Push
    ↓
Build (compile, package)
    ↓
Unit Tests
    ↓
Integration Tests
    ↓
Static Analysis (lint, SAST)
    ↓
Container Build & Scan
    ↓
Push to Registry
    ↓
Deploy to Staging
    ↓
Smoke Tests / E2E Tests
    ↓
[Manual Gate] ← Continuous Delivery stops here
    ↓
Deploy to Production  ← Continuous Deployment continues here
    ↓
Monitor & Alert
```

---

## CI/CD Tools Comparison

| Tool | Hosted | Self-hosted | K8s-native | Best For |
|------|--------|-------------|------------|---------|
| **GitHub Actions** | ✓ | ✓ (self-hosted runners) | Partial | GitHub-hosted repos |
| **GitLab CI** | ✓ | ✓ | ✓ | GitLab monorepo, K8s integration |
| **Jenkins** | ✗ | ✓ | ✓ (Jenkins X) | Enterprise, full control |
| **CircleCI** | ✓ | ✓ | Partial | Fast builds, orbs ecosystem |
| **Tekton** | ✗ | ✓ | ✓ (native) | K8s-native pipelines |
| **ArgoCD + Image Updater** | ✗ | ✓ | ✓ | GitOps CD |
| **Argo Workflows** | ✗ | ✓ | ✓ | Complex DAG pipelines |
| **Drone** | ✓ | ✓ | ✓ | Lightweight, Docker-native |
| **TeamCity** | ✓ | ✓ | Partial | JetBrains ecosystem |
| **Buildkite** | ✓ | Runners only | Partial | Large-scale, fast |

---

## Deployment Strategies

### Rolling Update

The default Kubernetes strategy. Replace old pods with new ones gradually.

```
Start: [v1][v1][v1][v1]
Step1: [v2][v1][v1][v1]
Step2: [v2][v2][v1][v1]
Step3: [v2][v2][v2][v1]
Done:  [v2][v2][v2][v2]
```

- Zero-downtime by default
- At any point, both versions are running — ensure backward compatibility
- Slow rollout for large deployments
- Automatic rollback on readiness probe failure

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 25%         # extra pods created
    maxUnavailable: 25%   # max pods taken down at once
```

### Blue-Green Deployment

Run two identical environments. Switch traffic instantaneously.

```
Before:
  Traffic → Blue (v1)    Green (idle, v2)

After switch:
  Traffic → Blue (idle, v1)    Green (v2)
```

**Implementation with Kubernetes:**
```yaml
# Blue deployment: version: blue
# Green deployment: version: green
# Service selector switches between them

kubectl patch service myapp \
  -p '{"spec":{"selector":{"version":"green"}}}'
```

**Pros**: Instant cutover, instant rollback (switch back to blue).
**Cons**: Requires 2x infrastructure; database migration must be compatible with both versions.

### Canary Deployment

Route a small percentage of traffic to the new version. Gradually increase if metrics look good.

```
100% → v1
  5% → v2, 95% → v1  (watch error rates, latency)
 25% → v2, 75% → v1
 50% → v2, 50% → v1
100% → v2
```

**With Istio:**
```yaml
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
spec:
  http:
  - match:
    - headers:
        x-canary:
          exact: "true"
    route:
    - destination:
        host: myapp-v2
  - route:
    - destination:
        host: myapp-v1
      weight: 95
    - destination:
        host: myapp-v2
      weight: 5
```

**With Argo Rollouts:**
```yaml
apiVersion: argoproj.io/v1alpha1
kind: Rollout
spec:
  strategy:
    canary:
      steps:
      - setWeight: 5
      - pause: {duration: 5m}      # analyze metrics
      - setWeight: 25
      - pause: {duration: 10m}
      - setWeight: 100
      analysis:
        templates:
        - templateName: error-rate  # auto-abort if error rate spikes
```

### Feature Flags

Deploy code to 100% of users but control feature activation at runtime:

```python
if feature_flag.is_enabled("new-checkout-flow", user_id=user.id):
    return new_checkout()
else:
    return old_checkout()
```

Tools: LaunchDarkly, Unleash, Flagsmith, Split.io, OpenFeature (standard).

Feature flags enable:
- Dark launches (deploy without exposing)
- A/B testing
- Percentage rollouts independent of deployments
- Emergency kill switches

---

## GitHub Actions Example

### Full Build + Deploy Pipeline

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy

on:
  push:
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with:
        node-version: '20'
        cache: 'npm'
    - run: npm ci
    - run: npm test
    - run: npm run lint

  build:
    needs: test
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    outputs:
      image-tag: ${{ steps.meta.outputs.tags }}
      image-digest: ${{ steps.push.outputs.digest }}
    steps:
    - uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Login to registry
      uses: docker/login-action@v3
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}

    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
        tags: |
          type=sha,prefix=sha-
          type=semver,pattern={{version}}

    - name: Build and push
      id: push
      uses: docker/build-push-action@v5
      with:
        context: .
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

    - name: Run Trivy vulnerability scanner
      uses: aquasecurity/trivy-action@master
      with:
        image-ref: ${{ steps.meta.outputs.tags }}
        exit-code: '1'
        severity: 'CRITICAL'

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    environment: staging
    steps:
    - uses: actions/checkout@v4
      with:
        repository: myorg/gitops-repo
        token: ${{ secrets.GITOPS_TOKEN }}

    - name: Update staging image tag
      run: |
        IMAGE_TAG=$(echo "${{ needs.build.outputs.image-tag }}" | grep sha | head -1)
        yq e ".image.tag = \"${IMAGE_TAG}\"" -i environments/staging/myapp/values.yaml
        git config user.email "ci@myorg.com"
        git config user.name "GitHub Actions"
        git commit -am "chore: update myapp staging to ${IMAGE_TAG}"
        git push

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production   # requires manual approval in GitHub
    steps:
    - uses: actions/checkout@v4
      with:
        repository: myorg/gitops-repo
        token: ${{ secrets.GITOPS_TOKEN }}

    - name: Update production image tag
      run: |
        IMAGE_TAG=$(echo "${{ needs.build.outputs.image-tag }}" | grep sha | head -1)
        yq e ".image.tag = \"${IMAGE_TAG}\"" -i environments/production/myapp/values.yaml
        git commit -am "chore: update myapp production to ${IMAGE_TAG}"
        git push
```

---

## Database Migrations in CI/CD

### The Challenge

Database migrations must run before the new application version starts. Rolling deployments mean old and new code run simultaneously — migrations must be backward-compatible.

### Three-Phase Migration (Expand-Contract Pattern)

```
Phase 1 (Expand): Add new column (nullable), old code ignores it
Phase 2: Deploy new code that reads/writes new column
Phase 3 (Contract): Remove old column, data fully migrated
```

Never: drop a column that the old code still reads in the same deployment.

### Running Migrations with Kubernetes

```yaml
# ArgoCD pre-sync hook (runs before deployment)
apiVersion: batch/v1
kind: Job
metadata:
  annotations:
    argocd.argoproj.io/hook: PreSync
    argocd.argoproj.io/hook-delete-policy: HookSucceeded
spec:
  template:
    spec:
      containers:
      - name: migrate
        image: myapp:{{ .Values.image.tag }}
        command: ["./migrate", "up"]
        env:
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: url
      restartPolicy: Never
```

---

## Environment Promotion

```
Feature Branch → Dev → Staging → Production
                 ↑        ↑           ↑
              auto      auto      manual gate
```

### Promotion Patterns

1. **Branch-based**: Each environment tracks a branch (`dev`, `staging`, `main`)
2. **Tag-based**: Production only deploys tagged releases (`v1.2.3`)
3. **PR-based**: Promotion is a PR merge in the GitOps repo
4. **Image digest promotion**: Promote by copying the exact image digest between values files

```bash
# Promote exact image from staging to production (no rebuilding)
STAGING_DIGEST=$(yq e '.image.digest' environments/staging/myapp/values.yaml)
yq e ".image.digest = \"${STAGING_DIGEST}\"" -i environments/production/myapp/values.yaml
```

---

## Pipeline Security

### Secrets Management in CI

```yaml
# Never hardcode secrets:
env:
  API_KEY: "sk-1234"  # BAD

# Use CI secrets:
env:
  API_KEY: ${{ secrets.API_KEY }}  # GitHub Actions

# Better: fetch from Vault at runtime
steps:
- uses: hashicorp/vault-action@v2
  with:
    url: https://vault.myorg.com
    method: jwt
    secrets: |
      secret/data/myapp/prod api_key | API_KEY;
```

### Supply Chain Security (SLSA)

```yaml
# Sign images with Sigstore/Cosign
- name: Sign image
  uses: sigstore/cosign-installer@v3
- run: |
    cosign sign --yes ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}@${{ steps.push.outputs.digest }}

# Generate SBOM (Software Bill of Materials)
- uses: anchore/sbom-action@v0
  with:
    image: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
```

### Dependency Scanning

```yaml
# Dependabot (GitHub) - automatic dependency updates
# .github/dependabot.yml
version: 2
updates:
- package-ecosystem: "npm"
  directory: "/"
  schedule:
    interval: "weekly"
- package-ecosystem: "docker"
  directory: "/"
  schedule:
    interval: "weekly"
```

---

## Observability in CI/CD

Track deployment frequency, lead time, change failure rate, and MTTR (DORA metrics):

| DORA Metric | Target (Elite) | How to Measure |
|-------------|---------------|----------------|
| Deployment frequency | Multiple times/day | Count pipeline runs |
| Lead time for changes | <1 hour | Commit timestamp → production deploy |
| Change failure rate | <5% | Rollbacks / total deploys |
| MTTR | <1 hour | Incident start → resolution |

---

## Production Checklist

- [ ] Tests run on every PR and must pass before merge
- [ ] Image built once, promoted through environments (no rebuilds)
- [ ] Image digest used for production deployments (not mutable tags)
- [ ] Vulnerability scanning on every image build
- [ ] SBOM generated and stored with each release
- [ ] Secrets fetched from vault, not stored in CI environment variables
- [ ] Database migrations are backward-compatible
- [ ] Staging environment mirrors production topology
- [ ] Manual approval gate before production deployment
- [ ] Rollback procedure documented and tested
- [ ] DORA metrics tracked
- [ ] Deployment notifications to Slack/Teams/PagerDuty

---

## Next Steps

- [ArgoCD & GitOps](./argocd-gitops.md) — the CD side of the pipeline
- [Helm Charts](./helm-charts.md) — packaging for CI/CD
- [Observability](../16-observability.md) — monitoring post-deployment
- [Security](../18-security.md) — securing the CI/CD pipeline
