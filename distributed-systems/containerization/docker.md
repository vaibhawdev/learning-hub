# Docker

## What Is Docker?

Docker is a platform for building, shipping, and running applications in **containers** — lightweight, isolated environments that package an application with all its dependencies (code, runtime, libraries, config). A container runs the same everywhere: your laptop, CI, staging, and production.

```
Without Docker:
"It works on my machine"
Developer → (different Python version, missing lib, different OS) → Production failure

With Docker:
Developer → Container Image → Runs identically everywhere
```

---

## Key Concepts

### Container vs Virtual Machine

| | Container | Virtual Machine |
|-|-----------|-----------------|
| Isolation | Process-level (shared OS kernel) | Full OS virtualization |
| Startup time | Milliseconds | Seconds to minutes |
| Size | Megabytes | Gigabytes |
| Performance | Near-native | 10–20% overhead |
| Security | Weaker isolation | Stronger isolation |
| Use case | Application packaging | Full OS isolation, multi-tenant |

### Image

An image is a read-only, layered filesystem snapshot. It contains:
- The base OS layer (e.g., `alpine`, `debian`)
- Application runtime (e.g., Python, Node.js, JVM)
- Your application code
- Configuration files

Images are built from a `Dockerfile` and stored in a registry (Docker Hub, ECR, GCR).

### Container

A container is a running instance of an image — a process with isolated filesystem, network, and process space. Multiple containers from the same image are independent.

### Layer Caching

Each `RUN`, `COPY`, and `ADD` instruction in a Dockerfile creates a new layer. Docker caches layers — if a layer hasn't changed, it is reused on rebuild.

```dockerfile
# Layer 1: base
FROM python:3.12-slim

# Layer 2: deps (cached until requirements.txt changes)
COPY requirements.txt .
RUN pip install -r requirements.txt

# Layer 3: app code (rebuilds frequently)
COPY . .
```

Order instructions from least to most frequently changing to maximize cache hits.

---

## Dockerfile Best Practices

### Minimal Base Images

```dockerfile
# Bad: full Ubuntu (900MB)
FROM ubuntu:22.04

# Better: slim variant (120MB)
FROM python:3.12-slim

# Best: Alpine (50MB, but uses musl libc — test compatibility)
FROM python:3.12-alpine

# For static binaries: scratch (0MB)
FROM scratch
```

### Multi-Stage Builds

Build artifacts in one stage; copy only the output to a minimal runtime image.

```dockerfile
# Stage 1: Build
FROM golang:1.22 AS builder
WORKDIR /app
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -o server ./cmd/server

# Stage 2: Run (no compiler, no source code)
FROM gcr.io/distroless/static-debian12
COPY --from=builder /app/server /server
ENTRYPOINT ["/server"]
```

Benefits:
- Final image contains only the runtime artifact (small, secure)
- Build tools and secrets used during build are not in the final image
- Typical size reduction: 1GB build image → 20MB runtime image

### Non-Root User

```dockerfile
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
```

Running as root inside a container is a security risk. Always run as a non-root user.

### .dockerignore

```
.git
node_modules
*.log
.env
__pycache__
*.pyc
tests/
docs/
```

Exclude files that should not be in the build context. Reduces build time and image size.

### Explicit Tags

```dockerfile
# Bad: mutable "latest"
FROM node:latest

# Good: pinned version
FROM node:20.11.1-slim
```

`latest` changes without notice, breaking reproducibility. Pin to specific versions.

### COPY vs ADD

Use `COPY` by default. Only use `ADD` for:
- Automatically extracting local `.tar.gz` archives
- Fetching from URLs (better to use `curl` explicitly for visibility)

### ENTRYPOINT vs CMD

```dockerfile
# ENTRYPOINT: fixed executable
ENTRYPOINT ["node"]
# CMD: default arguments (can be overridden at runtime)
CMD ["server.js"]

# Together: node server.js (CMD overridable)
# docker run image worker.js → runs: node worker.js
```

Use `exec form` (`["command"]`) not `shell form` (`command`) to avoid signal handling issues.

---

## Docker Networking

### Default Networks

| Network | Description |
|---------|-------------|
| `bridge` | Default. Containers on same bridge can communicate by IP. |
| `host` | Container shares host network namespace (no isolation). |
| `none` | No networking. |
| `overlay` | Multi-host networking (Docker Swarm / Kubernetes). |

### Custom Bridge Networks

```bash
docker network create myapp
docker run --network myapp --name db postgres
docker run --network myapp --name api myapi
# api can reach db as "db:5432"
```

On a custom bridge network, containers can communicate by name (DNS resolution). The default bridge network uses only IP addresses.

---

## Docker Volumes

Persistent storage that survives container restarts and is shared between containers.

### Types

| Type | Description | Use Case |
|------|-------------|---------|
| **Named volume** | Managed by Docker, stored in `/var/lib/docker/volumes/` | Database data, stateful apps |
| **Bind mount** | Maps host directory into container | Development (live code reload) |
| **tmpfs mount** | In-memory, never written to disk | Sensitive temp files |

```bash
# Named volume
docker run -v postgres_data:/var/lib/postgresql/data postgres

# Bind mount
docker run -v $(pwd)/src:/app/src myapp

# tmpfs
docker run --tmpfs /app/tmp:rw,size=100m myapp
```

**Never use bind mounts in production** — they tie the container to the host filesystem.

---

## Docker Compose

Defines and runs multi-container applications. Define all services in `docker-compose.yml`.

```yaml
version: "3.9"
services:
  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      - DATABASE_URL=postgres://user:pass@db:5432/mydb
    depends_on:
      db:
        condition: service_healthy
    networks:
      - backend

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: user
      POSTGRES_PASSWORD: pass
      POSTGRES_DB: mydb
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U user"]
      interval: 5s
      retries: 5
    networks:
      - backend

volumes:
  postgres_data:

networks:
  backend:
    driver: bridge
```

```bash
docker compose up -d       # start all services detached
docker compose logs -f api # tail logs
docker compose down -v     # stop and remove volumes
```

---

## Security Hardening

### Image Scanning

Scan images for known CVEs before deployment:

```bash
# Docker Scout (built-in)
docker scout cves myimage:tag

# Trivy (open-source, widely used)
trivy image myimage:tag

# Snyk
snyk container test myimage:tag
```

Integrate scanning into CI pipeline. Fail builds on critical CVEs.

### Read-Only Filesystem

```dockerfile
# In docker run or compose
--read-only
# Or in Kubernetes: readOnlyRootFilesystem: true
```

Mount writable volumes only for directories that need writes.

### Drop Linux Capabilities

```bash
docker run --cap-drop=ALL --cap-add=NET_BIND_SERVICE myapp
```

Containers inherit capabilities. Drop all and add back only what's needed.

### Secrets Management

```bash
# Bad: environment variables in plain text
docker run -e PASSWORD=secret myapp

# Better: Docker secrets (Swarm mode)
echo "mysecret" | docker secret create db_password -
docker service create --secret db_password myapp

# Best: Mount from vault at runtime
docker run -v /secrets/db_password:/run/secrets/db_password myapp
```

Never embed secrets in images. Never log environment variables.

### Distroless Images

Google's distroless images contain only the application and its runtime dependencies — no shell, no package manager, no debugging tools.

```dockerfile
FROM gcr.io/distroless/java21-debian12
COPY app.jar /app.jar
ENTRYPOINT ["java", "-jar", "/app.jar"]
```

Attack surface is minimal. Can be harder to debug — use a debug variant for investigation.

---

## Container Registries

| Registry | Notes |
|----------|-------|
| **Docker Hub** | Public default. Rate-limited pulls for anonymous users. |
| **Amazon ECR** | AWS-native, integrated with ECS/EKS. |
| **Google Artifact Registry** | GCP-native, replaces Container Registry. |
| **Azure Container Registry** | Azure-native. |
| **GitHub Container Registry (GHCR)** | Integrated with GitHub Actions. |
| **Harbor** | Self-hosted, open-source, includes vulnerability scanning. |
| **Quay.io** | Red Hat's registry, includes Clair scanning. |

---

## Image Tagging Strategy

```
myapp:latest          # ← avoid in production
myapp:1.4.2           # semantic version
myapp:1.4.2-alpine    # version + base image variant
myapp:sha-a1b2c3d4    # git commit SHA (most reproducible)
myapp:2026-06-06      # date-based
```

**Production strategy**: Tag with git SHA + semantic version. Never deploy `latest` in production.

---

## Things to Watch in Production

| Concern | What to Check |
|---------|--------------|
| Image bloat | Run `docker history image` to find large layers |
| Running as root | `docker inspect --format '{{.Config.User}}' image` |
| Secret in image | `docker history --no-trunc` and `docker inspect` for ENV vars |
| Old images accumulating | `docker image prune` scheduled job on hosts |
| Log driver | Default is `json-file` — unbounded; switch to `journald` or centralized logging |
| Resource limits | Set `--memory` and `--cpus` to prevent container from starving host |

---

## Production Checklist

- [ ] Multi-stage build reduces image size
- [ ] Non-root user (`USER appuser`)
- [ ] `.dockerignore` excludes build artifacts and secrets
- [ ] Explicit version tags (no `latest`)
- [ ] Base image scanned for CVEs in CI
- [ ] No secrets in environment variables or image layers
- [ ] Health check defined (`HEALTHCHECK` or K8s probes)
- [ ] Resource limits set (memory, CPU)
- [ ] Read-only root filesystem where possible
- [ ] Container logs sent to centralized log aggregator
- [ ] Image pushed to private registry (not Docker Hub for proprietary code)

---

## Next Steps

- [Kubernetes](./kubernetes.md) — orchestrating containers at scale
- [Helm Charts](../devops/helm-charts.md) — packaging Kubernetes deployments
- [Security](../18-security.md) — container security in depth
