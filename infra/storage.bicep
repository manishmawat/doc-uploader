param name string
param location string = resourceGroup().location
param tags object = {}

@description('Days a deleted document stays in the recycle bin before it is purged')
param recycleBinRetentionDays int = 14

// Azure Storage Account for document uploads
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob service
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

// Container for documents
resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: 'documents'
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

// Purge the app-level recycle bin: the API moves deleted documents to documents/.recycle-bin/
// (BlobStorageService.MoveToRecycleBinAsync). Keep in sync with BlobUpload:RecycleBinRetentionDays.
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    policy: {
      rules: [
        {
          name: 'purge-recycle-bin'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [ 'blockBlob' ]
              prefixMatch: [ 'documents/.recycle-bin/' ]
            }
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: recycleBinRetentionDays
                }
              }
            }
          }
        }
      ]
    }
  }
}

output id string = storageAccount.id
output name string = storageAccount.name
output key string = listKeys(storageAccount.id, '2023-01-01').keys[0].value
output endpoint string = storageAccount.properties.primaryEndpoints.blob
