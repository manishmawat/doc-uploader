param name string
param location string = resourceGroup().location
param tags object = {}

// Azure Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    buildProperties: {
      skipGithubActionWorkflowGeneration: false
    }
  }
}

// Custom domain (optional - can be configured post-deployment)
// Configure HTTPS redirects and SPA routing
resource spaRouting 'Microsoft.Web/staticSites/config@2023-12-01' = {
  name: '${name}/web'
  parent: staticWebApp
  properties: {
    // Fallback to index.html for SPA routing
    defaultFile: 'index.html'
  }
}

output id string = staticWebApp.id
output name string = staticWebApp.name
output url string = staticWebApp.properties.defaultHostname != null ? 'https://${staticWebApp.properties.defaultHostname}' : ''
output deploymentToken string = listSecrets(staticWebApp.id, '2023-12-01').properties.repositoryToken
