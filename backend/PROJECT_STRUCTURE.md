# Document Uploader API - Project Structure Summary

## 📁 Project Layout

```
doc-uploader-api/
├── Controllers/
│   └── DocumentUploadController.cs      # API endpoints (SAS token, confirm, user-info)
├── Models/
│   ├── StorageOptions.cs                # Configuration classes
│   └── BlobModels.cs                    # Request/Response DTOs
├── Services/
│   ├── IBlobStorageService.cs           # Service interface
│   └── BlobStorageService.cs            # SAS token generation, validation
├── infra/
│   └── main.bicep                       # Azure infrastructure as code
├── Program.cs                           # Application startup and DI config
├── DocUploaderApi.csproj                # Project file with NuGet dependencies
├── Dockerfile                           # Docker image definition
├── docker-compose.yml                   # Local Docker development
├── appsettings.json                     # Production configuration template
├── appsettings.Development.json         # Development configuration
├── global.json                          # .NET SDK version pinning
├── .gitignore                           # Git ignore rules
│
├── README.md                            # Overview and features
├── QUICKSTART.md                        # Get running in 5 minutes
├── SETUP.md                             # Detailed setup guide (Azure AD, storage, etc.)
├── DEPLOYMENT.md                        # Deploy to Azure (App Service, Container Apps)
├── ARCHITECTURE.md                      # System architecture and integration guide
├── SECURITY.md                          # Security model and best practices
├── TESTING.md                           # Testing with Postman and curl
│
└── doc-uploader-api.postman_collection.json  # Postman API collection

```

## 📋 File Descriptions

### Code Files

| File | Purpose |
|------|---------|
| `Program.cs` | Application startup, dependency injection, middleware configuration |
| `Controllers/DocumentUploadController.cs` | REST API endpoints for SAS token and upload operations |
| `Services/IBlobStorageService.cs` | Service interface for blob storage operations |
| `Services/BlobStorageService.cs` | Implementation of SAS token generation, validation, path sanitization |
| `Models/StorageOptions.cs` | Configuration DTOs for Azure Blob Storage and upload settings |
| `Models/BlobModels.cs` | Request/Response DTOs for API endpoints |
| `DocUploaderApi.csproj` | Project configuration and NuGet package dependencies |

### Configuration Files

| File | Purpose |
|------|---------|
| `appsettings.json` | Production configuration template (replace placeholders) |
| `appsettings.Development.json` | Local development configuration |
| `global.json` | Ensures .NET 9.0 SDK is used |

### Infrastructure Files

| File | Purpose |
|------|---------|
| `infra/main.bicep` | Azure infrastructure (Storage, App Service, Managed Identity, RBAC) |
| `Dockerfile` | Multi-stage Docker image for containerization |
| `docker-compose.yml` | Local Docker development environment |

### Documentation Files

| File | Purpose | Read When |
|------|---------|-----------|
| `README.md` | Overview, architecture, features | First - understand the project |
| `QUICKSTART.md` | Get running in 5 minutes | Want to try it immediately |
| `SETUP.md` | Detailed setup with Azure AD, Storage, Managed Identity | Setting up for first time |
| `DEPLOYMENT.md` | Deploy to Azure App Service, Container Apps, CI/CD | Ready to deploy to Azure |
| `ARCHITECTURE.md` | System design, data flows, integration | Need to understand how it works |
| `SECURITY.md` | Threat model, mitigations, compliance | Before production deployment |
| `TESTING.md` | Postman, curl, xUnit tests, load testing | Testing endpoints |

### Testing Files

| File | Purpose |
|------|---------|
| `doc-uploader-api.postman_collection.json` | Pre-configured Postman collection for API testing |

## 🚀 Quick Reference: Where to Find...

### I want to...
- **Understand what this API does** → `README.md`
- **Run it locally right now** → `QUICKSTART.md`
- **Set up from scratch** → `SETUP.md`
- **Deploy to Azure** → `DEPLOYMENT.md`
- **See how it works** → `ARCHITECTURE.md`
- **Understand security** → `SECURITY.md`
- **Test the endpoints** → `TESTING.md` + `doc-uploader-api.postman_collection.json`
- **See the code** → `Services/` and `Controllers/` folders
- **Change configuration** → `appsettings.json` or `appsettings.Development.json`
- **Deploy infrastructure** → `infra/main.bicep`
- **Containerize** → `Dockerfile` and `docker-compose.yml`

## 📦 NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Azure.Identity` | 1.11.4 | Managed Identity authentication |
| `Azure.Storage.Blobs` | 12.21.0 | Blob Storage SDK |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.0 | JWT token validation |
| `Microsoft.Identity.Web` | 2.19.0 | Azure AD integration |
| `Serilog` | 3.1.1 | Structured logging |
| `Serilog.AspNetCore` | 8.0.0 | ASP.NET Core logging |
| `Swashbuckle.AspNetCore` | 6.4.6 | Swagger/OpenAPI documentation |

## 🔧 Configuration Sections

### `AzureAd` Section
```json
{
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "your-tenant.onmicrosoft.com",
  "TenantId": "your-tenant-id",
  "ClientId": "your-api-client-id",
  "ClientSecret": "your-client-secret"
}
```

### `AzureBlobStorage` Section
```json
{
  "AccountName": "yourstorageaccount",
  "ContainerName": "documents"
}
```

### `BlobUpload` Section
```json
{
  "MaxFileSizeBytes": 104857600,
  "AllowedExtensions": [".pdf", ".docx", ".xlsx"],
  "SasTokenExpirationMinutes": 30
}
```

### `Cors` Section
```json
{
  "AllowedOrigins": ["https://your-blazor-app.com"]
}
```

## 🏗️ Architecture Overview

```
Blazor WebAssembly (Client)
            ↓ (HTTPS + JWT)
Document Uploader API (Server)
    ├─ Authentication: Azure AD
    ├─ Authorization: JWT validation
    └─ Core: SAS token generation
            ↓ (Managed Identity)
Azure Blob Storage (Data)
    ├─ User-specific folders
    ├─ Time-limited access
    └─ No public access
```

## 🔐 Security Features

✅ JWT authentication via Azure AD
✅ SAS token generation (30-min expiration)
✅ Path sanitization (prevents directory traversal)
✅ File validation (size + extension)
✅ User isolation (blob per user folder)
✅ Managed Identity (no secrets in code)
✅ HTTPS enforcement
✅ Structured audit logging

## 📈 Performance & Scalability

- **Stateless API** - Scales horizontally
- **Direct blob upload** - Bypasses API for data transfer
- **SAS tokens** - Lightweight generation (~50ms)
- **Managed Identity** - Automatic credential rotation
- **Storage Account** - Auto-scales to millions of blobs

## 🧪 Testing

- ✅ Health endpoint (no auth required)
- ✅ SAS token endpoint (JWT required)
- ✅ Upload confirmation (JWT required)
- ✅ User info endpoint (JWT required)
- ✅ Invalid file rejection
- ✅ Oversized file rejection
- ✅ Unauthorized access rejection

Test with Postman collection included: `doc-uploader-api.postman_collection.json`

## 🚀 Deployment Options

1. **App Service** (Recommended for beginners)
2. **Container Apps** (Serverless, more flexible)
3. **Kubernetes/AKS** (Advanced, enterprise)

All deployments use same code - only infrastructure changes.

## 📊 Typical Workflow

1. Clone or setup API locally
2. Configure Azure AD and Storage Account
3. Update `appsettings.json`
4. Run `dotnet run` or `docker-compose up`
5. Test with Postman collection
6. Deploy to Azure
7. Update Blazor app configuration
8. Integrated end-to-end testing

## ⚠️ Important Notes

- **Never commit secrets** - Use Key Vault in production
- **HTTPS everywhere** - Required for JWT tokens and SAS
- **Firewall blob storage** - Limit access to app only
- **Rotate credentials** - Monthly storage key rotation recommended
- **Monitor logs** - Setup Application Insights
- **Test before deploying** - Use TESTING.md guide

## 📚 Further Reading

- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/)
- [Azure Storage Security](https://learn.microsoft.com/en-us/azure/storage/common/storage-security-guide)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [SAS Token Documentation](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview)
