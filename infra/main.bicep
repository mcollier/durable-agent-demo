// ──────────────────────────────────────────────────────────────────────────────
// Main Bicep — Durable Agent Serverless Architecture
// ──────────────────────────────────────────────────────────────────────────────
// Deploys: Log Analytics Workspace, Application Insights, Storage Account,
//          Service Bus Namespace, Durable Task Scheduler + Task Hub,
//          AI Foundry (Cognitive Services) + Project + Model Deployment,
//          App Service Plan (FC1), Function App (Flex Consumption).
//          RBAC role assignments for managed identity.
// ──────────────────────────────────────────────────────────────────────────────

targetScope = 'subscription'

// ─── Parameters ─────────────────────────────────────────────────────────────

@description('Base name used to derive resource names. Keep short (≤10 chars).')
@minLength(2)
@maxLength(10)
param baseName string

@description('Azure region for all resources.')
param location string

@description('Name of the resource group to create. Defaults to rg-<baseName>.')
param resourceGroupName string = 'rg-${baseName}'

@description('Function App runtime name (e.g. dotnet-isolated, node, python).')
@allowed(['dotnet-isolated', 'node', 'python', 'java', 'powershell', 'custom'])
param functionRuntimeName string = 'dotnet-isolated'

@description('Function App runtime version.')
param functionRuntimeVersion string = '10.0'

@description('Maximum instance count for Flex Consumption scaling.')
param maximumInstanceCount int = 100

@description('Instance memory in MB for Flex Consumption.')
@allowed([512, 2048, 4096])
param instanceMemoryMB int = 2048

@description('Resource tags applied to all resources.')
param tags object = {}

// ─── AI Foundry Parameters ──────────────────────────────────────────────────

@description('Azure region for the AI Foundry account. Defaults to the main location. Override to use a region with available model quota.')
param aiFoundryLocation string = location

@description('Name of the OpenAI model to deploy in AI Foundry.')
param modelName string = 'gpt-4.1-mini'

@description('Model format identifier (e.g. OpenAI).')
param modelFormat string = 'OpenAI'

@description('Model version string.')
param modelVersion string = '2025-04-14'

@description('SKU name for the model deployment (e.g. GlobalStandard, Standard).')
param modelSkuName string = 'GlobalStandard'

@description('Deployment capacity in TPM units.')
param modelCapacity int = 1

// ─── Variables ──────────────────────────────────────────────────────────────

var resourceToken = toLower(uniqueString(subscription().subscriptionId, resourceGroupName, baseName))
var deploymentStorageContainerName = 'app-package-${take(baseName, 16)}-${take(resourceToken, 7)}'

// Resource names following Azure naming conventions
var logAnalyticsWorkspaceName = 'law-${baseName}-${resourceToken}'
var applicationInsightsName = 'appi-${baseName}-${resourceToken}'
var storageAccountName = take('st${replace(baseName, '-', '')}${resourceToken}', 24)
var serviceBusNamespaceName = 'sb-${baseName}-${resourceToken}'
var schedulerName = 'dts-${baseName}-${resourceToken}'
var taskHubName = 'th-${baseName}-${resourceToken}'
var appServicePlanName = 'asp-${baseName}-${resourceToken}'
var functionAppName = 'func-${baseName}-${resourceToken}'
var webAppPlanName = 'asp-web-${baseName}-${resourceToken}'
var webAppName = 'web-${baseName}-${resourceToken}'
var aiFoundryName = 'ai-${baseName}-${resourceToken}'
var aiProjectName = '${baseName}-proj'

// ═════════════════════════════════════════════════════════════════════════════
// Resource Group
// ═════════════════════════════════════════════════════════════════════════════

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// ═════════════════════════════════════════════════════════════════════════════
// Phase 1 — Foundation Resources (no dependencies, deployed in parallel)
// ═════════════════════════════════════════════════════════════════════════════

// ─── Log Analytics Workspace ────────────────────────────────────────────────
module logAnalyticsWorkspace 'br/public:avm/res/operational-insights/workspace:0.15.0' = {
  scope: rg
  params: {
    name: logAnalyticsWorkspaceName
    location: location
    tags: tags
  }
}

// ─── Storage Account ────────────────────────────────────────────────────────
module storageAccount 'br/public:avm/res/storage/storage-account:0.31.0' = {
  scope: rg
  params: {
    name: storageAccountName
    location: location
    tags: tags
    skuName: 'Standard_LRS'
    kind: 'StorageV2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    blobServices: {
      containers: [
        { name: deploymentStorageContainerName }
      ]
    }
  }
}

// ─── Service Bus Namespace ──────────────────────────────────────────────────
module serviceBusNamespace 'br/public:avm/res/service-bus/namespace:0.16.1' = {
  scope: rg
  params: {
    name: serviceBusNamespaceName
    location: location
    tags: tags
    skuObject: {
      name: 'Standard'
    }
    disableLocalAuth: true
    queues: [
      {
        name: 'inbound-feedback'
        maxDeliveryCount: 10
        deadLetteringOnMessageExpiration: true
        lockDuration: 'PT1M'
      }
    ]
  }
}

// ─── Durable Task Scheduler + Task Hub ──────────────────────────────────────
module durableTask 'modules/durable-task.bicep' = {
  scope: rg
  params: {
    schedulerName: schedulerName
    taskHubName: taskHubName
    location: location
    tags: tags
  }
}

// ─── AI Foundry (Cognitive Services) + Project + Model Deployment ───────────
module aiFoundry 'modules/ai-foundry.bicep' = {
  scope: rg
  params: {
    aiFoundryName: aiFoundryName
    aiProjectName: aiProjectName
    location: aiFoundryLocation
    tags: tags
    modelName: modelName
    modelFormat: modelFormat
    modelVersion: modelVersion
    modelSkuName: modelSkuName
    modelCapacity: modelCapacity
  }
}

// ═════════════════════════════════════════════════════════════════════════════
// Phase 2 — Monitoring & Orchestration (depends on Phase 1)
// ═════════════════════════════════════════════════════════════════════════════

// ─── Application Insights (workspace-based) ─────────────────────────────────
module applicationInsights 'br/public:avm/res/insights/component:0.7.1' = {
  scope: rg
  params: {
    name: applicationInsightsName
    location: location
    tags: tags
    workspaceResourceId: logAnalyticsWorkspace.outputs.resourceId
  }
}

// ═════════════════════════════════════════════════════════════════════════════
// Phase 3 — Compute Layer
// ═════════════════════════════════════════════════════════════════════════════

// ─── App Service Plan (Flex Consumption FC1) ────────────────────────────────
module appServicePlan 'br/public:avm/res/web/serverfarm:0.6.0' = {
  scope: rg
  params: {
    name: appServicePlanName
    location: location
    tags: tags
    skuName: 'FC1'
    reserved: true
  }
}

// ─── Function App (Flex Consumption) ────────────────────────────────────────
module functionApp 'br/public:avm/res/web/site:0.21.0' = {
  scope: rg
  params: {
    name: functionAppName
    kind: 'functionapp,linux'
    location: location
    tags: union(tags, { 'azd-service-name': 'froyo-func' })
    serverFarmResourceId: appServicePlan.outputs.resourceId
    managedIdentities: {
      systemAssigned: true
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.outputs.primaryBlobEndpoint}${deploymentStorageContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        instanceMemoryMB: instanceMemoryMB
        maximumInstanceCount: maximumInstanceCount
      }
      runtime: {
        name: functionRuntimeName
        version: functionRuntimeVersion
      }
    }
    configs: [
      {
        name: 'appsettings'
        properties: {
          // Application Insights
          APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.outputs.connectionString
          ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
          // Storage — managed identity
          AzureWebJobsStorage__blobServiceUri: storageAccount.outputs.primaryBlobEndpoint
          AzureWebJobsStorage__credential: 'managedidentity'
          // Service Bus — managed identity
          ServiceBusConnection__fullyQualifiedNamespace: '${serviceBusNamespace.outputs.name}.servicebus.windows.net'
          // Service Bus — queue name
          SERVICEBUS_QUEUE_NAME: 'inbound-feedback'
          // Durable Task Scheduler
          DURABLE_TASK_SCHEDULER_CONNECTION_STRING: durableTask.outputs.schedulerEndpoint
          TASKHUB_NAME: durableTask.outputs.taskHubName
          // AI Foundry
          AI_FOUNDRY_ENDPOINT: aiFoundry.outputs.accountEndpoint
          AI_FOUNDRY_DEPLOYMENT_NAME: aiFoundry.outputs.deploymentName
          AI_FOUNDRY_PROJECT_NAME: aiFoundry.outputs.projectName
        }
      }
    ]
  }
}

// ─── App Service Plan (Web App — B1 Linux) ─────────────────────────────────
module webAppPlan 'br/public:avm/res/web/serverfarm:0.6.0' = {
  scope: rg
  params: {
    name: webAppPlanName
    location: location
    tags: tags
    skuName: 'B1'
    reserved: true
  }
}

// ─── Web App (Razor Pages frontend) ─────────────────────────────────────────
module webApp 'br/public:avm/res/web/site:0.21.0' = {
  scope: rg
  params: {
    name: webAppName
    kind: 'app,linux'
    location: location
    tags: union(tags, { 'azd-service-name': 'froyo-web' })
    serverFarmResourceId: webAppPlan.outputs.resourceId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
    }
    configs: [
      {
        name: 'appsettings'
        properties: {
          APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.outputs.connectionString
          ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
          ASPNETCORE_ENVIRONMENT: 'Production'
          AzureFunctions__BaseUrl: 'https://${functionApp.outputs.defaultHostname}'
        }
      }
    ]
  }
}

// ═════════════════════════════════════════════════════════════════════════════
// Phase 4 — RBAC Role Assignments (managed identity → resources)
// ═════════════════════════════════════════════════════════════════════════════

module rbac 'modules/rbac.bicep' = {
  scope: rg
  params: {
    storageAccountName: storageAccountName
    serviceBusNamespaceName: serviceBusNamespaceName
    schedulerName: schedulerName
    aiFoundryAccountName: aiFoundryName
    functionAppName: functionAppName
    principalId: functionApp.outputs.?systemAssignedMIPrincipalId ?? ''
  }
}

// ═════════════════════════════════════════════════════════════════════════════
// Outputs
// ═════════════════════════════════════════════════════════════════════════════

@description('Name of the deployed Function App.')
output functionAppName string = functionApp.outputs.name

@description('Default hostname of the Function App.')
output functionAppHostname string = functionApp.outputs.defaultHostname

@description('Resource ID of the Function App.')
output functionAppResourceId string = functionApp.outputs.resourceId

@description('Durable Task Scheduler endpoint URL.')
output durableTaskSchedulerEndpoint string = durableTask.outputs.schedulerEndpoint

@description('Name of the Durable Task Hub.')
output durableTaskHubName string = durableTask.outputs.taskHubName

@description('Application Insights connection string.')
output applicationInsightsConnectionString string = applicationInsights.outputs.connectionString

@description('Service Bus namespace name.')
output serviceBusNamespaceName string = serviceBusNamespace.outputs.name

@description('AI Foundry account endpoint URL.')
output aiFoundryEndpoint string = aiFoundry.outputs.accountEndpoint

@description('AI Foundry account name.')
output aiFoundryAccountName string = aiFoundry.outputs.accountName

@description('AI Foundry project name.')
output aiFoundryProjectName string = aiFoundry.outputs.projectName

@description('AI Foundry model deployment name.')
output aiModelDeploymentName string = aiFoundry.outputs.deploymentName

@description('Name of the deployed Web App.')
output webAppName string = webApp.outputs.name

@description('Default hostname of the Web App.')
output webAppHostname string = webApp.outputs.defaultHostname
