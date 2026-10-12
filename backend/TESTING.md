# Testing Guide - Document Uploader API

This guide covers testing the Document Uploader API using various methods including Postman, curl, and integration tests.

## Testing Overview

### Test Scenarios

1. **Unauthenticated requests** - Should return 401
2. **Valid SAS token request** - Should return 200 with token
3. **Invalid file** - Should return 400
4. **File size validation** - Should validate and reject oversized files
5. **File extension validation** - Should reject unsupported extensions
6. **Upload confirmation** - Should return blob metadata

## Method 1: Testing with Postman

### 1.1 Import Postman Collection

Use the included Postman collection: `doc-uploader-api.postman_collection.json`

**Steps:**
1. Open Postman
2. Click "Import" → "Upload Files"
3. Select `doc-uploader-api.postman_collection.json`
4. Collection will be imported with all endpoints

### 1.2 Configure Environment Variables

Create a new Postman Environment with these variables:

```
base_url: http://localhost:7071
or
base_url: https://doc-uploader-api.azurewebsites.net

token: [You'll get this from login]
api_client_id: YOUR_API_CLIENT_ID
api_client_secret: YOUR_API_CLIENT_SECRET
tenant_id: YOUR_TENANT_ID
```

### 1.3 Get Azure AD Token

**Request:**
```http
POST https://login.microsoftonline.com/YOUR_TENANT_ID/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

client_id=YOUR_API_CLIENT_ID
&client_secret=YOUR_API_CLIENT_SECRET
&grant_type=client_credentials
&scope=api://YOUR_API_CLIENT_ID/.default
```

Or use Pre-request Script in Postman:

```javascript
// Pre-request Script to auto-refresh token
const getTokenRequest = {
    url: 'https://login.microsoftonline.com/{{tenant_id}}/oauth2/v2.0/token',
    method: 'POST',
    header: 'Content-Type: application/x-www-form-urlencoded',
    body: {
        mode: 'urlencoded',
        urlencoded: [
            { key: 'client_id', value: '{{api_client_id}}' },
            { key: 'client_secret', value: '{{api_client_secret}}' },
            { key: 'grant_type', value: 'client_credentials' },
            { key: 'scope', value: 'api://{{api_client_id}}/.default' }
        ]
    }
};

pm.sendRequest(getTokenRequest, (err, response) => {
    if (err) console.log(err);
    else {
        pm.environment.set('token', response.json().access_token);
    }
});
```

### 1.4 Test Endpoints

#### Health Check (No Auth Required)

```http
GET {{base_url}}/api/documentupload/health

Response:
{
    "status": "healthy",
    "timestamp": "2024-01-15T12:00:00Z"
}
```

#### Get SAS Token

```http
POST {{base_url}}/api/documentupload/sas-token
Authorization: Bearer {{token}}
Content-Type: application/json

Body:
{
    "fileName": "document.pdf",
    "fileSizeBytes": 1024000,
    "mimeType": "application/pdf"
}

Response:
{
    "blobUri": "https://storageaccount.blob.core.windows.net/documents/user.email/document.pdf",
    "signature": "?sv=2021-06-08&...",
    "expiresAt": "2024-01-15T14:30:00Z",
    "expiresInSeconds": 1800,
    "containerName": "documents"
}
```

#### Confirm Upload

```http
POST {{base_url}}/api/documentupload/confirm-upload
Authorization: Bearer {{token}}

Query params:
?blobPath=user.email/document.pdf

Response:
{
    "blobUri": "https://storageaccount.blob.core.windows.net/documents/user.email/document.pdf",
    "fileName": "document.pdf",
    "fileSizeBytes": 1024000,
    "uploadedAt": "2024-01-15T12:00:00Z"
}
```

#### Get User Info

```http
GET {{base_url}}/api/documentupload/user-info
Authorization: Bearer {{token}}

Response:
{
    "userId": "user-id-guid",
    "email": "user@example.com",
    "name": "User Name",
    "claims": [...]
}
```

## Method 2: Testing with cURL

### 2.1 Get Token

```bash
# Get Azure AD token
TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=$API_CLIENT_ID" \
  -d "client_secret=$API_CLIENT_SECRET" \
  -d "grant_type=client_credentials" \
  -d "scope=api://$API_CLIENT_ID/.default" \
  | jq -r '.access_token')

echo "Token: $TOKEN"
```

### 2.2 Test Health Endpoint

```bash
curl -X GET https://localhost:7071/api/documentupload/health \
  -H "Accept: application/json"
```

### 2.3 Test SAS Token Endpoint

```bash
curl -X POST https://localhost:7071/api/documentupload/sas-token \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.pdf",
    "fileSizeBytes": 1024000,
    "mimeType": "application/pdf"
  }'
```

### 2.4 Test Invalid File

```bash
# Should return 400 - file type not allowed
curl -X POST https://localhost:7071/api/documentupload/sas-token \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.exe",
    "fileSizeBytes": 1024000
  }'

# Expected response:
# {"code":"FILE_VALIDATION_FAILED","message":"File type '.exe' is not allowed...","requestId":"..."}
```

### 2.5 Test Unauthorized

```bash
# Should return 401 - missing token
curl -X POST https://localhost:7071/api/documentupload/sas-token \
  -H "Content-Type: application/json" \
  -d '{"fileName": "test.pdf"}'

# Expected response: 401 Unauthorized
```

## Method 3: Integration Testing

### 3.1 Create xUnit Test Project

```bash
dotnet new xunit -n DocUploaderApi.Tests
cd DocUploaderApi.Tests
dotnet add reference ../DocUploaderApi.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

### 3.2 Write API Tests

Create `DocumentUploadControllerTests.cs`:

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace DocUploaderApi.Tests;

public class DocumentUploadControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DocumentUploadControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_WithoutAuth_Returns200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/documentupload/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsAsync<dynamic>();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task SasToken_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { fileName = "test.pdf", fileSizeBytes = 1024000 };

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/documentupload/sas-token", 
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SasToken_InvalidFile_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { fileName = "test.exe" }; // Invalid extension

        // Note: In real test, add auth token
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/documentupload/sas-token", 
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

### 3.3 Run Tests

```bash
dotnet test
```

## Method 4: Full Upload Flow Test

### 4.1 End-to-End Flow

```bash
#!/bin/bash

set -e

# Variables
API_URL="https://localhost:7071"
TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=$API_CLIENT_ID" \
  -d "client_secret=$API_CLIENT_SECRET" \
  -d "grant_type=client_credentials" \
  -d "scope=api://$API_CLIENT_ID/.default" \
  | jq -r '.access_token')

echo "✓ Got token: ${TOKEN:0:20}..."

# Step 1: Request SAS token
echo "→ Requesting SAS token..."
SAS_RESPONSE=$(curl -s -X POST "$API_URL/api/documentupload/sas-token" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "test.pdf",
    "fileSizeBytes": 1024,
    "mimeType": "application/pdf"
  }')

echo "$SAS_RESPONSE" | jq .

BLOB_URI=$(echo "$SAS_RESPONSE" | jq -r '.blobUri')
SIGNATURE=$(echo "$SAS_RESPONSE" | jq -r '.signature')
EXPIRES_IN=$(echo "$SAS_RESPONSE" | jq -r '.expiresInSeconds')

echo "✓ SAS token received (expires in $EXPIRES_IN seconds)"

# Step 2: Create test file
echo "→ Creating test file..."
echo "This is a test PDF file" > test.pdf

# Step 3: Upload to blob storage using SAS
echo "→ Uploading file to blob storage..."
SAS_URI="${BLOB_URI}${SIGNATURE}"
curl -X PUT "$SAS_URI" \
  -H "x-ms-blob-type: BlockBlob" \
  -H "Content-Type: application/pdf" \
  --data-binary @test.pdf

echo "✓ File uploaded successfully"

# Step 4: Confirm upload
BLOB_PATH=$(echo "$BLOB_URI" | sed "s|.*/||")
echo "→ Confirming upload..."
CONFIRM_RESPONSE=$(curl -s -X POST "$API_URL/api/documentupload/confirm-upload" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"blobPath\": \"$BLOB_PATH\"}")

echo "$CONFIRM_RESPONSE" | jq .

echo "✓ Upload flow completed successfully!"
```

## Test Checklist

### Security Tests
- [ ] ✅ Unauthenticated request returns 401
- [ ] ✅ Expired token returns 401
- [ ] ✅ Invalid token returns 401
- [ ] ✅ SAS token has correct expiration
- [ ] ✅ SAS token is scoped to specific blob

### Validation Tests
- [ ] ✅ Invalid file extension rejected
- [ ] ✅ Oversized file rejected
- [ ] ✅ Empty filename rejected
- [ ] ✅ Valid file accepted

### Functionality Tests
- [ ] ✅ SAS token can be used to upload
- [ ] ✅ Upload confirmation returns correct metadata
- [ ] ✅ User info endpoint returns current user
- [ ] ✅ Health endpoint accessible without auth
- [ ] ✅ CORS headers present in response

### Performance Tests
- [ ] ✅ Token generation < 500ms
- [ ] ✅ Upload confirmation < 1s
- [ ] ✅ Can handle 10 concurrent requests
- [ ] ✅ Can generate 100 tokens/minute

## Load Testing with Apache JMeter

### 4.1 Create Test Plan

```xml
<?xml version="1.0" encoding="UTF-8"?>
<jmeterTestPlan version="1.2">
  <hashTree>
    <TestPlan guiclass="TestPlanGui" testname="Document Uploader API Load Test">
      <elementProp name="TestPlan.user_defined_variables" />
      <stringProp name="TestPlan.comments">Load test for SAS token generation</stringProp>
    </TestPlan>
    
    <!-- Thread Group: 10 users, 100 requests each -->
    <ThreadGroup guiclass="ThreadGroupGui" testname="Thread Group">
      <stringProp name="ThreadGroup.num_threads">10</stringProp>
      <stringProp name="ThreadGroup.ramp_time">10</stringProp>
      <stringProp name="ThreadGroup.duration">60</stringProp>
    </ThreadGroup>
    
    <!-- HTTP Sampler: SAS Token Request -->
    <HTTPSamplerProxy guiclass="HttpTestSampleGui" testname="Get SAS Token">
      <stringProp name="HTTPSampler.domain">localhost</stringProp>
      <stringProp name="HTTPSampler.port">7071</stringProp>
      <stringProp name="HTTPSampler.path">/api/documentupload/sas-token</stringProp>
      <stringProp name="HTTPSampler.method">POST</stringProp>
    </HTTPSamplerProxy>
  </hashTree>
</jmeterTestPlan>
```

## Monitoring API in Production

### View Real-Time Logs

```bash
# For App Service
az webapp log tail \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP"

# For Container App
az containerapp logs show \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP"
```

### Query Application Insights

```bash
# Query failed requests
az monitor log-analytics query \
  -w "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP/providers/microsoft.operationalinsights/workspaces/$WORKSPACE_NAME" \
  -q "requests | where resultCode >= 400"
```

## Common Test Issues & Solutions

### Issue: "CORS policy: Response to preflight request"

**Solution:** Ensure API running with correct CORS configuration and all origins properly configured.

### Issue: "Invalid token claims"

**Solution:** 
- Verify token is for correct resource (API Client ID)
- Check token hasn't expired
- Verify tenant ID in token matches configured

### Issue: "The file type '.pdf' is not allowed"

**Solution:** Add `.pdf` to `BlobUpload.AllowedExtensions` in appsettings

## Next Steps

1. ✅ API tested locally
2. See [DEPLOYMENT.md](./DEPLOYMENT.md) for production deployment
3. Setup CI/CD pipeline with automated tests
4. Configure alerts for test failures
5. Establish baseline performance metrics
