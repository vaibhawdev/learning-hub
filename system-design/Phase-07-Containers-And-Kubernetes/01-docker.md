# Docker

> **Phase:** 7 — Containers and Kubernetes  
> **Status:** Not Started

## Overview

Docker containers are the standard packaging and runtime unit for cloud-native applications. This section goes beyond basic `docker run` to cover the Linux primitives that make containers work, how to build lean, secure images, and the networking model that enables container communication.

## Topics

### Docker Internals

**What it is:** Docker containers are Linux namespaces + cgroups — not VMs. Namespaces provide isolation (PID, network, mount, UTS, IPC); cgroups provide resource limits (CPU, memory).

**Why it matters:** Understanding that containers are processes with isolation (not mini-VMs) explains their startup speed, their shared kernel, and security implications.

**Key concepts to understand:**
- Linux namespaces: pid, net, mnt, uts, ipc, user — each container gets its own namespace instances
- cgroups v1/v2: kernel mechanism for CPU shares, memory limits, and I/O throttling
- Container runtime: `containerd` (production) and `runc` (OCI runtime) — Docker uses these internally
- OCI (Open Container Initiative): standardized container image and runtime specifications
- Container vs VM: containers share host kernel — faster startup, smaller footprint, weaker isolation
- `docker run --memory 512m --cpus 0.5`: applying resource constraints via cgroups

**Resources to explore:**
- [ ] (to be filled)

---

### Images / Layers

**What it is:** Docker images are stacked, read-only layers forming a union filesystem — each layer represents a change set from a `RUN`, `COPY`, or `ADD` Dockerfile instruction.

**Why it matters:** Understanding layer caching is essential for fast builds — ordering Dockerfile instructions to maximize cache hits reduces build time from minutes to seconds.

**Key concepts to understand:**
- Union filesystem (OverlayFS): layers merged into a single coherent filesystem view
- Layer caching: Docker caches each instruction layer — invalidated when any instruction or its inputs change
- Cache invalidation: a changed instruction invalidates all subsequent layers — put frequently changing instructions last
- Base image layers: `FROM mcr.microsoft.com/dotnet/aspnet:8.0` adds pre-built runtime layers
- Image size: each layer adds to image size — combine RUN commands with `&&` to reduce layers
- Image digest: immutable SHA256 hash of image content — use for reproducible deployments

**Resources to explore:**
- [ ] (to be filled)

---

### Networking

**What it is:** Docker provides network drivers (bridge, host, overlay, none) that determine how containers communicate with each other and the outside world.

**Why it matters:** Container networking is the plumbing for multi-container applications — understanding bridge networks and DNS resolution prevents connectivity issues in Compose and Kubernetes.

**Key concepts to understand:**
- Bridge network (default): Docker creates a virtual bridge, each container gets a virtual NIC with a private IP
- User-defined bridge networks: containers on the same named network can resolve each other by container name
- Host network: container shares host network namespace — maximum performance, minimum isolation
- Overlay network (Docker Swarm): spans multiple Docker hosts — Kubernetes uses its own CNI instead
- DNS resolution: Docker's embedded DNS resolves container names to IPs on user-defined networks
- Port mapping: `-p hostPort:containerPort` adds iptables NAT rules to expose container ports

**Resources to explore:**
- [ ] (to be filled)

---

### Volumes

**What it is:** Docker volumes are managed storage that persists beyond container lifecycle — used for databases, file uploads, and any data that must survive container restarts or replacements.

**Why it matters:** Containers are ephemeral — everything written to the container filesystem is lost on replacement. Volumes and bind mounts solve this, but they have different use cases.

**Key concepts to understand:**
- Named volumes: managed by Docker (`docker volume create`), stored in `/var/lib/docker/volumes`
- Bind mounts: host directory mounted into container — for development live-reload scenarios
- Volume drivers: local, NFS, Azure Files — pluggable backends for storage
- tmpfs mounts: in-memory filesystem — for sensitive data that shouldn't be written to disk
- Volume lifetime: volumes persist after container removal — must be explicitly deleted
- Database containers: always use volumes for database data directories

**Resources to explore:**
- [ ] (to be filled)

---

### Multi-Stage Builds

**What it is:** Dockerfile pattern that uses multiple `FROM` stages — typically a large build stage (SDK) and a small runtime stage (ASP.NET runtime) — resulting in much smaller production images.

**Why it matters:** A .NET app built with the full SDK image is ~800MB; multi-stage reduces it to ~100MB — smaller images mean faster pulls, less attack surface, and less storage cost.

**Key concepts to understand:**
- Build stage: `FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build` — has full SDK, build tools
- Runtime stage: `FROM mcr.microsoft.com/dotnet/aspnet:8.0` — only ASP.NET runtime, no SDK
- `COPY --from=build /app/out .`: copy artifacts from build stage to runtime stage
- Chiseled images: ultra-minimal .NET base images without shell or package manager
- `COPY --from=build` can reference any named or numbered stage
- Layer optimization in multi-stage: restore NuGet packages as a separate layer before copying source

**Resources to explore:**
- [ ] (to be filled)

---

### Security Hardening

**What it is:** Practices for reducing the attack surface of Docker containers and images — running as non-root, scanning for vulnerabilities, using minimal base images, and limiting capabilities.

**Why it matters:** Containers run as root by default — a container escape from a root container gives full host access; security hardening limits the blast radius of a container compromise.

**Key concepts to understand:**
- Non-root user: `USER app` in Dockerfile — never run production containers as root
- Read-only filesystem: `--read-only` flag — prevents attackers from writing tools or scripts
- Dropped capabilities: `--cap-drop ALL --cap-add NET_BIND_SERVICE` — minimum required Linux capabilities
- Seccomp profile: limits available syscalls — Docker default profile blocks ~44 dangerous syscalls
- Image vulnerability scanning: Trivy, Snyk, Azure Container Registry vulnerability assessment
- Distroless images: no shell, no package manager — drastically reduced attack surface

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Kubernetes](./02-kubernetes.md)
- [Cloud Security](../Phase-10-Security-Engineering/03-cloud-security.md)
- [AKS](../Phase-06-Cloud-And-Azure/01-azure-core.md)
