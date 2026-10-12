# Quick Start - Document Uploader API

Get the API up and running in 5 minutes for local development.

## 1. Prerequisites

```bash
# Verify .NET SDK installed
dotnet --version
# Should output: 9.0.x or higher

# Verify Azure CLI installed
az --version
```

## 2. Login to Azure

```bash
az login
az account show
```

## 3. Quick Configuration (Local Development)

### Option A: Using Environment Variables (Fastest)

```bash
# Set these variables in your terminal/IDE
export TENANT_ID="your-tenant-id"
export API_CLIENT_ID="your-api-client-id"
export API_CLIENT_SECRET="your-api-client-secret"
export STORAGE_ACCOUNT="yourstorageaccount"

# Run the API
cd doc-uploader-api
dotnet run
```

### Option B: Using appsettings.Development.json

Edit `appsettings.Development.json`:
```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-api-client-id",
    "ClientSecret": "your-api-client-secret"
  },
  "AzureBlobStorage": {
    "AccountName": "yourstorageaccount"
  }
}
```

Then:
```bash
cd doc-uploader-api
dotnet run
```

## 4. Test the API

### Health Check (No Auth Required)
```bash
curl -X GET https://localhost:7071/api/documentupload/health
# Response: {"status":"healthy","timestamp":"2024-01-15T12:00:00Z"}
```

### Get SAS Token (Requires Auth)

First, get a token:
```bash
TOKEN=$(az account get-access-token \
  --resource "api://$API_CLIENT_ID" \
  --query "accessToken" -o tsv)
```

Then request SAS token:
```bash
curl -X POST https://localhost:7071/api/documentupload/sas-token \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.pdf",
    "fileSizeBytes": 1024000
  }'
```

Response:
```json
{
  "blobUri": "https://yourstorageaccount.blob.core.windows.net/documents/user/test.pdf",
  "signature": "?sv=2021-06-08&ss=b&srt=sco&sp=racwd&se=...",
  "expiresAt": "2024-01-15T14:30:00Z",
  "expiresInSeconds": 1800,
  "containerName": "documents"
}
```

## 5. Swagger Documentation

Open in browser:
```
https://localhost:7071/swagger
```

## 6. With Docker (Optional)

```bash
# Create .env file
cat > .env << EOF
AZURE_TENANT_ID=$TENANT_ID
AZURE_CLIENT_ID=$API_CLIENT_ID
AZURE_CLIENT_SECRET=$API_CLIENT_SECRET
AZURE_STORAGE_ACCOUNT_NAME=$STORAGE_ACCOUNT
EOF

# Run with Docker Compose
docker-compose up --build
```

API available at: http://localhost:7071

## 7. Integrate with Blazor App

Update your Blazor `appsettings.json`:

```json
{
  "BlobStorage": {
    "SasApiEndpoint": "https://localhost:7071/api/documentupload/sas-token",
    "ApiScope": "api://YOUR_API_CLIENT_ID/access_as_user"
  }
}
```

Update client to use new API endpoint:
```csharp
var sasTokenResponse = await _httpClient.PostAsJsonAsync(
    "https://localhost:7071/api/documentupload/sas-token",
    new { fileName = fileName, fileSizeBytes = fileSize }
);
```

## Common Issues & Solutions

### ❌ "Unauthorized" Response

```bash
# Check if token is valid
TOKEN=$(az account get-access-token \
  --resource "api://$API_CLIENT_ID" \
  --query "accessToken" -o tsv)

echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'
```

### ❌ "Storage account not found"

```bash
# Verify account name (lowercase, no hyphens)
az storage account list --query "[].name"
```

### ❌ "Port 7071 already in use"

```bash
# Use different port
dotnet run -- --urls "https://localhost:8071"
```

### ❌ Certificate validation error

For local development, use `-k` (insecure) flag with curl:
```bash
curl -k -X GET https://localhost:7071/api/documentupload/health
```

## Next Steps

- 📖 Read [README.md](./README.md) for architecture overview
- 🔒 Read [SECURITY.md](./SECURITY.md) for security best practices
- 🧪 Read [TESTING.md](./TESTING.md) for testing with Postman
- 🚀 Read [DEPLOYMENT.md](./DEPLOYMENT.md) for Azure deployment
- ⚙️ Read [SETUP.md](./SETUP.md) for detailed configuration

## Need Help?

1. Check the documentation files above
2. Review API Swagger docs at `/swagger`
3. Check logs: `dotnet run 2>&1 | grep -i "error"`
4. Verify Azure credentials: `az account show`
