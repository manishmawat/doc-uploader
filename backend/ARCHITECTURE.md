# Document Uploader API - Architecture & Integration

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      CLIENT TIER                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Blazor WebAssembly Application                 │   │
│  │  - User authentication with Azure AD                   │   │
│  │  - File selection UI                                    │   │
│  │  - Requests SAS token from API                          │   │
│  │  - Uploads file directly to blob storage                │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            ↓ HTTPS
┌─────────────────────────────────────────────────────────────────┐
│                      API TIER                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Document Uploader API                           │   │
│  │  ASP.NET Core 9.0                                       │   │
│  │                                                         │   │
│  │  Endpoints:                                             │   │
│  │  • POST /api/documentupload/sas-token        [Protected]│   │
│  │  • POST /api/documentupload/confirm-upload   [Protected]│   │
│  │  • GET /api/documentupload/user-info         [Protected]│   │
│  │  • GET /api/documentupload/health            [Public]   │   │
│  │                                                         │   │
│  │  Authentication: Azure AD (JWT tokens)                  │   │
│  │  Logging: Serilog (structured logging)                  │   │
│  │  CORS: Configurable for Blazor app                      │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                  ↓ Managed Identity              ↓ Managed Identity
┌─────────────────────────────┐      ┌──────────────────────────────┐
│   Azure AD / Entra ID       │      │  Azure Blob Storage          │
│                             │      │                              │
│  • Validates JWT tokens     │      │  • Stores user documents     │
│  • Issues access tokens     │      │  • Isolated per user folder  │
│  • User identity source     │      │  • Private access only       │
│                             │      │  • Encryption at rest        │
│  Roles:                     │      │  • HTTPS only                │
│  • API resource owner       │      │                              │
│  • User authenticator       │      │  Access Control:             │
└─────────────────────────────┘      │  • Via SAS tokens (30 min)   │
                                     │  • Managed Identity RBAC     │
                                     │  • No public access          │
                                     └──────────────────────────────┘
```

## Request Flow: Getting a SAS Token

```
1. USER INTERACTION
   ┌──────────────┐
   │ Blazor App   │ User selects file
   └──────┬───────┘
          │
          ↓
2. AUTHENTICATION
   ┌──────────────────────────┐
   │ Azure AD              ◄──┼─ User already logged in
   │ (Token obtained)     │
   └──────┬───────────────┘
          │ JWT Token
          ↓
3. REQUEST SAS TOKEN
   ┌──────────────────────────────────────────┐
   │ POST /api/documentupload/sas-token       │
   │ Authorization: Bearer {JWT_TOKEN}        │
   │ {                                        │
   │   "fileName": "document.pdf",            │
   │   "fileSizeBytes": 1024000               │
   │ }                                        │
   └──────┬───────────────────────────────────┘
          │
          ↓
4. API VALIDATION
   ┌─────────────────────────────────────┐
   │ • Validate JWT signature & claims   │
   │ • Check file extension              │
   │ • Check file size                   │
   │ • Extract user identifier           │
   │ • Sanitize paths                    │
   └──────┬────────────────────────────────┘
          │
          ↓
5. SAS TOKEN GENERATION
   ┌──────────────────────────────────────┐
   │ • Create blob path: user/filename    │
   │ • Sign with storage key              │
   │ • Set 30-min expiration              │
   │ • Limited permissions (racwd)        │
   └──────┬─────────────────────────────────┘
          │
          ↓
6. RESPONSE TO CLIENT
   ┌────────────────────────────────────────┐
   │ {                                      │
   │   "blobUri": "https://...",            │
   │   "signature": "?sv=...&se=...",       │
   │   "expiresAt": "2024-01-15T14:30:00Z", │
   │   "expiresInSeconds": 1800             │
   │ }                                      │
   └──────┬──────────────────────────────────┘
          │
          ↓
7. DIRECT UPLOAD TO BLOB
   Blazor App combines blobUri + signature
   │
   └─ PUT https://storage.blob.core.windows.net/...?sv=...&se=...
      └─ Upload file directly (bypasses API)
         ↓
      8. Upload confirmation (optional)
         └─ POST /api/documentupload/confirm-upload
            └─ API verifies blob exists and returns metadata
```

## Security Model

### Authentication Layer
```
┌─────────────────────────────────────────────┐
│  All Protected Endpoints                    │
│                                             │
│  1. Request arrives with: Authorization:   │
│     Bearer {JWT_TOKEN}                      │
│                                             │
│  2. API validates token:                    │
│     ✓ Signature valid (Azure AD public key)│
│     ✓ Not expired                          │
│     ✓ Correct audience (Client ID)         │
│     ✓ Correct issuer (tenant ID)           │
│                                             │
│  3. Extract user claims:                    │
│     - oid (user ID)                         │
│     - preferred_username (email)            │
│     - upn (UPN)                             │
│                                             │
│  4. Continue processing (if valid) or      │
│     return 401 Unauthorized (if invalid)   │
└─────────────────────────────────────────────┘
```

### Authorization Layer (SAS Token)
```
┌─────────────────────────────────────────────┐
│  SAS Token Properties                       │
│                                             │
│  Scoped Access:                             │
│  - Resource: Single blob only               │
│  - Cannot access other blobs                │
│  - Cannot list containers                   │
│  - Cannot delete other resources            │
│                                             │
│  Time-Limited:                              │
│  - Valid for 30 minutes                     │
│  - Expires at specific UTC timestamp        │
│  - Automatic revocation (no manual action)  │
│                                             │
│  Permissions:                               │
│  - r: Read                                  │
│  - a: Add (append block)                    │
│  - c: Create                                │
│  - w: Write                                 │
│  - d: Delete                                │
│  - racwd: All above combined                │
│                                             │
│  User Isolation:                            │
│  - Blob path: {user-email}/{filename}       │
│  - Users can only upload to their folder    │
└─────────────────────────────────────────────┘
```

## Integration Points with Blazor App

### 1. Configuration Update

In Blazor `appsettings.json`:
```json
{
  "BlobStorage": {
    "SasApiEndpoint": "https://api.yourdomain.com/api/documentupload/sas-token",
    "ApiScope": "api://your-api-client-id/access_as_user"
  }
}
```

### 2. Service Update

Update `DocumentUploadService.cs`:

```csharp
public class DocumentUploadService : IDocumentUploadService
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    
    public async Task UploadDocumentAsync(Stream fileStream, string fileName)
    {
        // Step 1: Request SAS token from API
        var tokenRequest = new { 
            fileName = fileName, 
            fileSizeBytes = fileStream.Length 
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            "https://api.yourdomain.com/api/documentupload/sas-token",
            tokenRequest);
            
        var sasResponse = await response.Content.ReadAsAsync<SasTokenResponse>();
        
        // Step 2: Construct full SAS URI with signature
        var sasUri = new Uri($"{sasResponse.BlobUri}{sasResponse.Signature}");
        
        // Step 3: Upload file directly to blob storage
        var uploadResponse = await _httpClient.PutAsync(
            sasUri,
            new StreamContent(fileStream));
            
        return uploadResponse.IsSuccessStatusCode;
    }
}
```

### 3. Update Blazor Component

Update file upload component:

```razor
@page "/upload"
@inject IDocumentUploadService UploadService
@inject NavigationManager Nav

<div>
    <InputFile OnChange="@OnFileSelected" />
    <button @onclick="@OnUpload">Upload</button>
    @if (!string.IsNullOrEmpty(statusMessage))
    {
        <p>@statusMessage</p>
    }
</div>

@code {
    private IBrowserFile selectedFile;
    private string statusMessage = "";
    
    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        selectedFile = e.File;
    }
    
    private async Task OnUpload()
    {
        try
        {
            using var stream = selectedFile.OpenReadStream();
            await UploadService.UploadDocumentAsync(stream, selectedFile.Name);
            statusMessage = "File uploaded successfully!";
        }
        catch (Exception ex)
        {
            statusMessage = $"Error: {ex.Message}";
        }
    }
}
```

## Deployment Topology

### Development
```
Localhost
├── Blazor App     (https://localhost:7000)
├── API            (https://localhost:7071)
└── Storage        (Azurite emulator or Azure)
```

### Staging
```
Azure
├── Static Web App (Blazor)
├── App Service    (API)
├── Managed Identity
└── Storage Account
```

### Production
```
Azure (Multi-Region Ready)
├── Static Web App + CDN (Blazor)
├── App Service Plan + Slots (API)
├── Managed Identity + RBAC
├── Storage Account (GRS)
├── Application Insights
├── Key Vault
└── WAF + DDoS Protection
```

## Data Flow: Upload to Download

### Upload Flow
```
1. User selects file in Blazor UI
2. Blazor app requests SAS token from API
3. API validates user + file + returns SAS token
4. Blazor app uploads directly to blob using SAS
5. Blob storage receives file directly
6. (Optional) Blazor calls confirm-upload endpoint
7. API logs upload event
```

### Access Control
```
File Location: documents/user@example.com/report.pdf

Access:
- User authenticated via Azure AD
  ↓
- Requests SAS token (scoped to their blob)
  ↓
- Uses token for time-limited direct access
  ↓
- Token expires automatically

No direct access:
- Public/anonymous users: ✗ Blocked
- Other authenticated users: ✗ Path restricted
- API service: ✓ Via managed identity + RBAC
```

## Scalability Considerations

### Blob Storage
- ✅ Auto-scales to millions of blobs
- ✅ Geo-redundancy (GRS) for disaster recovery
- ✅ Hot tier for frequently accessed files
- ✅ Archive tier for long-term storage

### API
- ✅ Stateless design (scales horizontally)
- ✅ App Service Plan autoscaling
- ✅ Load balancer distributes requests
- ✅ Session affinity not required

### Performance
- ✅ Direct blob upload (bypasses API for data)
- ✅ API only handles tokens (lightweight)
- ✅ Blob storage can handle parallel uploads
- ✅ CDN for static Blazor assets

## Monitoring & Operations

### Logging Points
```
1. Authentication attempts (success/failure)
2. SAS token generation (user, file, timestamp)
3. File validation (size, type, extension)
4. Path sanitization (attempted vs sanitized)
5. Upload confirmation (blob size, timestamp)
6. Error events (with correlation IDs)
```

### Metrics to Track
```
- SAS token generation rate
- Average token generation latency
- Failed authentication attempts
- Invalid file rejections
- Blob storage API response times
- User upload patterns (trends)
```

### Alerts to Configure
```
- 401 Unauthorized spike (possible attack)
- 400 Bad Request spike (invalid files)
- Storage account quota approaching
- SAS token generation errors
- High API latency (>1s)
```

## Cost Estimation

### Monthly Costs (Typical)

| Component | Tier | Cost |
|-----------|------|------|
| Storage Account | Standard LRS | ~$5 |
| Blob Storage | Hot tier, 100 GB | ~$2 |
| App Service | B2 (Basic) | ~$15 |
| Static Web App | Standard | ~$10 |
| Application Insights | 5GB/day | ~$5 |
| **Total** | | **~$37/month** |

### Optimization Tips
- Use Cool tier for archival documents
- Auto-scale App Service (pay only for what you use)
- Set blob lifecycle policies for aging documents
- Monitor and optimize for unused storage

## Disaster Recovery

### Backup Strategy
```
1. Blob Storage
   - Geo-redundant (GRS) for automatic failover
   - Snapshots for point-in-time recovery

2. API Configuration
   - Infrastructure as Code (Bicep) for quick redeploy
   - Secrets in Key Vault (not lost in disaster)

3. Database (if added)
   - Geo-replicated backup
   - Point-in-time restore capability
```

### Recovery Time Objectives (RTO)
- **API Redeploy:** ~5 minutes (automated)
- **Storage Failover:** Automatic (~1-5 minutes)
- **Database Restore:** ~15 minutes

### Recovery Point Objectives (RPO)
- **Application State:** No data loss (stateless)
- **Blob Storage:** No data loss (GRS)
- **Logs:** Up to 24 hours (retention policy)
