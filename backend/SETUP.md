# Setup Guide - Document Uploader API

This guide walks you through setting up the Document Uploader API for local development and testing.

## Prerequisites

### Required
- .NET 9.0 SDK or later
- Azure subscription with access to create resources
- Azure CLI (`az` command)
- Visual Studio Code or Visual Studio 2022
- Docker (optional, for containerized setup)

### Azure Resources
- Azure AD/Entra ID tenant
- Azure Storage Account

## Step 1: Register Application in Azure AD

### 1.1 Register API Application

```bash
# Login to Azure
az login

# Set your tenant ID
TENANT_ID="your-tenant-id"

# Create app registration for the API
az ad app create \
  --display-name "Document Uploader API" \
  --is-api-app true \
  --web-redirect-uris "https://localhost:7071/auth/openid/callback" \
  --query "appId" -o tsv

# Save the API Client ID from output
API_CLIENT_ID="<saved-client-id>"
```

### 1.2 Create Client Secret

```bash
# Create a secret for the API
az ad app credential create \
  --id "$API_CLIENT_ID" \
  --display-name "API-Secret" \
  --query "password" -o tsv

# Save the secret (PASSWORD) - you'll need this
API_CLIENT_SECRET="<saved-secret>"
```

### 1.3 Expose API Permissions

```bash
# Get the service principal ID
SERVICE_PRINCIPAL_ID=$(az ad sp show \
  --id "$API_CLIENT_ID" \
  --query "id" -o tsv)

# Create an app scope
az ad app update \
  --id "$API_CLIENT_ID" \
  --add "api.oauth2PermissionScopes" \
  '[{
    "adminConsentDescription": "Allows the application to access the Document Uploader API",
    "adminConsentDisplayName": "Access Document Uploader API",
    "id": "'"$(uuidgen)"'",
    "isEnabled": true,
    "type": "User",
    "userConsentDescription": "Allows the application to access the Document Uploader API",
    "userConsentDisplayName": "Access Document Uploader API",
    "value": "access_as_user"
  }]'
```

## Step 2: Create Azure Storage Account

### 2.1 Create Storage Account

```bash
# Set variables
RESOURCE_GROUP="doc-uploader-rg"
LOCATION="eastus"
STORAGE_ACCOUNT="docuploader$(date +%s)sa"

# Create resource group
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

# Create storage account
az storage account create \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku "Standard_LRS" \
  --kind "StorageV2" \
  --access-tier "Hot" \
  --https-only true \
  --min-tls-version "TLS1_2"

# IMPORTANT: Disable public access
az storage account blob-service-properties update \
  --account-name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --enable-change-feed false \
  --enable-last-access-tracking false
```

### 2.2 Create Blob Container

```bash
# Create container (private)
az storage container create \
  --name "documents" \
  --account-name "$STORAGE_ACCOUNT" \
  --auth-mode login \
  --public-access "off"
```

## Step 3: Create Managed Identity (for API)

### 3.1 Create User-Assigned Managed Identity

```bash
IDENTITY_NAME="doc-uploader-api-identity"

# Create managed identity
az identity create \
  --name "$IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "id" -o tsv

# Save the identity ID
IDENTITY_ID=$(az identity show \
  --name "$IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "id" -o tsv)

# Get the principal ID for role assignment
PRINCIPAL_ID=$(az identity show \
  --name "$IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "principalId" -o tsv)
```

### 3.2 Assign Storage Blob Data Contributor Role

```bash
# Get storage account resource ID
STORAGE_ACCOUNT_ID=$(az storage account show \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "id" -o tsv)

# Assign role
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type "ServicePrincipal" \
  --scope "$STORAGE_ACCOUNT_ID"
```

## Step 4: Configure Local Development

### 4.1 Update appsettings.Development.json

```bash
cd doc-uploader-api

# Create local settings file
cat > appsettings.Development.json << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "$TENANT_ID.onmicrosoft.com",
    "TenantId": "$TENANT_ID",
    "ClientId": "$API_CLIENT_ID",
    "ClientSecret": "$API_CLIENT_SECRET"
  },
  "AzureBlobStorage": {
    "AccountName": "$STORAGE_ACCOUNT",
    "ContainerName": "documents"
  },
  "BlobUpload": {
    "MaxFileSizeBytes": 104857600,
    "SasTokenExpirationMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:7000",
      "https://localhost:5000",
      "http://localhost:3000"
    ]
  }
}
EOF
```

### 4.2 Set Environment Variables (Alternative to appsettings)

```bash
export AzureAd__TenantId="$TENANT_ID"
export AzureAd__ClientId="$API_CLIENT_ID"
export AzureAd__ClientSecret="$API_CLIENT_SECRET"
export AzureBlobStorage__AccountName="$STORAGE_ACCOUNT"
export AzureBlobStorage__ContainerName="documents"
```

## Step 5: Test Local Setup

### 5.1 Restore Dependencies

```bash
dotnet restore
```

### 5.2 Run the API

```bash
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7071
```

### 5.3 Verify with Curl

```bash
# Test health endpoint (no auth required)
curl -k https://localhost:7071/api/documentupload/health

# Expected response:
# {"status":"healthy","timestamp":"2024-01-15T12:00:00Z"}
```

## Step 6: Get Azure AD Token for Testing

### 6.1 Get Token via Azure CLI

```bash
# Get access token
TOKEN=$(az account get-access-token \
  --resource "api://$API_CLIENT_ID" \
  --query "accessToken" -o tsv)

echo $TOKEN
```

### 6.2 Test API Endpoint

```bash
# Test with token
curl -k -X POST https://localhost:7071/api/documentupload/sas-token \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.pdf",
    "fileSizeBytes": 1024000
  }'
```

## Step 7: Docker Setup (Optional)

### 7.1 Build Docker Image

```bash
docker build -t doc-uploader-api:latest .
```

### 7.2 Run with Docker Compose

```bash
# Create .env file
cat > .env << EOF
AZURE_TENANT_ID=$TENANT_ID
AZURE_CLIENT_ID=$API_CLIENT_ID
AZURE_CLIENT_SECRET=$API_CLIENT_SECRET
AZURE_STORAGE_ACCOUNT_NAME=$STORAGE_ACCOUNT
EOF

# Start containers
docker-compose up
```

## Troubleshooting

### Issue: "Unauthorized" when calling API

**Solution:** 
- Verify token is valid: `az account get-access-token --query "accessToken" -o tsv | jq -R 'split(".") | .[1] | @base64d | fromjson'`
- Check token hasn't expired
- Verify client ID and secret in appsettings match Azure AD registration

### Issue: Storage account not found

**Solution:**
- Verify storage account name in appsettings: `az storage account list --query "[].name"`
- Ensure storage account is in same subscription: `az account show`

### Issue: "Container 'documents' not found"

**Solution:**
```bash
# List containers
az storage container list --account-name "$STORAGE_ACCOUNT" --auth-mode login

# Create if missing
az storage container create \
  --name "documents" \
  --account-name "$STORAGE_ACCOUNT" \
  --auth-mode login
```

### Issue: CORS errors in Blazor app

**Solution:**
- Update `Cors.AllowedOrigins` in appsettings to include your Blazor app URL
- Ensure API is running with HTTPS
- Test with curl first to isolate CORS vs authentication issues

## Next Steps

1. ✅ Local development complete
2. See [TESTING.md](./TESTING.md) for API testing with Postman
3. See [DEPLOYMENT.md](./DEPLOYMENT.md) for Azure deployment
4. Configure Blazor app to use API endpoint from appsettings

## Environment Variables Reference

| Variable | Purpose | Example |
|----------|---------|---------|
| `AzureAd__TenantId` | Azure AD tenant | `12345678-1234-1234-1234-123456789012` |
| `AzureAd__ClientId` | API registration client ID | `87654321-4321-4321-4321-210987654321` |
| `AzureAd__ClientSecret` | API client secret | `abc123def456...` |
| `AzureBlobStorage__AccountName` | Storage account name | `docuploaderstorage` |
| `AzureBlobStorage__ContainerName` | Blob container name | `documents` |
| `BlobUpload__MaxFileSizeBytes` | Max file size | `104857600` (100MB) |
| `BlobUpload__SasTokenExpirationMinutes` | Token expiry | `30` |

## Cleanup

When done testing, clean up Azure resources:

```bash
# Delete resource group (deletes all resources)
az group delete \
  --name "$RESOURCE_GROUP" \
  --yes --no-wait

# Delete app registration
az ad app delete --id "$API_CLIENT_ID"
```
