---
goal: Add Azure AI Foundry (Cognitive Services) with a project and model deployment to the existing durable-agent serverless infrastructure
---

# Introduction

This plan adds Azure AI Foundry capabilities to the existing durable-agent serverless infrastructure. Azure AI Foundry uses the new simplified resource model based on `Microsoft.CognitiveServices/accounts` with `allowProjectManagement: true`, which replaces the older ML workspace Hub/Project pattern. The Foundry account, a project, and an initial model deployment are defined as raw Bicep resources (no AVM module exists for this pattern). The Function App's managed identity receives RBAC access to call the deployed models. This integrates into the existing `main.bicep` subscription-scoped deployment.

Reference implementation: [microsoft-foundry/foundry-samples/00-basic](https://github.com/microsoft-foundry/foundry-samples/blob/main/infrastructure/infrastructure-setup-bicep/00-basic/main.bicep)

## Resources

### aiFoundryAccount

```yaml
name: aiFoundryAccount
kind: Raw
type: Microsoft.CognitiveServices/accounts@2025-06-01

purpose: Azure AI Foundry account (Cognitive Services with project management enabled) that provides the AI services endpoint, model hosting, and project container for the durable agent
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Globally unique name for the AI Foundry account. Also used as the custom subdomain. Pattern ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$, 2–64 chars.
      example: ai-durable-agent-001
    - name: location
      type: string
      description: Azure region for deployment. Must support Cognitive Services AIServices kind.
      example: eastus2
    - name: kind
      type: string
      description: Resource kind. Must be 'AIServices' for AI Foundry.
      example: AIServices
    - name: sku.name
      type: string
      description: SKU name for the Cognitive Services account.
      example: S0
    - name: identity.type
      type: string
      description: Managed identity type. SystemAssigned recommended.
      example: SystemAssigned
    - name: properties.allowProjectManagement
      type: bool
      description: Enables AI Foundry project management as child resources. REQUIRED for Foundry functionality.
      example: true
    - name: properties.customSubDomainName
      type: string
      description: Custom subdomain for token-based authentication endpoint. Must be globally unique.
      example: ai-durable-agent-001
  optional:
    - name: properties.disableLocalAuth
      type: bool
      description: Disable API key authentication. Set true to enforce Entra ID only.
      default: true
    - name: properties.publicNetworkAccess
      type: string
      description: Whether public endpoint access is allowed.
      default: Enabled
    - name: properties.networkAcls
      type: object
      description: Network ACL rules for the account.
      default: '{ defaultAction: "Allow" }'
    - name: tags
      type: object
      description: Resource tags
      default: '{}'

outputs:
  - name: id
    type: string
    description: Resource ID of the AI Foundry account
  - name: name
    type: string
    description: Name of the deployed account
  - name: properties.endpoint
    type: string
    description: Primary endpoint URL of the AI Foundry account
  - name: properties.endpoints
    type: object
    description: Dictionary of all endpoints (OpenAI, etc.)
  - name: identity.principalId
    type: string
    description: Principal ID of the system-assigned managed identity

references:
  docs: https://learn.microsoft.com/en-us/azure/ai-services/what-are-ai-services
  api: https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/accounts
  foundry: https://github.com/microsoft-foundry/foundry-samples/tree/main/infrastructure/infrastructure-setup-bicep
```

### aiFoundryProject

```yaml
name: aiFoundryProject
kind: Raw
type: Microsoft.CognitiveServices/accounts/projects@2025-06-01

purpose: AI Foundry project — child resource that groups developer API access, files, and agent configurations for a specific use case. Projects provide isolated RBAC and identity scoping.
dependsOn:
  - aiFoundryAccount

parameters:
  required:
    - name: name
      type: string
      description: Name of the project. Pattern ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$, 2–64 chars.
      example: durable-agent-proj
    - name: parent
      type: symbolicReference
      description: Symbolic reference to the parent AI Foundry account resource
      example: aiFoundryAccount
    - name: location
      type: string
      description: Azure region for deployment (must match parent account)
      example: eastus2
  optional:
    - name: identity.type
      type: string
      description: Managed identity type for the project
      default: SystemAssigned
    - name: properties.description
      type: string
      description: Human-readable description of the project
      default: ''
    - name: properties.displayName
      type: string
      description: Display name of the project
      default: ''
    - name: tags
      type: object
      description: Resource tags
      default: '{}'

outputs:
  - name: id
    type: string
    description: Resource ID of the project
  - name: name
    type: string
    description: Name of the deployed project
  - name: properties.endpoints
    type: object
    description: Dictionary of project-specific endpoints
  - name: identity.principalId
    type: string
    description: Principal ID of the project's system-assigned managed identity

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/accounts/projects
  foundry: https://github.com/microsoft-foundry/foundry-samples/blob/main/infrastructure/infrastructure-setup-bicep/00-basic/main.bicep
```

### aiModelDeployment

```yaml
name: aiModelDeployment
kind: Raw
type: Microsoft.CognitiveServices/accounts/deployments@2025-06-01

purpose: Deploys an OpenAI model (e.g. gpt-4.1-mini) to the AI Foundry account for use by the durable agent orchestrations via the developer API
dependsOn:
  - aiFoundryAccount

parameters:
  required:
    - name: name
      type: string
      description: Name of the deployment (typically matches the model name for discoverability)
      example: gpt-4.1-mini
    - name: parent
      type: symbolicReference
      description: Symbolic reference to the parent AI Foundry account
      example: aiFoundryAccount
    - name: sku.name
      type: string
      description: SKU name for the deployment (e.g. GlobalStandard, Standard, ProvisionedManaged)
      example: GlobalStandard
    - name: sku.capacity
      type: int
      description: Deployment capacity in TPM (tokens per minute) units. Varies by SKU.
      example: 1
    - name: properties.model.name
      type: string
      description: Name of the model to deploy
      example: gpt-4.1-mini
    - name: properties.model.format
      type: string
      description: Model format identifier
      example: OpenAI
    - name: properties.model.version
      type: string
      description: Model version string
      example: '2025-04-14'
  optional:
    - name: properties.versionUpgradeOption
      type: string
      description: Auto-upgrade behavior when new model versions become available
      default: OnceNewDefaultVersionAvailable

outputs:
  - name: id
    type: string
    description: Resource ID of the model deployment
  - name: name
    type: string
    description: Name of the deployed model deployment

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/accounts/deployments
  models: https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models
```

# Implementation Plan

This plan adds 3 new Azure resources and 1 new RBAC role assignment to the existing `main.bicep`. The AI Foundry account deploys in the Foundation phase (Phase 1) alongside existing independent resources. The project and model deployment follow in Phase 2 as child resources. RBAC for the Function App's managed identity is added to the existing Phase 4. All 3 resources use raw Bicep — no AVM module exists for the new AI Foundry Cognitive Services pattern.

## Phase 1 — AI Foundry Account (Foundation)

**Objective:** Deploy the AI Foundry account as an independent resource with no dependencies. This integrates into the existing Phase 1 in `main.bicep`.

- IMPLEMENT-GOAL-001: Provision the AI Foundry (Cognitive Services) account with project management enabled, system-assigned managed identity, and local auth disabled.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-001 | Add AI Foundry parameters to `main.bicep`                                   | Add `param modelName string = 'gpt-4.1-mini'`, `param modelFormat string = 'OpenAI'`, `param modelVersion string = '2025-04-14'`, `param modelSkuName string = 'GlobalStandard'`, `param modelCapacity int = 1` parameters with `@description` decorators |
| TASK-002 | Add AI Foundry naming variables                                             | Add `var aiFoundryName = 'ai-${baseName}-${resourceToken}'`, `var aiProjectName = '${baseName}-proj'` variables in the Variables section |
| TASK-003 | Create the AI Foundry account resource in `main.bicep`                      | Define `resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01'` with `kind: 'AIServices'`, `sku: { name: 'S0' }`, `identity: { type: 'SystemAssigned' }`, and `properties: { allowProjectManagement: true, customSubDomainName: aiFoundryName, disableLocalAuth: true, publicNetworkAccess: 'Enabled', networkAcls: { defaultAction: 'Allow' } }`. Place in Phase 1 section. Note: this resource is resource-group scoped, so it belongs inside a module or the existing `rg` scope |

## Phase 2 — Project and Model Deployment

**Objective:** Deploy the AI Foundry project and initial model deployment as child resources of the account. This integrates into the existing Phase 2 in `main.bicep`.

- IMPLEMENT-GOAL-002: Provision the AI Foundry project and deploy an OpenAI model for the durable agent to consume.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-004 | Create the AI Foundry project as child resource                             | Define `resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01'` with `parent: aiFoundry`, `identity: { type: 'SystemAssigned' }`, and `properties: { description: 'Durable agent AI project', displayName: '${baseName} Agent Project' }` |
| TASK-005 | Create the model deployment as child resource                               | Define `resource aiModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01'` with `parent: aiFoundry`, `sku: { name: modelSkuName, capacity: modelCapacity }`, and `properties: { model: { name: modelName, format: modelFormat, version: modelVersion } }` |

## Phase 3 — Function App Configuration

**Objective:** Add AI Foundry endpoint configuration to the Function App's app settings so the durable agent can call the deployed models.

- IMPLEMENT-GOAL-003: Configure Function App with AI Foundry endpoint and model deployment details.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-006 | Add AI Foundry app settings to Function App config                          | Add to the existing `configs` appsettings: `AI_FOUNDRY_ENDPOINT` = `aiFoundry.properties.endpoint`, `AI_FOUNDRY_DEPLOYMENT_NAME` = model deployment name, `AI_FOUNDRY_PROJECT_NAME` = project name |

## Phase 4 — RBAC for Function App Identity

**Objective:** Grant the Function App's system-assigned managed identity `Cognitive Services OpenAI User` role on the AI Foundry account so it can call the deployed models using managed identity.

- IMPLEMENT-GOAL-004: Assign RBAC role to the Function App's managed identity for keyless AI model access.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-007 | Add Cognitive Services OpenAI User role assignment                          | Add role assignment for `Cognitive Services OpenAI User` (role definition ID: `5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) scoped to the AI Foundry account, with principal = Function App's `systemAssignedMIPrincipalId`. Add to `modules/rbac.bicep` |

## Phase 5 — Outputs

**Objective:** Expose AI Foundry outputs from `main.bicep` for downstream consumption.

- IMPLEMENT-GOAL-005: Add outputs for AI Foundry endpoint, project name, and deployment name.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-008 | Add AI Foundry outputs to `main.bicep`                                      | Add outputs: `aiFoundryEndpoint` (account endpoint URL), `aiFoundryAccountName` (account name), `aiFoundryProjectName` (project name), `aiModelDeploymentName` (deployment name) |

## Integration Notes

### Scoping

The existing `main.bicep` uses `targetScope = 'subscription'` and scopes all resources into the `rg` resource group. The AI Foundry resources are resource-group scoped, so they must be defined either:

- **Option A (recommended):** In a new `modules/ai-foundry.bicep` module, scoped to `rg` — follows the pattern used by `modules/durable-task.bicep` for raw resources with parent-child relationships.
- **Option B:** Inline in `main.bicep` if the resources are defined via module calls with `scope: rg`.

Option A is recommended because the AI Foundry account, project, and model deployment form a parent-child hierarchy, similar to how `durable-task.bicep` encapsulates the scheduler + task hub.

### Module Structure

```
infra/bicep/
├── main.bicep                  # Add module call + parameters + outputs
├── main.bicepparam             # Add AI Foundry parameters
└── modules/
    ├── durable-task.bicep      # Existing: scheduler + taskHub
    ├── rbac.bicep              # Add: Cognitive Services OpenAI User role
    └── ai-foundry.bicep        # NEW: AI Foundry account + project + deployment
```

### `modules/ai-foundry.bicep` — Expected Interface

```bicep
// Parameters
param aiFoundryName string
param aiProjectName string
param location string
param tags object = {}

// Model deployment parameters
param modelName string
param modelFormat string
param modelVersion string
param modelSkuName string
param modelCapacity int

// Resources: account → project, account → deployment

// Outputs
output accountId string        // resource ID
output accountName string      // resource name
output accountEndpoint string  // properties.endpoint
output projectName string      // project name
output deploymentName string   // model deployment name
```

## High-level design

```
Architecture Diagram (AI Foundry Addition)
==========================================

                    ┌─────────────────────────────────────────────────┐
                    │              Azure Resource Group                │
                    │                                                 │
                    │  ┌───── Existing Resources ─────────────────┐   │
                    │  │  Log Analytics, App Insights, Storage,   │   │
                    │  │  Service Bus, Durable Task Scheduler,    │   │
                    │  │  App Service Plan (FC1), Function App    │   │
                    │  └──────────────────┬──────────────────────┘   │
                    │                     │                           │
                    │                     │ Managed Identity (RBAC)   │
                    │                     │                           │
                    │  ┌──────────────────▼──────────────────────┐   │
                    │  │        AI Foundry Account               │   │
                    │  │  Microsoft.CognitiveServices/accounts   │   │
                    │  │  kind: AIServices                       │   │
                    │  │  allowProjectManagement: true            │   │
                    │  │                                         │   │
                    │  │  ┌─────────────────────────────────┐   │   │
                    │  │  │  Project                         │   │   │
                    │  │  │  accounts/projects                │   │   │
                    │  │  │  (durable-agent-proj)             │   │   │
                    │  │  └─────────────────────────────────┘   │   │
                    │  │                                         │   │
                    │  │  ┌─────────────────────────────────┐   │   │
                    │  │  │  Model Deployment                │   │   │
                    │  │  │  accounts/deployments             │   │   │
                    │  │  │  (gpt-4.1-mini / GlobalStandard)  │   │   │
                    │  │  └─────────────────────────────────┘   │   │
                    │  └─────────────────────────────────────────┘   │
                    └─────────────────────────────────────────────────┘

Data Flow (with AI Foundry)
============================

    ┌──────────┐   Service Bus      ┌──────────────────┐
    │ External │   Trigger          │   Function App   │
    │ Producer │──────────────────►│  (Flex Consump.) │
    └──────────┘                    │                  │
                                    │  ┌────────────┐  │  Managed Identity
                                    │  │  Durable   │  │  (RBAC)
                                    │  │ Functions  │──┼──────────────┐
                                    │  │Orchestrator│  │              │
                                    │  └─────┬──────┘  │              │
                                    │        │         │              │
                                    │  ┌─────▼──────┐  │              │
                                    │  │ Activity:  │  │              │
                                    │  │ Call AI    │──┼──────┐       │
                                    │  │ Foundry    │  │      │       │
                                    │  └────────────┘  │      │       │
                                    └──────────────────┘      │       │
                                                              │       │
                        ┌─────────────────────────────────────▼───────▼──────┐
                        │                AI Foundry Account                   │
                        │  ┌──────────────────┐   ┌───────────────────────┐  │
                        │  │    Project        │   │  Model Deployment    │  │
                        │  │ (durable-agent)   │   │  (gpt-4.1-mini)     │  │
                        │  └──────────────────┘   └───────────────────────┘  │
                        └────────────────────────────────────────────────────┘

    All connections use Managed Identity (RBAC) — no API keys.
    Function App → AI Foundry uses Cognitive Services OpenAI User role.
```

### Key Design Decisions

1. **New AI Foundry Resource Model:** Uses `Microsoft.CognitiveServices/accounts@2025-06-01` with `allowProjectManagement: true` instead of the older ML workspace Hub/Project pattern. This is the recommended approach per the [microsoft-foundry/foundry-samples](https://github.com/microsoft-foundry/foundry-samples) reference implementation.

2. **No Additional Dependencies:** Unlike the legacy ML workspace pattern (which required Key Vault, Storage Account, and Container Registry), the new Foundry model is self-contained. Only the Cognitive Services account, project, and deployment resources are needed.

3. **Raw Bicep Only:** No AVM module exists for this new resource pattern. All 3 resources are defined using explicit API version `2025-06-01`.

4. **Local Auth Disabled:** `disableLocalAuth: true` enforces Entra ID (RBAC) authentication only. No API keys are used, consistent with the project's zero-secrets architecture.

5. **Separate Module:** The AI Foundry resources are encapsulated in `modules/ai-foundry.bicep` to keep the parent-child relationship self-contained, following the precedent set by `modules/durable-task.bicep`.

6. **GlobalStandard SKU:** Model deployment uses `GlobalStandard` SKU for cost-effective, globally routed inference. This can be parameterized for production use with `Standard` or `ProvisionedManaged` SKUs.

7. **RBAC — Cognitive Services OpenAI User:** The Function App's managed identity receives `Cognitive Services OpenAI User` (role ID `5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`), which grants read access to OpenAI model deployments. This is the least-privilege role for calling models without managing them.
