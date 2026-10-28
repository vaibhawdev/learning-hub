# Infrastructure as Code

> **Phase:** 6 — Cloud and Azure  
> **Status:** Not Started

## Overview

Infrastructure as Code (IaC) transforms infrastructure provisioning from manual, error-prone clicking into versioned, repeatable, testable code. This section covers the tools and practices for defining, deploying, and managing cloud infrastructure programmatically.

## Topics

### Terraform

**What it is:** HashiCorp's open-source IaC tool using HCL (HashiCorp Configuration Language) to declare infrastructure resources across multiple cloud providers, with a state-based apply model.

**Why it matters:** Terraform is the most widely adopted multi-cloud IaC tool — understanding its state model, plan/apply cycle, and module system is essential for managing cloud infrastructure at scale.

**Key concepts to understand:**
- Providers: plugins that implement resource CRUD operations (azurerm, aws, google)
- State file: Terraform's record of managed infrastructure — stored in remote backends (Azure Blob, Terraform Cloud)
- Plan/Apply cycle: `terraform plan` shows changes before `terraform apply` executes them
- State drift: when real infrastructure diverges from state — detected by `terraform refresh`
- Modules: reusable, parameterized infrastructure components
- Workspaces: separate state environments (dev/staging/prod) within one configuration

**Resources to explore:**
- [ ] (to be filled)

---

### Bicep

**What it is:** Microsoft's domain-specific language for Azure Resource Manager (ARM) templates — a cleaner, more concise syntax over JSON ARM templates that compiles to ARM JSON.

**Why it matters:** Bicep is the recommended IaC approach for Azure-only deployments — it has first-class Azure type checking, IDE support, and native integration with Azure deployment pipelines.

**Key concepts to understand:**
- Bicep vs ARM JSON: Bicep transpiles to ARM JSON — same deployment engine, better authoring experience
- Resource declarations: `resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01'`
- Parameters and variables: input parameters with types and defaults; computed variables
- Modules: extract reusable resource sets into module files with input/output parameters
- `az deployment group create --template-file main.bicep`: deploy from Azure CLI
- What-if: `az deployment group what-if` — preview changes before applying (like Terraform plan)

**Resources to explore:**
- [ ] (to be filled)

---

### Pulumi

**What it is:** An IaC tool that uses general-purpose programming languages (TypeScript, Python, C#, Go) instead of domain-specific languages — enabling full programming logic in infrastructure definitions.

**Why it matters:** Pulumi enables complex infrastructure logic (loops, conditionals, abstractions) without DSL limitations — particularly attractive for .NET teams using C# for infrastructure code.

**Key concepts to understand:**
- Pulumi stacks: named deployment targets (dev, staging, prod) with separate state
- Resources as objects: `new azure.storage.Account("myAccount", { ... })` in C#
- Input/Output: Pulumi's async-like model for resource property references — `Output<T>` for values known after deployment
- Stack references: reference outputs from one stack in another for cross-stack dependencies
- `pulumi preview` / `pulumi up`: equivalent to Terraform plan/apply
- State backends: Pulumi Cloud (default), Azure Blob, S3 for self-managed state

**Resources to explore:**
- [ ] (to be filled)

---

### GitOps

**What it is:** An operational model where the desired state of infrastructure and applications is declared in Git, and automated processes continuously reconcile actual state with the desired state.

**Why it matters:** GitOps makes infrastructure changes auditable, reversible, and reviewable through pull requests — aligning infrastructure management with software development practices.

**Key concepts to understand:**
- Git as the source of truth: all changes go through Git — no manual `az` commands in production
- Pull request workflow: infrastructure changes reviewed and approved before merge → auto-deploy
- Reconciliation loop: GitOps operator (Flux, ArgoCD) continuously reconciles cluster state with Git
- Flux CD: GitOps for Kubernetes — watches Git repo, applies Kubernetes manifests to cluster
- ArgoCD: GitOps controller with UI for visualizing application state vs desired state
- Drift detection: GitOps operators alert or auto-correct when cluster state diverges from Git

**Resources to explore:**
- [ ] (to be filled)

---

### Policy as Code

**What it is:** Defining compliance and security policies as machine-readable code that can be automatically enforced, tested, and audited — preventing non-compliant infrastructure from being deployed.

**Why it matters:** Manual compliance reviews don't scale — policy as code shifts security and compliance left, catching violations before deployment rather than in audits.

**Key concepts to understand:**
- Azure Policy: built-in and custom policies enforced at resource creation/modification (deny, audit, modify)
- OPA (Open Policy Agent): general-purpose policy engine using Rego language — used in Kubernetes admission, CI/CD
- Gatekeeper: OPA as a Kubernetes admission webhook — enforces policies on resource creation
- Policy as code in pipelines: run policy checks in CI (Checkov, tfsec, Semgrep for Terraform/Bicep)
- Compliance initiative: group policies into initiatives for regulatory frameworks (SOC2, ISO 27001, HIPAA)
- Terraform Sentinel: HashiCorp's policy framework for Terraform Cloud/Enterprise

**Resources to explore:**
- [ ] (to be filled)

---

## Summary

- Key takeaways as bullet points (leave blank for now — to fill after study)

## Related Sections

- [Azure Core](./01-azure-core.md)
- [Cloud Architecture](./02-cloud-architecture.md)
- [Containers and Kubernetes](../Phase-07-Containers-And-Kubernetes/02-kubernetes.md)
- [Cloud Security](../Phase-10-Security-Engineering/03-cloud-security.md)
