// ──────────────────────────────────────────────────────────────────────────────
// Module: AI Foundry — Account + Project + Model Deployment
// ──────────────────────────────────────────────────────────────────────────────
// Deploys an Azure AI Foundry account (Cognitive Services with project
// management), a project, and an initial model deployment.
// No AVM module exists for this new resource pattern — raw Bicep.
// ──────────────────────────────────────────────────────────────────────────────

@description('Name of the AI Foundry account (2–64 chars, alphanumeric + hyphens). Also used as the custom subdomain.')
@minLength(2)
@maxLength(64)
param aiFoundryName string

@description('Name of the AI Foundry project (2–64 chars).')
@minLength(2)
@maxLength(64)
param aiProjectName string

@description('Azure region for deployment.')
param location string = resourceGroup().location

@description('Resource tags.')
param tags object = {}

// ─── Model Deployment Parameters ────────────────────────────────────────────

@description('Name of the OpenAI model to deploy.')
param modelName string = 'gpt-4.1-mini'

@description('Model format identifier.')
param modelFormat string = 'OpenAI'

@description('Model version string.')
param modelVersion string = '2025-04-14'

@description('SKU name for the model deployment (e.g. GlobalStandard, Standard, ProvisionedManaged).')
param modelSkuName string = 'GlobalStandard'

@description('Deployment capacity in TPM units.')
param modelCapacity int = 1

// ─── AI Foundry Account ─────────────────────────────────────────────────────
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: aiFoundryName
  location: location
  tags: tags
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: aiFoundryName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// ─── AI Foundry Project (child of Account) ──────────────────────────────────
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: aiFoundry
  name: aiProjectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// ─── Model Deployment (child of Account) ────────────────────────────────────
resource aiModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: aiFoundry
  name: modelName
  sku: {
    name: modelSkuName
    capacity: modelCapacity
  }
  properties: {
    model: {
      name: modelName
      format: modelFormat
      version: modelVersion
    }
  }
}

// ─── Outputs ────────────────────────────────────────────────────────────────

@description('Resource ID of the AI Foundry account.')
output accountResourceId string = aiFoundry.id

@description('Name of the AI Foundry account.')
output accountName string = aiFoundry.name

@description('Primary endpoint URL of the AI Foundry account.')
output accountEndpoint string = aiFoundry.properties.endpoint

@description('Name of the AI Foundry project.')
output projectName string = aiProject.name

@description('Name of the model deployment.')
output deploymentName string = aiModelDeployment.name
