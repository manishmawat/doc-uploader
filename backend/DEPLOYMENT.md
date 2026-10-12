# Deployment Guide - Document Uploader API to Azure

This guide covers deploying the Document Uploader API to Azure Container Apps or App Service.

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- Resource group created
- All setup steps from [SETUP.md](./SETUP.md) completed

## Option 1: Deploy to Azure App Service (Recommended for beginners)

### 1.1 Create App Service Plan

```bash
# Variables
RESOURCE_GROUP="doc-uploader-rg"
LOCATION="eastus"
APP_SERVICE_PLAN="doc-uploader-api-plan"
APP_SERVICE_NAME="doc-uploader-api-$(date +%s)"

# Create App Service Plan (Linux, B2)
az appservice plan create \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --sku "B2" \
  --is-linux
```

### 1.2 Create App Service

```bash
# Create app service with .NET 9 runtime
az webapp create \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --runtime "DOTNETCORE:9.0" \
  --runtime-version-json '{"linux":"9.0"}'
```

### 1.3 Enable Managed Identity

```bash
# Enable system-assigned managed identity
az webapp identity assign \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --identities "[system]"

# Get the principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "principalId" -o tsv)
```

### 1.4 Assign Storage Permissions

```bash
# Get storage account ID
STORAGE_ACCOUNT_ID=$(az storage account show \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "id" -o tsv)

# Assign Storage Blob Data Contributor role
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type "ServicePrincipal" \
  --scope "$STORAGE_ACCOUNT_ID"
```

### 1.5 Configure App Settings

```bash
# Set environment variables in App Service
az webapp config appsettings set \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    AzureAd__Instance="https://login.microsoftonline.com/" \
    AzureAd__Domain="$TENANT_ID.onmicrosoft.com" \
    AzureAd__TenantId="$TENANT_ID" \
    AzureAd__ClientId="$API_CLIENT_ID" \
    AzureAd__ClientSecret="$API_CLIENT_SECRET" \
    AzureBlobStorage__AccountName="$STORAGE_ACCOUNT" \
    AzureBlobStorage__ContainerName="documents" \
    BlobUpload__MaxFileSizeBytes="104857600" \
    BlobUpload__SasTokenExpirationMinutes="30" \
    Cors__AllowedOrigins="https://your-blazor-app.azurewebsites.net"
```

### 1.6 Deploy Code via Git

#### Option A: Deploy from ZIP

```bash
# Build release
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../DocUploaderApi.zip .
cd ..

# Deploy to App Service
az webapp deployment source config-zip \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --src "./DocUploaderApi.zip"
```

#### Option B: Deploy from Local Git

```bash
# Configure local git deployment
az webapp deployment user set \
  --user-name "git-deployment-user" \
  --password "secure-password-here"

# Get git URL
GIT_URL=$(az webapp deployment source config-local-git \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "url" -o tsv)

# Add remote (from project root)
git remote add azure "$GIT_URL"

# Deploy
git push azure main
```

#### Option C: Deploy from GitHub (Recommended for CI/CD)

```bash
# Create GitHub deployment
az webapp deployment github-actions add \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --repo "https://github.com/yourusername/DocumentUploader" \
  --branch "main" \
  --runtime "dotnet" \
  --runtime-version "9.0"
```

### 1.7 Verify Deployment

```bash
# Get app URL
APP_URL=$(az webapp show \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "defaultHostName" -o tsv)

# Test health endpoint
curl -X GET "https://$APP_URL/api/documentupload/health"

# Expected response:
# {"status":"healthy","timestamp":"2024-01-15T12:00:00Z"}
```

## Option 2: Deploy to Azure Container Apps (Serverless)

### 2.1 Create Container Registry

```bash
REGISTRY_NAME="docuploadersacr"

# Create container registry
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$REGISTRY_NAME" \
  --sku "Basic" \
  --admin-enabled true

# Get registry login server
REGISTRY_LOGIN_SERVER=$(az acr show \
  --name "$REGISTRY_NAME" \
  --query "loginServer" -o tsv)
```

### 2.2 Build and Push Docker Image

```bash
# Build and push to ACR
az acr build \
  --registry "$REGISTRY_NAME" \
  --image "doc-uploader-api:latest" \
  --file "Dockerfile" \
  .

# Or build locally and push
docker build -t "$REGISTRY_LOGIN_SERVER/doc-uploader-api:latest" .
docker push "$REGISTRY_LOGIN_SERVER/doc-uploader-api:latest"
```

### 2.3 Create Container Apps Environment

```bash
ENVIRONMENT_NAME="doc-uploader-env"

# Create environment
az containerapp env create \
  --name "$ENVIRONMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION"
```

### 2.4 Deploy Container App

```bash
CONTAINER_APP_NAME="doc-uploader-api-app"

# Create container app
az containerapp create \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT_NAME" \
  --image "$REGISTRY_LOGIN_SERVER/doc-uploader-api:latest" \
  --registry-server "$REGISTRY_LOGIN_SERVER" \
  --registry-username "$REGISTRY_USERNAME" \
  --registry-password "$REGISTRY_PASSWORD" \
  --target-port 80 \
  --ingress external \
  --query properties.configuration.ingress.fqdn -o tsv
```

### 2.5 Configure Environment Variables

```bash
# Update container app settings
az containerapp update \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT="Production" \
    AzureAd__Instance="https://login.microsoftonline.com/" \
    AzureAd__Domain="$TENANT_ID.onmicrosoft.com" \
    AzureAd__TenantId="$TENANT_ID" \
    AzureAd__ClientId="$API_CLIENT_ID" \
    AzureAd__ClientSecret="$API_CLIENT_SECRET" \
    AzureBlobStorage__AccountName="$STORAGE_ACCOUNT" \
    AzureBlobStorage__ContainerName="documents"
```

### 2.6 Assign Managed Identity

```bash
# Get managed identity created for Container App
PRINCIPAL_ID=$(az containerapp show \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "identity.principalId" -o tsv)

# Assign Storage Blob Data Contributor role
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type "ServicePrincipal" \
  --scope "$STORAGE_ACCOUNT_ID"
```

## Option 3: Infrastructure as Code with Bicep

### 3.1 Deploy with Bicep

```bash
# Deploy infrastructure
az deployment group create \
  --name "doc-uploader-deployment" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "infra/main.bicep" \
  --parameters \
    location="$LOCATION" \
    environmentName="prod" \
    projectName="doc-uploader"
```

### 3.2 Update App Settings

```bash
# Get outputs from deployment
APP_SERVICE_NAME=$(az deployment group show \
  --name "doc-uploader-deployment" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.outputs.appServiceName.value" -o tsv)

# Set configuration
az webapp config appsettings set \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    AzureAd__TenantId="$TENANT_ID" \
    AzureAd__ClientId="$API_CLIENT_ID" \
    AzureAd__ClientSecret="$API_CLIENT_SECRET"
```

## Step 3: Configure Blazor App

Update your Blazor app's `appsettings.json` to point to the deployed API:

```json
{
  "BlobStorage": {
    "SasApiEndpoint": "https://your-deployed-api.azurewebsites.net/api/documentupload/sas-token",
    "ApiScope": "api://YOUR_API_CLIENT_ID/access_as_user"
  }
}
```

## Step 4: Enable HTTPS Only

```bash
# For App Service
az webapp update \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --https-only true

# For Container App
az containerapp ingress enable \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --type external \
  --allow-insecure false
```

## Step 5: Setup Custom Domain (Optional)

```bash
# For App Service
az webapp config hostname add \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --hostname "api.yourdomain.com"

# Create SSL certificate binding (requires certificate)
az webapp config ssl bind \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --certificate-thumbprint "$CERT_THUMBPRINT" \
  --ssl-type "SNI"
```

## Step 6: Setup CI/CD with GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

env:
  AZURE_WEBAPP_NAME: doc-uploader-api
  AZURE_RESOURCE_GROUP: doc-uploader-rg
  DOTNET_VERSION: '9.0.x'

jobs:
  deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish -c Release -o ./publish
    
    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
        package: ./publish
```

## Monitoring & Logging

### Enable Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --app "doc-uploader-api-insights" \
  --location "$LOCATION" \
  --resource-group "$RESOURCE_GROUP" \
  --application-type "web"

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app "doc-uploader-api-insights" \
  --resource-group "$RESOURCE_GROUP" \
  --query "instrumentationKey" -o tsv)

# Add to app settings
az webapp config appsettings set \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$INSTRUMENTATION_KEY"
```

### Setup Alerts

```bash
# Alert on HTTP 5xx errors
az monitor metrics alert create \
  --name "doc-uploader-api-500-errors" \
  --resource-group "$RESOURCE_GROUP" \
  --scopes "/subscriptions/{subscription}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$APP_SERVICE_NAME" \
  --condition "avg Http5xx > 5" \
  --window-size "5m" \
  --evaluation-frequency "1m" \
  --action "create-or-update-action-group" "action-group-name"
```

## Cleanup

```bash
# Delete resource group (removes all resources)
az group delete \
  --name "$RESOURCE_GROUP" \
  --yes --no-wait

# Or delete individual resources
az webapp delete \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP"

az appservice plan delete \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --yes
```

## Troubleshooting Deployments

### App Service won't start

```bash
# Check recent logs
az webapp log tail \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP"

# View deployment status
az webapp deployment list \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP"
```

### Container App can't authenticate to storage

```bash
# Verify role assignment
az role assignment list \
  --assignee "$PRINCIPAL_ID" \
  --resource-group "$RESOURCE_GROUP"

# Verify storage account firewall isn't blocking
az storage account show \
  --name "$STORAGE_ACCOUNT" \
  --query "properties.networkAcls"
```

## Next Steps

1. ✅ API deployed to Azure
2. Update Blazor app configuration
3. See [TESTING.md](./TESTING.md) for testing deployed API
4. Setup monitoring and alerts
5. Configure backup and disaster recovery
