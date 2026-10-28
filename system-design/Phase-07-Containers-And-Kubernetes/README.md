# Phase 7 — Containers and Kubernetes

> **Status:** Not Started

## Overview

Containers and Kubernetes are the standard deployment and orchestration platform for cloud-native applications. This phase covers Docker internals and best practices, then Kubernetes from fundamentals to production operations.

## Sections

1. [Docker](./01-docker.md)  
   Docker internals, image layers, networking, volumes, multi-stage builds, and security hardening.

2. [Kubernetes](./02-kubernetes.md)  
   Pods, Deployments, StatefulSets, Services, Ingress, ConfigMaps, Secrets, HPA/VPA, scheduling, Operators, Helm, service mesh, and security.

## Learning Objectives

By the end of this phase, you should be able to:

- Write an optimized, multi-stage Dockerfile for a .NET application with security hardening
- Deploy a stateless service on Kubernetes with HPA, PDB, and readiness/liveness probes configured
- Configure network policies to implement a default-deny-all security posture
- Explain the difference between a Deployment and a StatefulSet and when to use each
- Write a Helm chart for a .NET microservice with environment-specific values

## Suggested Order

Docker (1) first to understand the container primitive, then Kubernetes (2) which orchestrates containers.
