// ──────────────────────────────────────────────────────────────────────────────
// Module: RBAC Role Assignments
// ──────────────────────────────────────────────────────────────────────────────
// Assigns least-privilege roles to the Function App's managed identity for
// secure, keyless access to Storage, Service Bus, and Durable Task Scheduler.
// ──────────────────────────────────────────────────────────────────────────────

@description('Name of the Storage Account.')
param storageAccountName string

@description('Name of the Service Bus Namespace.')
param serviceBusNamespaceName string

@description('Name of the Durable Task Scheduler.')
param schedulerName string

@description('Name of the Function App (used for deterministic guid generation).')
param functionAppName string

@description('Principal ID of the Function App managed identity.')
param principalId string

// ─── Built-in RBAC role definition IDs ──────────────────────────────────────
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var durableTaskDataContributorRoleId = '0ad04412-c4d5-4796-b79c-f76d14c8d402'

// ─── Existing resource references ───────────────────────────────────────────

resource storageAccountRef 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource serviceBusNamespaceRef 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusNamespaceName
}

resource durableTaskSchedulerRef 'Microsoft.DurableTask/schedulers@2025-11-01' existing = {
  name: schedulerName
}

// ─── Storage Blob Data Contributor → Storage Account ────────────────────────
resource storageBlobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountName, storageBlobDataContributorRoleId, functionAppName)
  scope: storageAccountRef
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalType: 'ServicePrincipal'
  }
}

// ─── Service Bus Data Receiver → Service Bus Namespace ──────────────────────
resource serviceBusReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceName, serviceBusDataReceiverRoleId, functionAppName)
  scope: serviceBusNamespaceRef
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalType: 'ServicePrincipal'
  }
}

// ─── Service Bus Data Sender → Service Bus Namespace ────────────────────────
resource serviceBusSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceName, serviceBusDataSenderRoleId, functionAppName)
  scope: serviceBusNamespaceRef
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalType: 'ServicePrincipal'
  }
}

// ─── Durable Task Data Contributor → Durable Task Scheduler ─────────────────
resource durableTaskRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(schedulerName, durableTaskDataContributorRoleId, functionAppName)
  scope: durableTaskSchedulerRef
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', durableTaskDataContributorRoleId)
    principalType: 'ServicePrincipal'
  }
}
