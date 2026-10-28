# Phase 6 — Cloud and Azure

> **Status:** Not Started

## Overview

Azure is the deployment target for most .NET enterprise workloads. This phase covers the core Azure services, cloud architecture patterns, and infrastructure-as-code practices needed to design, deploy, and operate production-grade cloud systems.

## Sections

1. [Azure Core](./01-azure-core.md)  
   Resource Groups, VNets, NSGs, Managed Identity, Key Vault, Private Endpoints, App Service, AKS, Service Bus, Event Grid, Cosmos DB, Azure SQL, Redis, and Application Insights.

2. [Cloud Architecture](./02-cloud-architecture.md)  
   High availability, fault tolerance, multi-region, DR strategies, cost optimization, autoscaling, multi-tenancy, and zero trust.

3. [Infrastructure as Code](./03-infrastructure-as-code.md)  
   Terraform, Bicep, Pulumi, GitOps, and Policy as Code.

## Learning Objectives

By the end of this phase, you should be able to:

- Design an Azure VNet topology with appropriate subnets and NSG rules for a multi-tier application
- Configure Managed Identity for an App Service to access Key Vault and Cosmos DB without credentials
- Write Bicep or Terraform to deploy a complete Azure environment from scratch
- Calculate RTO/RPO requirements and select an appropriate DR strategy
- Design a multi-tenant SaaS data isolation model using EF Core query filters

## Suggested Order

Azure Core (1) provides the service knowledge, Cloud Architecture (2) covers the patterns that use those services, and IaC (3) is how you provision it all reliably.
