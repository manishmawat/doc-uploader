# Document Uploader API

A production-ready ASP.NET Core API for securely uploading documents to Azure Blob Storage with SAS token-based authentication. This API implements the recommended pattern for client-side static web apps to upload files with time-limited, scoped access.

## Features

- ✅ **SAS Token Generation** - Time-limited access tokens for direct blob uploads
- ✅ **Azure AD/Entra ID Authentication** - Secure API access with JWT tokens
- ✅ **Managed Identity** - No secrets in configuration, uses Azure Managed Identity
- ✅ **File Validation** - Size and extension validation before token generation
- ✅ **Path Sanitization** - Prevents directory traversal attacks
- ✅ **CORS Support** - Configured for Blazor WebAssembly frontend
- ✅ **Health Checks** - Built-in health check endpoints
- ✅ **Logging & Monitoring** - Structured logging with Serilog
- ✅ **API Documentation** - Swagger/OpenAPI documentation included
- ✅ **Container Ready** - Docker and Docker Compose support
- ✅ **Infrastructure as Code** - Bicep templates for Azure deployment

## Architecture

```
┌─────────────────────────────────────┐
│   Blazor WebAssembly Client         │
│   1. POST /api/documentupload/sas-token
│   2. Returns SAS URI + signature
│   3. Direct upload to blob storage
└─────────────────────────────────────┘
         ↓ (HTTPS)
┌─────────────────────────────────────┐
│   Document Uploader API             │
│   - Validates user (Azure AD)       │
│   - Validates file metadata         │
│   - Generates scoped SAS token      │
│   - Logs audit trail                │
└─────────────────────────────────────┘
         ↓ (Managed Identity)
┌─────────────────────────────────────┐
│   Azure Blob Storage                │
│   - Direct client upload            │
│   - Token auto-expires after 30 min │
│   - No public access                │
└─────────────────────────────────────┘
```

## Quick Start

### Local Development

1. **Prerequisites**
   ```bash
   # Install .NET 9 SDK
   dotnet --version  # Should show 9.0.x
   ```

2. **Clone and configure**
   ```bash
   cd doc-uploader-api
   cp appsettings.Development.json appsettings.Local.json
   # Edit appsettings.Local.json with your Azure AD and storage account details
   ```

3. **Run locally**
   ```bash
   dotnet run
   # API will be available at https://localhost:7071
   # Swagger UI: https://localhost:7071/swagger
   ```

### Docker

```bash
# Build and run with Docker Compose
docker-compose up --build

# API will be available at http://localhost:7071
```

## API Endpoints

### 1. Generate SAS Token (Protected)
```http
POST /api/documentupload/sas-token
Authorization: Bearer {JWT_TOKEN}
Content-Type: application/json

{
  "fileName": "document.pdf",
  "fileSizeBytes": 1024000,
  "mimeType": "application/pdf"
}
```

**Response:**
```json
{
  "blobUri": "https://mystorageaccount.blob.core.windows.net/documents/user.email/document.pdf",
  "signature": "?sv=2021-06-08&ss=b&srt=sco&sp=racwd&se=2024-01-15T14:30:00Z&...",
  "expiresAt": "2024-01-15T14:30:00Z",
  "expiresInSeconds": 1800,
  "containerName": "documents"
}
```

### 2. Confirm Upload (Protected)
```http
POST /api/documentupload/confirm-upload?blobPath=user.email/document.pdf
Authorization: Bearer {JWT_TOKEN}
```

### 3. User Info (Protected)
```http
GET /api/documentupload/user-info
Authorization: Bearer {JWT_TOKEN}
```

### 4. Health Check (Public)
```http
GET /api/documentupload/health
```

## Configuration

### appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "YOUR_TENANT.onmicrosoft.com",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_API_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "AzureBlobStorage": {
    "AccountName": "YOUR_STORAGE_ACCOUNT_NAME",
    "ContainerName": "documents"
  },
  "BlobUpload": {
    "MaxFileSizeBytes": 104857600,
    "AllowedExtensions": [".pdf", ".docx", ".xlsx"],
    "SasTokenExpirationMinutes": 30
  },
  "Cors": {
    "AllowedOrigins": ["https://yourapp.com"]
  }
}
```

## Security Considerations

### ✅ Implemented

- Minimum TLS 1.2 for HTTPS
- JWT token validation via Azure AD
- SAS tokens scoped to specific blobs and time-limited (30 minutes)
- Path sanitization to prevent directory traversal
- File size and extension validation
- Blob storage allows no public access
- Managed Identity for authentication (no secrets in code)
- Structured logging for audit trails

### ⚠️ Additional Hardening

For production, also implement:
- Network firewall rules on storage account
- Private endpoints for blob storage
- IP whitelist if known IPs
- Enable blob versioning for audit trail
- Enable soft delete and immutable blobs for sensitive files
- Monitor and alert on unauthorized access attempts
- Implement rate limiting

## Testing

See [TESTING.md](./TESTING.md) for:
- Postman collection
- Local testing with curl
- Integration testing examples

## Deployment

See [DEPLOYMENT.md](./DEPLOYMENT.md) for:
- Azure CLI deployment
- Infrastructure setup with Bicep
- Configuration in Azure
- CI/CD pipeline examples

## API Reference

Full OpenAPI/Swagger documentation available at `/swagger` endpoint when running locally.

## License

MIT License - See LICENSE file

## Support

For issues or questions, please refer to the documentation files or create an issue in the repository.
