param location string = resourceGroup().location
param environmentName string = 'dev'
param projectName string = 'doc-uploader'

param appServiceSkuName string = 'B2'
param appServiceSkuTier string = 'Basic'

param storageAccountType string = 'Standard_LRS'
param storageAccountAccessTier string = 'Hot'

// Tags for resource organization
param tags object = {
  environment: environmentName
  project: projectName
  createdBy: 'bicep'
  createdDate: utcNow('yyyy-MM-dd')
}

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = '${replace(projectName, '-', '')}${environmentName}${uniqueSuffix}'
var appServicePlanName = '${projectName}-asp-${environmentName}'
var appServiceName = '${projectName}-api-${environmentName}'
var containerRegistryName = '${replace(projectName, '-', '')}${environmentName}${uniqueSuffix}'
var managedIdentityName = '${projectName}-api-identity-${environmentName}'
var keyVaultName = '${replace(projectName, '-', '')}-kv-${environmentName}-${uniqueSuffix}'
var containerName = 'documents'

// Resource: Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
  tags: tags
}

// Resource: Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: storageAccountType
  }
  properties: {
    accessTier: storageAccountAccessTier
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false  // IMPORTANT: Disable public access
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'  // Change to 'Deny' and add IP rules in production
    }
  }
  tags: tags
}

// Resource: Blob Container
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/${containerName}'
  properties: {
    publicAccess: 'None'  // IMPORTANT: No public access
  }
}

// Role Assignment: Storage Blob Data Contributor to Managed Identity
resource storageBlobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, managedIdentity.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Resource: App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServiceSkuName
    tier: appServiceSkuTier
  }
  properties: {
    reserved: true
  }
  tags: tags
}

// Resource: App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'AZURE_CLIENT_ID'  // Lets DefaultAzureCredential pick the user-assigned identity
          value: managedIdentity.properties.clientId
        }
        {
          name: 'AzureBlobStorage__AccountName'
          value: storageAccount.name
        }
        {
          name: 'AzureBlobStorage__ContainerName'
          value: containerName
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '0'  // Set to 1 if deploying from package
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://+:80'
        }
      ]
      connectionStrings: []
    }
  }
  tags: tags
  dependsOn: [
    storageBlobRoleAssignment
  ]
}

// Outputs
output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityId string = managedIdentity.id
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output containerName string = containerName
