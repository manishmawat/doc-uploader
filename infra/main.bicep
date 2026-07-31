param location string = resourceGroup().location
param environment string = 'dev'
param appName string = 'doc-uploader'

// Resource naming
var staticWebAppName = '${appName}-${environment}-swa'
var storageAccountName = replace('${appName}${environment}storage', '-', '')
var tags = {
  environment: environment
  project: 'UploaderDoc'
  managed_by: 'azd'
}

// Static Web App
module staticWebApp 'staticwebapp.bicep' = {
  name: 'staticWebApp'
  params: {
    name: staticWebAppName
    location: location
    tags: tags
  }
}

// Storage Account for document uploads
module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    name: storageAccountName
    location: location
    tags: tags
  }
}

// Outputs for configuration
output staticWebAppUrl string = staticWebApp.outputs.url
output storageAccountName string = storage.outputs.name
output storageAccountKey string = storage.outputs.key
output storageContainerName string = 'documents'
