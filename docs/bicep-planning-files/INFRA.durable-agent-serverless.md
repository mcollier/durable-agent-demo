---
goal: Deploy a serverless event-driven architecture with Azure Function App (Flex Consumption), Azure Service Bus, Azure Storage Account, Azure Application Insights, and Durable Task Scheduler (Consumption)
---

# Introduction

This plan defines the Azure infrastructure for a serverless, event-driven application built on Azure Functions (Flex Consumption) with durable orchestration capabilities. The architecture uses Azure Service Bus for reliable messaging, Azure Storage Account for function runtime storage, Application Insights (workspace-based) for observability, and Durable Task Scheduler (Consumption tier) for orchestrating long-running workflows. All resources are deployed via Bicep using Azure Verified Modules (AVM) where available, and raw resource definitions where no AVM module exists.

## Resources

### logAnalyticsWorkspace

```yaml
name: logAnalyticsWorkspace
kind: AVM
avmModule: br/public:avm/res/operational-insights/workspace:0.15.0

purpose: Provides the backing Log Analytics workspace required by workspace-based Application Insights for log storage and querying
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Name of the Log Analytics workspace
      example: law-durable-agent-001
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: skuName
      type: string
      description: Pricing tier of the workspace
      default: PerGB2018
    - name: retentionInDays
      type: int
      description: Number of days to retain data
      default: 30
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the Log Analytics workspace
  - name: name
    type: string
    description: Name of the deployed workspace
  - name: logAnalyticsWorkspaceId
    type: string
    description: Workspace ID (customer ID) for configuration references

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.operationalinsights/workspaces
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/operational-insights/workspace
```

### applicationInsights

```yaml
name: applicationInsights
kind: AVM
avmModule: br/public:avm/res/insights/component:0.7.1

purpose: Workspace-based Application Insights resource for monitoring, diagnostics, and telemetry collection of the Function App
dependsOn:
  - logAnalyticsWorkspace

parameters:
  required:
    - name: name
      type: string
      description: Name of the Application Insights component
      example: appi-durable-agent-001
    - name: workspaceResourceId
      type: string
      description: Resource ID of the Log Analytics workspace
      example: logAnalyticsWorkspace.outputs.resourceId
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: kind
      type: string
      description: The kind of application this component refers to
      default: web
    - name: applicationType
      type: string
      description: Type of application being monitored
      default: web
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the Application Insights component
  - name: connectionString
    type: string
    description: Application Insights connection string for telemetry ingestion
  - name: instrumentationKey
    type: string
    description: Instrumentation key (legacy, prefer connection string)

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/components
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/insights/component
```

### storageAccount

```yaml
name: storageAccount
kind: AVM
avmModule: br/public:avm/res/storage/storage-account:0.31.0

purpose: Provides runtime storage for the Azure Function App (Flex Consumption requires a storage account for deployment artifacts and internal state)
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Globally unique name for the storage account (3-24 chars, lowercase alphanumeric)
      example: stdurableagent001
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: skuName
      type: string
      description: Storage account SKU
      default: Standard_LRS
    - name: kind
      type: string
      description: Storage account kind
      default: StorageV2
    - name: allowBlobPublicAccess
      type: bool
      description: Whether blob public access is allowed
      default: false
    - name: allowSharedKeyAccess
      type: bool
      description: Whether shared key access is allowed
      default: false
    - name: minimumTlsVersion
      type: string
      description: Minimum TLS version
      default: TLS1_2
    - name: networkAcls
      type: object
      description: Network ACL rules for the storage account
      default: '{ defaultAction: "Allow" }'
    - name: blobServices
      type: object
      description: Blob service configuration
      default: '{}'
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the storage account
  - name: name
    type: string
    description: Name of the deployed storage account
  - name: primaryBlobEndpoint
    type: string
    description: Primary blob service endpoint URL

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/storageaccounts
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/storage/storage-account
```

### serviceBusNamespace

```yaml
name: serviceBusNamespace
kind: AVM
avmModule: br/public:avm/res/service-bus/namespace:0.16.1

purpose: Azure Service Bus namespace for reliable asynchronous messaging between components. Provides queues and/or topics for the Function App to consume
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Name of the Service Bus namespace
      example: sb-durable-agent-001
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: skuObject
      type: object
      description: SKU configuration for the Service Bus namespace
      default: '{ name: "Standard" }'
    - name: queues
      type: array
      description: Array of queue definitions to create in the namespace
      default: '[]'
    - name: topics
      type: array
      description: Array of topic definitions to create in the namespace
      default: '[]'
    - name: disableLocalAuth
      type: bool
      description: Disable SAS authentication for the namespace
      default: true
    - name: tags
      type: object
      description: Resource tags
      default: {}
    - name: roleAssignments
      type: array
      description: RBAC role assignments for the namespace
      default: '[]'

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the Service Bus namespace
  - name: name
    type: string
    description: Name of the deployed namespace
  - name: serviceBusEndpoint
    type: string
    description: Service Bus endpoint (FQDN)

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/namespaces
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/service-bus/namespace
```

### durableTaskScheduler

```yaml
name: durableTaskScheduler
kind: Raw
type: Microsoft.DurableTask/schedulers@2025-11-01

purpose: Durable Task Scheduler resource (Consumption SKU) that provides the backend orchestration engine for Durable Functions
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Name of the Durable Task Scheduler (alphanumeric and hyphens, 3-64 chars)
      example: dts-durable-agent-001
    - name: location
      type: string
      description: Azure region for deployment
      example: eastus
    - name: properties.sku.name
      type: string
      description: SKU name for the scheduler
      example: Consumption
    - name: properties.ipAllowlist
      type: 'string[]'
      description: IP allow list for the scheduler. Values can be IPv4, IPv6 or CIDR
      example: '["0.0.0.0/0"]'
  optional:
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: id
    type: string
    description: Resource ID of the Durable Task Scheduler
  - name: name
    type: string
    description: Name of the deployed scheduler
  - name: properties.endpoint
    type: string
    description: URL endpoint of the Durable Task Scheduler

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.durabletask/schedulers
```

### durableTaskHub

```yaml
name: durableTaskHub
kind: Raw
type: Microsoft.DurableTask/schedulers/taskHubs@2025-11-01

purpose: Task Hub child resource of the Durable Task Scheduler. Provides an isolated execution context for durable orchestrations
dependsOn:
  - durableTaskScheduler

parameters:
  required:
    - name: name
      type: string
      description: Name of the Task Hub (alphanumeric and hyphens, 3-64 chars)
      example: taskhub-durable-agent-001
    - name: parent
      type: symbolicReference
      description: Symbolic reference to the parent Durable Task Scheduler resource
      example: durableTaskScheduler

outputs:
  - name: id
    type: string
    description: Resource ID of the Task Hub
  - name: name
    type: string
    description: Name of the deployed Task Hub
  - name: properties.dashboardUrl
    type: string
    description: URL to the Durable Task Scheduler dashboard for this Task Hub

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.durabletask/schedulers/taskhubs
```

### appServicePlan

```yaml
name: appServicePlan
kind: AVM
avmModule: br/public:avm/res/web/serverfarm:0.6.0

purpose: Flex Consumption (FC1) App Service Plan that hosts the Function App with serverless scaling
dependsOn: []

parameters:
  required:
    - name: name
      type: string
      description: Name of the App Service Plan
      example: asp-durable-agent-001
    - name: skuName
      type: string
      description: SKU name for the plan
      example: FC1
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: kind
      type: string
      description: Plan kind
      default: functionapp
    - name: reserved
      type: bool
      description: Must be true for Linux plans
      default: true
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the App Service Plan
  - name: name
    type: string
    description: Name of the deployed plan

references:
  docs: https://learn.microsoft.com/en-us/azure/templates/microsoft.web/serverfarms
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/serverfarm
```

### functionApp

```yaml
name: functionApp
kind: AVM
avmModule: br/public:avm/res/web/site:0.21.0

purpose: Azure Function App (Flex Consumption) that hosts the serverless functions, processes Service Bus messages, and orchestrates durable workflows via Durable Task Scheduler
dependsOn:
  - appServicePlan
  - storageAccount
  - applicationInsights
  - serviceBusNamespace
  - durableTaskScheduler
  - durableTaskHub

parameters:
  required:
    - name: name
      type: string
      description: Globally unique name of the Function App
      example: func-durable-agent-001
    - name: kind
      type: string
      description: Resource kind
      example: functionapp,linux
    - name: serverFarmResourceId
      type: string
      description: Resource ID of the Flex Consumption App Service Plan
      example: appServicePlan.outputs.resourceId
  optional:
    - name: location
      type: string
      description: Azure region for deployment
      default: resourceGroup().location
    - name: managedIdentities
      type: object
      description: Managed identity configuration (system-assigned recommended)
      default: '{ systemAssigned: true }'
    - name: functionAppConfig
      type: object
      description: Flex Consumption configuration including deployment storage, runtime, and scaling
      default: See implementation notes below
    - name: appSettingsKeyValuePairs
      type: object
      description: Application settings key-value pairs
      default: '{}'
    - name: siteConfig
      type: object
      description: Site configuration for the Function App
      default: '{}'
    - name: applicationInsightResourceId
      type: string
      description: Resource ID of the Application Insights component
      default: ''
    - name: tags
      type: object
      description: Resource tags
      default: {}

outputs:
  - name: resourceId
    type: string
    description: Resource ID of the Function App
  - name: name
    type: string
    description: Name of the deployed Function App
  - name: defaultHostname
    type: string
    description: Default hostname of the Function App
  - name: systemAssignedMIPrincipalId
    type: string
    description: Principal ID of the system-assigned managed identity

references:
  docs: https://learn.microsoft.com/en-us/azure/azure-functions/flex-consumption-how-to
  avm: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/web/site
```

# Implementation Plan

This architecture deploys 8 Azure resources across 4 phases. The first phase provisions foundational resources with no dependencies (Log Analytics, Storage, Service Bus, Durable Task Scheduler). The second phase deploys dependent monitoring and orchestration resources (Application Insights, Task Hub). The third phase deploys the compute layer (App Service Plan, Function App). The fourth phase configures RBAC role assignments so the Function App's managed identity can access Service Bus, Storage, and the Durable Task Scheduler securely.

## Phase 1 — Foundation Resources

**Objective:** Deploy all independent base resources that have no dependencies on other resources in this plan.

These resources can be deployed in parallel since they have no inter-dependencies.

- IMPLEMENT-GOAL-001: Provision foundational infrastructure — Log Analytics Workspace, Storage Account, Service Bus Namespace, and Durable Task Scheduler.

| Task     | Description                                                                 | Action                                                                                                    |
| -------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| TASK-001 | Create the Log Analytics Workspace using AVM module                         | Define `logAnalyticsWorkspace` module using `br/public:avm/res/operational-insights/workspace:0.15.0`     |
| TASK-002 | Create the Storage Account using AVM module                                 | Define `storageAccount` module using `br/public:avm/res/storage/storage-account:0.31.0`                   |
| TASK-003 | Create the Service Bus Namespace using AVM module with local auth disabled  | Define `serviceBusNamespace` module using `br/public:avm/res/service-bus/namespace:0.16.1`                |
| TASK-004 | Create the Durable Task Scheduler (Consumption SKU) as raw resource         | Define `durableTaskScheduler` resource using `Microsoft.DurableTask/schedulers@2025-11-01` with `sku.name = 'Consumption'` and `ipAllowlist = ['0.0.0.0/0']` |

## Phase 2 — Monitoring and Orchestration

**Objective:** Deploy resources that depend on Phase 1 outputs — Application Insights (requires Log Analytics Workspace) and Task Hub (requires Durable Task Scheduler).

- IMPLEMENT-GOAL-002: Provision monitoring and durable orchestration child resources.

| Task     | Description                                                                  | Action                                                                                                     |
| -------- | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| TASK-005 | Create workspace-based Application Insights using AVM module                 | Define `applicationInsights` module using `br/public:avm/res/insights/component:0.7.1`, passing `logAnalyticsWorkspace.outputs.resourceId` as `workspaceResourceId` |
| TASK-006 | Create the Task Hub as a child resource of the Durable Task Scheduler        | Define `durableTaskHub` resource using `Microsoft.DurableTask/schedulers/taskHubs@2025-11-01` with `parent: durableTaskScheduler` |

## Phase 3 — Compute Layer

**Objective:** Deploy the App Service Plan (Flex Consumption) and Function App with all required configuration including `functionAppConfig` for Flex Consumption deployment storage.

- IMPLEMENT-GOAL-003: Provision the serverless compute resources — App Service Plan and Function App.

| Task     | Description                                                                  | Action                                                                                                     |
| -------- | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| TASK-007 | Create the Flex Consumption App Service Plan using AVM module                | Define `appServicePlan` module using `br/public:avm/res/web/serverfarm:0.6.0` with `skuName: 'FC1'`, `kind: 'functionapp'`, and `reserved: true` (Linux) |
| TASK-008 | Create the Function App using AVM module with Flex Consumption config        | Define `functionApp` module using `br/public:avm/res/web/site:0.21.0` with `kind: 'functionapp,linux'`, system-assigned managed identity, `applicationInsightResourceId`, and `functionAppConfig` containing deployment storage referencing the storage account |
| TASK-009 | Configure Function App `functionAppConfig` for Flex Consumption              | Set `functionAppConfig.deployment.storage` with `type: 'blobContainer'`, `value: '<storageAccount-blobEndpoint>/deploymentpackage'`, and `authentication` using system-assigned managed identity (StorageBlobDataContributor) |
| TASK-010 | Configure Function App app settings for Service Bus and Durable Task Scheduler | Set app settings: `ServiceBusConnection__fullyQualifiedNamespace` = Service Bus FQDN, `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` = DTS endpoint, `TASKHUB_NAME` = Task Hub name |

## Phase 4 — Identity and Access Management

**Objective:** Configure RBAC role assignments so the Function App's system-assigned managed identity has least-privilege access to all dependent resources.

- IMPLEMENT-GOAL-004: Assign RBAC roles to the Function App's managed identity for secure, keyless access.

| Task     | Description                                                                  | Action                                                                                                     |
| -------- | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| TASK-011 | Assign Storage Blob Data Contributor on the Storage Account                  | Create role assignment for `Storage Blob Data Contributor` (role ID: `ba92f5b4-2d11-453d-a403-e96b0029c9fe`) scoped to the storage account, with principal = `functionApp.outputs.systemAssignedMIPrincipalId` |
| TASK-012 | Assign Azure Service Bus Data Receiver on the Service Bus Namespace          | Create role assignment for `Azure Service Bus Data Receiver` (role ID: `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0`) scoped to the Service Bus namespace, with principal = `functionApp.outputs.systemAssignedMIPrincipalId` |
| TASK-013 | Assign Azure Service Bus Data Sender on the Service Bus Namespace            | Create role assignment for `Azure Service Bus Data Sender` (role ID: `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39`) scoped to the Service Bus namespace, with principal = `functionApp.outputs.systemAssignedMIPrincipalId` |
| TASK-014 | Assign Durable Task Scheduler Data Contributor on the Durable Task Scheduler | Create role assignment for the appropriate DTS data-plane role scoped to the Durable Task Scheduler, with principal = `functionApp.outputs.systemAssignedMIPrincipalId` |

## High-level design

```
Architecture Diagram
====================

                    ┌─────────────────────────────────────────────┐
                    │              Azure Resource Group            │
                    │                                             │
                    │  ┌─────────────────────────────────────┐    │
                    │  │       Monitoring Layer               │    │
                    │  │                                     │    │
                    │  │  ┌──────────────┐  ┌────────────┐  │    │
                    │  │  │ Log Analytics│◄─┤ Application │  │    │
                    │  │  │  Workspace   │  │  Insights   │  │    │
                    │  │  └──────────────┘  └─────┬──────┘  │    │
                    │  └──────────────────────────┼─────────┘    │
                    │                             │ telemetry     │
                    │  ┌──────────────────────────┼─────────┐    │
                    │  │       Compute Layer       │         │    │
                    │  │                           ▼         │    │
                    │  │  ┌───────────────┐  ┌──────────┐   │    │
                    │  │  │  App Service  │◄─┤ Function │   │    │
                    │  │  │  Plan (FC1)   │  │   App    │   │    │
                    │  │  └───────────────┘  └──┬──┬──┬─┘   │    │
                    │  └────────────────────────┼──┼──┼─────┘    │
                    │                           │  │  │           │
                    │          ┌────────────────┘  │  └─────┐    │
                    │          │ messages           │ storage│    │
                    │          ▼                    ▼        ▼    │
                    │  ┌──────────────┐  ┌──────────┐ ┌────────┐ │
                    │  │  Service Bus │  │ Durable  │ │Storage │ │
                    │  │  Namespace   │  │  Task    │ │Account │ │
                    │  │  (Standard)  │  │Scheduler │ │(v2 LRS)│ │
                    │  └──────────────┘  │(Consump.)│ └────────┘ │
                    │                    │          │             │
                    │                    │┌────────┐│             │
                    │                    ││Task Hub││             │
                    │                    │└────────┘│             │
                    │                    └──────────┘             │
                    └─────────────────────────────────────────────┘

Network / Data Flow Diagram
============================

    ┌──────────┐   Service Bus      ┌──────────────────┐
    │ External │   Trigger/Send     │   Function App   │
    │ Producer │──────────────────►│  (Flex Consump.) │
    └──────────┘                    │                  │
                                    │  ┌────────────┐  │
                                    │  │  Durable   │  │  Managed Identity
                                    │  │ Functions  │──┼──────────────────┐
                                    │  │Orchestrator│  │                  │
                                    │  └────────────┘  │                  │
                                    └───────┬──────────┘                  │
                                            │                             │
                        ┌───────────────────┼───────────────────┐         │
                        │                   │                   │         │
                        ▼                   ▼                   ▼         ▼
               ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐
               │ Service Bus  │   │   Storage    │   │  Durable Task        │
               │  Namespace   │   │   Account    │   │  Scheduler           │
               │              │   │              │   │  ┌────────────────┐  │
               │  ┌────────┐  │   │  deployment  │   │  │   Task Hub     │  │
               │  │ Queues │  │   │  artifacts   │   │  │  (orchestr.)   │  │
               │  │/Topics │  │   │              │   │  └────────────────┘  │
               │  └────────┘  │   └──────────────┘   └──────────────────────┘
               └──────────────┘

    All connections use Managed Identity (RBAC) — no connection strings or keys.
    Telemetry flows from Function App ──► Application Insights ──► Log Analytics.
```

### Key Design Decisions

1. **Flex Consumption (FC1):** ALWAYS use Flex Consumption plan per Azure Functions best practices. This provides serverless scaling with per-execution billing and cold-start optimization.

2. **Managed Identity (System-Assigned):** All inter-resource communication uses RBAC with the Function App's system-assigned managed identity. No connection strings or shared access keys.

3. **Durable Task Scheduler (Consumption):** The Consumption SKU provides a fully managed, serverless backend for Durable Functions orchestrations. No AVM module exists — deployed as a raw Bicep resource using `Microsoft.DurableTask/schedulers@2025-11-01`.

4. **Workspace-based Application Insights:** Modern Application Insights requires a Log Analytics workspace backend. This provides unified querying and longer retention.

5. **Service Bus (Standard SKU):** Standard tier supports both queues and topics/subscriptions. Local SAS auth is disabled; access is controlled exclusively via RBAC.

6. **Storage Account Security:** Blob public access and shared key access are both disabled. The Function App accesses deployment storage via managed identity with the `StorageBlobDataContributor` role.

7. **Flex Consumption `functionAppConfig`:** The Function App MUST include `functionAppConfig` with `deployment.storage` configuration pointing to a blob container in the storage account, authenticated via managed identity. This is a mandatory requirement for FC1 plans.

### Bicep File Structure (Recommended)

```
infra/
├── main.bicep                  # Orchestrates all modules
├── main.bicepparam             # Parameters file
└── modules/
    └── durable-task.bicep      # Raw resources: scheduler + taskHub
```

- All AVM modules are referenced directly via `br/public:avm/res/...` in `main.bicep`.
- Durable Task Scheduler and Task Hub are defined in a shared Bicep module since they are raw resources with a parent-child relationship.
