# 🚀 Local Development with Azurite

This guide walks you through running the Document Uploader API locally with **Azurite** (Azure Storage emulator) - no Azure subscription required!

## What is Azurite?

**Azurite** is Microsoft's local storage emulator that mimics Azure Blob Storage, Queue Storage, and Table Storage. Perfect for local development and testing.

**Benefits:**
- ✅ No Azure subscription needed
- ✅ No network latency
- ✅ Fast development iteration
- ✅ Identical API to Azure Storage
- ✅ Free and open-source

---

## Prerequisites

### Option A: Docker Desktop (Recommended)
- [Install Docker Desktop](https://www.docker.com/products/docker-desktop)
- Includes Docker Compose out-of-the-box
- All dependencies managed automatically

### Option B: Native Installation
- .NET 10 SDK: [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- Azurite: `npm install -g azurite` (requires Node.js)
- Or: Download [Azure Storage Emulator](https://go.microsoft.com/fwlink/?LinkId=717179) (Windows only)

---

## Quick Start (5 minutes)

### Method 1: Docker Compose (Easiest)

```bash
# Clone/navigate to project
cd c:\dev\Portfolio\DocumentUploader\doc-uploader-api

# Start all services (API + Azurite)
docker-compose up --build

# Verify services are running
# API: http://localhost:7071/swagger
# Azurite: http://localhost:10000 (blob storage)
```

**That's it!** Services start automatically, and azurite initializes the "documents" container.

### Method 2: Native .NET (Without Docker)

#### Step 1: Start Azurite

```bash
# Option A: Via NPM (requires Node.js installed)
npx azurite

# Option B: Windows Azure Storage Emulator
"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe" start
```

#### Step 2: Create Container (First Time Only)

```bash
# Download Azure CLI or use Python
# Using PowerShell + Azure SDK:
$env:AZURE_STORAGE_ACCOUNT = "devstoreaccount1"
$env:AZURE_STORAGE_KEY = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
$env:AZURE_STORAGE_ENDPOINT_SUFFIX = "core.windows.net"

# Use Azure Storage Explorer or Azure CLI:
az storage container create --name documents --account-name devstoreaccount1 --account-key "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==" --endpoint-url "http://127.0.0.1:10000"
```

#### Step 3: Run API

```bash
cd c:\dev\Portfolio\DocumentUploader\doc-uploader-api

# Restore and run
dotnet restore
dotnet run

# Output should show:
# Now listening on: http://localhost:5000
# Now listening on: https://localhost:5001
```

---

## Configuration

### Azurite Connection String

The connection string is pre-configured in `appsettings.Development.json`:

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;..."
  }
}
```

**Key Points:**
- `AccountName`: `devstoreaccount1` (fixed for azurite)
- `AccountKey`: Standard azurite key (shown above)
- `BlobEndpoint`: Points to `http://127.0.0.1:10000` (local)
- No Azure AD credentials needed (you can use dummy values for local testing)

### Override via Environment Variables

```bash
# Override azurite endpoint if running on different port
export AzureBlobStorage__ConnectionString="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=...;BlobEndpoint=http://azurite:10000/devstoreaccount1;..."

# Or for Azure AD testing:
export AzureAd__TenantId="your-tenant-id"
export AzureAd__ClientId="your-client-id"
export AzureAd__ClientSecret="your-client-secret"

dotnet run
```

---

## Testing Locally

### 1. Health Check (No Auth Required)

```bash
curl http://localhost:7071/api/documentupload/health
```

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T12:00:00Z"
}
```

### 2. Get SAS Token (Requires Mock JWT)

For local testing without real Azure AD:

```bash
# Create a mock JWT (for testing only)
# Use JWT.io to create token with payload:
# {
#   "preferred_username": "user@example.com",
#   "email": "user@example.com"
# }

# OR use the Postman collection with mock bearer token

curl -X POST http://localhost:7071/api/documentupload/sas-token \
  -H "Authorization: Bearer YOUR_MOCK_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.pdf",
    "fileSizeBytes": 1024000,
    "mimeType": "application/pdf"
  }'
```

### 3. Upload File with SAS Token

```bash
# Upload directly to azurite using SAS token
curl -X PUT "http://127.0.0.1:10000/devstoreaccount1/documents/user@example.com/test.pdf?sv=...&sig=..." \
  -H "x-ms-blob-type: BlockBlob" \
  --data-binary @test.pdf
```

### 4. Verify Upload with Azure Storage Explorer

1. Download [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/)
2. **Connect to Local Emulator:**
   - Select "Storage Accounts" → "+ Add account"
   - Choose "Attach to local emulator"
   - Connection will appear in left sidebar
3. **Browse Containers:**
   - Expand "Blob Containers"
   - Click "documents"
   - See uploaded files organized by user folder

---

## Postman Collection (Local Testing)

The project includes `doc-uploader-api.postman_collection.json` with pre-built requests.

### Setup for Local Testing:

1. **Import Collection:**
   - Open Postman
   - File → Import → Select `doc-uploader-api.postman_collection.json`

2. **Configure Environment Variables:**
   - Create new Environment
   - Set variables:
     ```
     base_url: http://localhost:7071
     token: (use any Bearer token for local testing)
     api_client_id: (dummy value)
     api_client_secret: (dummy value)
     tenant_id: (dummy value)
     ```

3. **Update Requests for Local Testing:**
   - Open **"Public: Health Check"** request
   - Send → Should return `{"status": "healthy"}`

4. **Mock Authentication (for testing SAS endpoints):**
   - The API validates JWT locally in Development
   - For testing, create a minimal JWT or update Program.cs to skip auth in Development:
     ```csharp
     // Temporary for local testing only
     if (app.Environment.IsDevelopment())
     {
         app.UseAuthorization(); // Can skip for pure local testing
     }
     ```

---

## Troubleshooting Local Development

### Problem: "Connection refused" on port 10000

**Solution:** Azurite not running
```bash
# Check if azurite is running:
docker-compose ps

# If not, restart:
docker-compose restart azurite

# Or start manually:
npx azurite
```

### Problem: "Container 'documents' not found"

**Solution:** Initialize container on first run
```bash
# Docker Compose: Automatically created via entrypoint
# Manual Azurite: Create it yourself:

az storage container create \
  --name documents \
  --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
```

### Problem: "401 Unauthorized" on SAS token endpoint

**Solution:** Mock authentication for local testing

**Option A:** Skip JWT validation locally (Development only)
```csharp
// In Program.cs
if (!app.Environment.IsDevelopment())
{
    app.UseAuthentication();
    app.UseAuthorization();
}
else
{
    app.UseRouting();
}
```

**Option B:** Use a test JWT from jwt.io with payload:
```json
{
  "preferred_username": "local-user@example.com",
  "email": "local-user@example.com",
  "aud": "your-client-id",
  "iss": "https://login.microsoftonline.com/your-tenant-id/v2.0"
}
```

### Problem: Slow performance / timeouts

**Solution:** Check resource allocation
```bash
# Docker: Increase resources
# Docker Desktop → Settings → Resources → Increase CPUs/Memory

# Restart with clean state:
docker-compose down
docker-compose up --build --force-recreate
```

### Problem: "403 Forbidden" on blob upload

**Solution:** SAS token signature mismatch
- Verify connection string has correct AccountKey
- Check BlobEndpoint matches running azurite port
- Verify container name matches ("documents")

---

## Development Workflow

### Scenario 1: Rapid API Testing

```bash
# Terminal 1: Start services
cd doc-uploader-api
docker-compose up

# Terminal 2: Make changes and rebuild
dotnet build
# API auto-reloads in Development mode

# Terminal 3: Test with curl or Postman
curl http://localhost:7071/swagger
```

### Scenario 2: Full Stack Testing (Blazor + API)

```bash
# Terminal 1: Start API with Azurite
cd doc-uploader-api
docker-compose up

# Terminal 2: Start Blazor frontend
cd ../doc-uploader
dotnet run --urls "https://localhost:7000"

# Terminal 3: Open browser
# https://localhost:7000
```

### Scenario 3: Debugging Blob Operations

```bash
# Terminal 1: Start services
docker-compose up

# Terminal 2: Open Azure Storage Explorer
# Connect to Local Emulator → Browse "documents" container
# Watch files appear in real-time as you upload

# Terminal 3: Tail API logs
docker logs -f doc-uploader-api
```

---

## Switching from Local to Azure

When ready to deploy to Azure:

### Step 1: Update appsettings.json (Production)

```json
{
  "AzureBlobStorage": {
    "AccountName": "your-prod-account-name",
    "ContainerName": "documents"
    // Remove ConnectionString - API uses Managed Identity
  }
}
```

### Step 2: Redeploy

```bash
# Bicep deployment
az deployment group create \
  --resource-group myResourceGroup \
  --template-file infra/main.bicep \
  --parameters location=eastus \
  --parameters environmentName=prod
```

**The API automatically detects production and uses Managed Identity instead of azurite!**

---

## Performance Characteristics (Local)

| Operation | Time | Notes |
|-----------|------|-------|
| Get SAS Token | 10-50ms | Network latency + crypto |
| Upload File (10MB) | 100-500ms | Disk I/O |
| List Blobs | 5-20ms | In-memory query |
| Delete Blob | 5-15ms | In-memory operation |

---

## Data Persistence

### Docker Compose

Azurite data is stored in Docker container:
```bash
# Containers are ephemeral - data lost on restart
docker-compose down  # Deletes all data

# To persist data:
docker-compose down -v  # Remove volumes

# Add volume to docker-compose.yml:
services:
  azurite:
    volumes:
      - azurite-data:/data
volumes:
  azurite-data:
```

### Native Azurite

Data stored locally by default:
```bash
# Default: ~/.azurite (macOS/Linux) or %APPDATA%\Azurite (Windows)
npx azurite --location /path/to/data

# View data:
ls ~/.azurite
```

---

## Resource Limits

### Local Development Azurite

- **Storage Capacity:** Unlimited (limited by disk space)
- **Concurrent Connections:** Hundreds
- **Blob Size:** Up to 512MB (default)
- **Container Size:** Unlimited

### Compared to Azure

| Aspect | Azurite | Azure Storage |
|--------|---------|---------------|
| **Cost** | Free | Pay per GB |
| **Latency** | <10ms | 50-200ms |
| **Scaling** | Single machine | Global |
| **SLA** | None | 99.99% |
| **Regions** | 1 (Local) | Multiple |

---

## Next Steps

✅ **Local Development Complete**
- [ ] Run `docker-compose up`
- [ ] Test with Postman collection
- [ ] Upload files and verify in Storage Explorer

🚀 **Ready for Azure?**
- [ ] Follow [DEPLOYMENT.md](DEPLOYMENT.md) for Azure deployment
- [ ] Configure Azure AD app registration
- [ ] Deploy API to App Service

📚 **Learn More**
- [Azurite Documentation](https://github.com/Azure/Azurite)
- [Azure Storage Emulator](https://docs.microsoft.com/azure/storage/common/storage-use-emulator)
- [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/)

---

## Quick Commands Reference

```bash
# Docker Compose
docker-compose up                    # Start all services
docker-compose down                  # Stop all services
docker-compose logs -f doc-uploader-api  # View API logs
docker-compose restart azurite       # Restart azurite

# .NET CLI
dotnet run                          # Run API
dotnet watch                        # Watch mode (auto-reload)
dotnet build                        # Build project
dotnet test                         # Run tests

# Azure CLI (with azurite)
az storage container create --name documents --connection-string "..."
az storage blob upload --file test.pdf --container-name documents --name test.pdf --connection-string "..."
```

---

**Happy local development! 🎉**
