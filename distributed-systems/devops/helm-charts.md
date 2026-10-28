# Helm Charts

## What Is Helm?

Helm is the package manager for Kubernetes. It allows you to define, install, and upgrade complex Kubernetes applications as versioned packages called **charts**. Instead of managing 20 individual YAML files, you manage one chart with configurable values.

```
Without Helm:
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
kubectl apply -f configmap.yaml
kubectl apply -f ingress.yaml
# Different values for each environment = duplicated YAML

With Helm:
helm install myapp ./mychart --values prod-values.yaml
```

---

## Core Concepts

### Chart

A chart is a collection of files describing a related set of Kubernetes resources. Chart structure:

```
mychart/
├── Chart.yaml          # Chart metadata (name, version, description)
├── values.yaml         # Default configuration values
├── values.schema.json  # (optional) JSON Schema validation for values
├── charts/             # Dependency charts
├── templates/          # Kubernetes manifest templates
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── _helpers.tpl    # Template helper functions
│   ├── NOTES.txt       # Post-install instructions
│   └── tests/
│       └── test-connection.yaml
└── .helmignore
```

### Release

A **release** is a deployed instance of a chart. The same chart can be installed multiple times as different releases (e.g., `myapp-staging` and `myapp-prod`).

### Repository

A chart repository is an HTTP server hosting packaged charts (`.tgz` files) and an `index.yaml`. Helm Hub / Artifact Hub aggregates public repos.

---

## Chart.yaml

```yaml
apiVersion: v2          # v2 = Helm 3
name: myapp
description: A Helm chart for myapp
type: application       # application or library
version: 1.4.2          # Chart version (SemVer)
appVersion: "2.3.1"     # Application version (informational)

dependencies:
- name: postgresql
  version: "13.x.x"
  repository: "https://charts.bitnami.com/bitnami"
  condition: postgresql.enabled  # enable/disable via values
```

---

## Templates and Values

### values.yaml

```yaml
replicaCount: 2

image:
  repository: myapp
  tag: "1.0.0"
  pullPolicy: IfNotPresent

service:
  type: ClusterIP
  port: 80

ingress:
  enabled: false
  host: myapp.example.com

resources:
  requests:
    cpu: 250m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: false
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
```

### Deployment Template

```yaml
# templates/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "myapp.fullname" . }}
  labels:
    {{- include "myapp.labels" . | nindent 4 }}
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  selector:
    matchLabels:
      {{- include "myapp.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      labels:
        {{- include "myapp.selectorLabels" . | nindent 8 }}
    spec:
      containers:
      - name: {{ .Chart.Name }}
        image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
        imagePullPolicy: {{ .Values.image.pullPolicy }}
        ports:
        - containerPort: 8080
        resources:
          {{- toYaml .Values.resources | nindent 12 }}
```

### _helpers.tpl (Named Templates)

```yaml
# templates/_helpers.tpl
{{- define "myapp.fullname" -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "myapp.labels" -}}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "myapp.selectorLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
```

---

## Template Functions

Helm uses the Go template engine plus the Sprig function library:

```yaml
# String manipulation
{{ .Values.name | upper }}
{{ .Values.name | lower | trimSuffix "-" | trunc 63 }}
{{ printf "%s-%s" .Release.Name .Chart.Name }}

# Conditionals
{{- if .Values.ingress.enabled }}
  # ingress manifest
{{- end }}

{{- if eq .Values.environment "production" }}
  replicas: 5
{{- else }}
  replicas: 1
{{- end }}

# Default values
{{ .Values.image.tag | default .Chart.AppVersion | quote }}

# toYaml: embed YAML blocks
resources:
  {{- toYaml .Values.resources | nindent 10 }}

# Range: iterate over lists
{{- range .Values.env }}
- name: {{ .name }}
  value: {{ .value | quote }}
{{- end }}

# Required: fail if value not provided
{{ required "image.repository is required" .Values.image.repository }}
```

---

## Values Override Hierarchy

Helm merges values in this priority order (highest to lowest):

```
1. --set flag (command line)           ← highest priority
2. --set-string, --set-file flags
3. -f custom-values.yaml files (right-most wins)
4. chart default values.yaml           ← lowest priority
```

```bash
helm install myapp ./mychart \
  -f values.yaml \
  -f prod-values.yaml \
  --set image.tag=1.2.3 \
  --set replicaCount=5
```

### Environment-Specific Values

```
values/
├── values.yaml          # defaults
├── values-dev.yaml      # development overrides
├── values-staging.yaml  # staging overrides
└── values-prod.yaml     # production overrides
```

```bash
helm upgrade --install myapp ./mychart -f values/values-prod.yaml
```

---

## Lifecycle Hooks

Hooks run Kubernetes jobs at specific points in the release lifecycle:

```yaml
# templates/db-migration.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: db-migrate
  annotations:
    "helm.sh/hook": pre-upgrade,pre-install
    "helm.sh/hook-weight": "-5"          # lower = runs first
    "helm.sh/hook-delete-policy": before-hook-creation,hook-succeeded
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: migrate
        image: myapp:{{ .Values.image.tag }}
        command: ["./migrate", "up"]
```

| Hook | When |
|------|------|
| `pre-install` | Before any resources created on install |
| `post-install` | After all resources created on install |
| `pre-upgrade` | Before upgrade starts |
| `post-upgrade` | After upgrade completes |
| `pre-rollback` | Before rollback |
| `post-rollback` | After rollback |
| `pre-delete` | Before any resources deleted on uninstall |

---

## Chart Dependencies

### Declaring Dependencies

```yaml
# Chart.yaml
dependencies:
- name: redis
  version: "18.x.x"
  repository: "https://charts.bitnami.com/bitnami"
  condition: redis.enabled
- name: postgresql
  version: "13.x.x"
  repository: "https://charts.bitnami.com/bitnami"
  condition: postgresql.enabled
```

```bash
helm dependency update ./mychart   # downloads deps to charts/
```

### Configuring Sub-Chart Values

```yaml
# values.yaml
redis:
  enabled: true
  auth:
    enabled: false

postgresql:
  enabled: true
  auth:
    username: myapp
    database: mydb
```

---

## Helmfile

Helmfile manages multiple Helm releases declaratively — equivalent to `docker-compose.yml` for Helm:

```yaml
# helmfile.yaml
repositories:
- name: bitnami
  url: https://charts.bitnami.com/bitnami

releases:
- name: myapp
  namespace: production
  chart: ./charts/myapp
  values:
  - values/values.yaml
  - values/values-prod.yaml
  set:
  - name: image.tag
    value: {{ requiredEnv "IMAGE_TAG" }}

- name: redis
  namespace: production
  chart: bitnami/redis
  version: "18.x.x"
  values:
  - values/redis-prod.yaml
```

```bash
helmfile sync        # deploy all releases
helmfile diff        # show diff before applying
helmfile destroy     # uninstall all releases
```

---

## OCI Registry Support

Helm 3.8+ supports storing charts in OCI registries (same as Docker images):

```bash
# Push chart to registry
helm package ./mychart
helm push mychart-1.0.0.tgz oci://registry.example.com/charts

# Install from OCI registry
helm install myapp oci://registry.example.com/charts/mychart --version 1.0.0
```

---

## Common Helm Commands

```bash
# Install or upgrade (idempotent)
helm upgrade --install myapp ./mychart \
  --namespace production \
  --create-namespace \
  -f values-prod.yaml \
  --wait \
  --timeout 5m

# List releases
helm list -n production

# Show current values
helm get values myapp -n production

# Show rendered manifests (without applying)
helm template myapp ./mychart -f values-prod.yaml

# Diff before upgrade (requires helm-diff plugin)
helm diff upgrade myapp ./mychart -f values-prod.yaml

# Rollback to previous version
helm rollback myapp 1 -n production

# Uninstall
helm uninstall myapp -n production

# Lint chart for errors
helm lint ./mychart -f values-prod.yaml
```

---

## Chart Testing

### Built-in Test

```yaml
# templates/tests/test-connection.yaml
apiVersion: v1
kind: Pod
metadata:
  name: test-connection
  annotations:
    "helm.sh/hook": test
spec:
  containers:
  - name: wget
    image: busybox
    command: ['wget', '--spider', 'http://{{ include "myapp.fullname" . }}:{{ .Values.service.port }}']
  restartPolicy: Never
```

```bash
helm test myapp -n production
```

### ct (Chart Testing)

```bash
ct lint --chart-dirs ./charts
ct install --chart-dirs ./charts
```

---

## Things to Watch

| Concern | Mitigation |
|---------|-----------|
| `helm upgrade` fails mid-way | Use `--atomic` to auto-rollback on failure |
| Values contain secrets | Use External Secrets Operator; never put secrets in values |
| Chart version drift between envs | Pin chart version in helmfile or ArgoCD |
| `--set` used in CI with special chars | Use `--set-string` or `--values` file |
| No rollback plan | Keep `helm history` and test `helm rollback` in staging |
| Large manifests in ConfigMaps | Use `helm.sh/resource-policy: keep` annotation to prevent deletion |

---

## Production Checklist

- [ ] `values.schema.json` validates required values
- [ ] Separate values files per environment (no hardcoded prod values in `values.yaml`)
- [ ] No secrets in values files — use External Secrets or Vault
- [ ] `helm upgrade --install --atomic --wait` in CI
- [ ] `helm diff` in CI to show what will change before applying
- [ ] Chart versioned and stored in OCI registry or chart repo
- [ ] `helm lint` in CI to catch template errors early
- [ ] Post-upgrade smoke test (`helm test`)
- [ ] Helm history retained for rollback

---

## Next Steps

- [ArgoCD & GitOps](./argocd-gitops.md) — deploy Helm charts via ArgoCD
- [CI/CD Patterns](./ci-cd-patterns.md) — integrate Helm into pipelines
- [Kubernetes](../containerization/kubernetes.md) — the platform Helm deploys to
