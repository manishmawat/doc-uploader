# Security Guide - Document Uploader API

This document outlines the security architecture, best practices, and hardening recommendations for the Document Uploader API.

## Security Architecture

### 1. Authentication & Authorization

#### Current Implementation
- **Protocol:** OAuth 2.0 / OpenID Connect
- **Provider:** Azure AD / Microsoft Entra ID
- **Token Type:** JWT (JSON Web Tokens)
- **Token Validation:** Microsoft.Identity.Web library

#### How It Works
```
1. User logs in to Blazor app with Azure AD credentials
2. Blazor app acquires access token for API
3. Client sends request with: Authorization: Bearer {JWT}
4. API validates token signature, expiration, and claims
5. Request processed with authenticated user context
```

#### Best Practices
✅ Always use HTTPS for token transmission
✅ Tokens expire after short period (typically 1 hour)
✅ Refresh tokens used to get new access tokens
✅ Never store refresh tokens in browser localStorage
✅ Use secure cookies (HttpOnly, Secure, SameSite)

### 2. SAS Token Generation

#### Security Model
```
Time-Limited Access:
- SAS tokens expire after 30 minutes (configurable)
- Client can only use token within expiration window
- Cannot extend or modify expiration after issuance

Scoped Permissions:
- Each token grants access to single blob only
- Permissions limited to: Read, Add, Create, Write, Delete (for upload)
- Cannot list containers or blobs
- Cannot download other blobs

User Isolation:
- Blobs stored in user-specific folder: {user-email}/
- Users can only upload to their own folder
- API enforces path sanitization to prevent traversal
```

#### SAS Token Anatomy
```
https://account.blob.core.windows.net/container/blob-path?sv=2021-06-08&ss=b&srt=sco&sp=racwd&se=2024-01-15T14:30:00Z&sig=...

Components:
- sv     : Signed version (2021-06-08)
- ss     : Signed services (b=blob)
- srt    : Signed resource types (sco=service,container,object)
- sp     : Signed permissions (racwd=read,add,create,write,delete)
- se     : Signed expiration (UTC timestamp)
- sig    : HMAC-SHA256 signature (account key)
```

### 3. Blob Storage Access Control

#### Container Configuration
```json
{
  "publicAccess": "None",           // No anonymous access
  "defaultEncryption": true,        // Encryption at rest
  "https_only": true,               // Require HTTPS
  "minimumTlsVersion": "TLS1_2"     // TLS 1.2 minimum
}
```

#### Network Configuration
```
- allowBlobPublicAccess: false      // Disable public access
- networkAcls.defaultAction: Deny   // Default deny (production)
- networkAcls.bypass: AzureServices // Allow Azure services
```

#### Managed Identity
- No connection strings or account keys in configuration
- App Service/Container App uses Managed Identity
- Permissions granted via RBAC role: Storage Blob Data Contributor
- Automatic credential rotation handled by Azure

### 4. API Security Features

#### Input Validation
✅ File name validation and sanitization
✅ File size validation (max 100 MB configurable)
✅ File extension whitelist (configurable)
✅ User identifier validation
✅ Path traversal protection via regex

#### Output Security
✅ No sensitive data in error messages
✅ No stack traces exposed to clients
✅ Request ID correlation for security logs
✅ Proper HTTP status codes (not leaking information)

#### Transport Security
✅ HTTPS enforced (min TLS 1.2)
✅ HSTS header recommended
✅ Secure cookie flags (HttpOnly, Secure, SameSite=Strict)
✅ CORS restricted to known origins

#### Logging & Auditing
✅ All authentication attempts logged
✅ All SAS token generations logged with user ID
✅ All upload confirmations logged
✅ Structured logging for SIEM integration
✅ No secrets logged (tokens, passwords)

## Security Configuration Checklist

### ✅ Development Environment
- [ ] HTTPS enabled
- [ ] CORS configured for local clients only
- [ ] Secrets in appsettings.Development.json
- [ ] Debug logging enabled
- [ ] Health endpoint accessible

### ✅ Staging Environment
- [ ] HTTPS with valid certificate
- [ ] CORS configured for staging app URLs
- [ ] Secrets stored in Azure Key Vault
- [ ] Information-level logging
- [ ] Monitoring and alerting enabled

### ✅ Production Environment
- [ ] HTTPS with valid certificate (wildcard or specific domain)
- [ ] CORS restricted to production Blazor app URL only
- [ ] ALL secrets in Azure Key Vault
- [ ] Warning-level logging (less verbose)
- [ ] Application Insights for monitoring
- [ ] WAF (Web Application Firewall) enabled
- [ ] DDoS protection enabled
- [ ] IP whitelist if applicable

## Threat Model & Mitigations

### 1. Unauthorized API Access

**Threat:** Attacker calls API without valid JWT token

**Mitigations:**
- ✅ JWT validation required on all endpoints
- ✅ Token expiration enforcement
- ✅ Token signature verification via Azure AD public keys
- ✅ Rate limiting (recommended addition)

**Additional Hardening:**
```csharp
// Add rate limiting middleware
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(policyName: "TokenLimit",
        configure: options =>
        {
            options.PermitLimit = 10;              // 10 requests
            options.Window = TimeSpan.FromMinutes(1); // per minute
        });
});
```

### 2. Token Replay Attack

**Threat:** Attacker intercepts and reuses SAS token

**Mitigations:**
- ✅ Short expiration time (30 minutes)
- ✅ HTTPS encryption (prevents interception)
- ✅ SAS token bound to single blob
- ✅ Account key rotated periodically

**Additional Hardening:**
- Implement token blacklist for revoked tokens
- Use client certificate for mutual TLS (mTLS)
- Implement request signing in addition to SAS

### 3. Path Traversal Attack

**Threat:** Attacker uploads to unauthorized paths like `../../admin/`

**Mitigations:**
- ✅ Path sanitization with regex
- ✅ User folder isolation (user.email/)
- ✅ Removal of `..` sequences
- ✅ Removal of `/` and `\` characters

**Code Example:**
```csharp
private static string SanitizePath(string input)
{
    var sanitized = input
        .Replace("\\", "-")
        .Replace("/", "-")
        .Replace("..", "");
    
    sanitized = Regex.Replace(sanitized, @"[^\w\s\.\-@]", "");
    return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
}
```

### 4. File Upload Malware

**Threat:** Attacker uploads malicious file

**Mitigations:**
- ✅ File extension whitelist
- ✅ File size limit
- ✅ MIME type validation
- ✅ Store in isolated container

**Additional Hardening:**
```csharp
// Add malware scanning with Azure Defender
// Integrate with VirusTotal or ClamAV API for scanning
public async Task<bool> ScanFileForMalware(Stream fileStream, string fileName)
{
    // Call scanning service
    // Return true if safe, false if malware detected
}
```

### 5. Storage Account Key Exposure

**Threat:** Account key leaked, attacker gets full blob access

**Mitigations:**
- ✅ No account keys in code/config
- ✅ Managed Identity used instead
- ✅ SAS tokens have limited scope
- ✅ Account key rotation in Azure portal

**Additional Hardening:**
```bash
# Rotate account key every 90 days
az storage account keys renew \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --key "primary"
```

### 6. Denial of Service (DoS)

**Threat:** Attacker floods API with requests

**Mitigations:**
- ✅ Rate limiting (recommended)
- ✅ API throttling in Azure (recommended)
- ✅ Azure DDoS Protection Standard

**Implementation:**
```csharp
[RateLimiters("TokenLimit")]
[HttpPost("sas-token")]
public async Task<ActionResult<SasTokenResponse>> GetSasToken(...)
{
    // Rate-limited endpoint
}
```

### 7. Data Breach - Blob Access

**Threat:** Attacker gains access to storage account and reads all blobs

**Mitigations:**
- ✅ Blob storage encryption at rest
- ✅ No public access allowed
- ✅ Managed Identity provides minimal access
- ✅ Network firewall rules
- ✅ Private endpoints (recommended)

**Additional Hardening:**
```bash
# Enable storage encryption with customer-managed keys (CMK)
az storage account update \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --encryption-key-type-for-table "Account" \
  --encryption-key-type-for-queue "Account"
```

## Compliance & Standards

### GDPR Compliance

- ✅ User data stored in blob specific to user folder
- ✅ Audit logs for data access
- ✅ Data isolation per user
- [ ] **TODO:** Implement data deletion on request
- [ ] **TODO:** Implement data export functionality

### HIPAA Compliance (Healthcare)

- ✅ HTTPS encryption
- ✅ Audit logging
- [ ] **TODO:** Enable encryption with customer-managed keys
- [ ] **TODO:** Enable soft delete and immutable blobs
- [ ] **TODO:** Enable purge protection

### SOC 2 Compliance

- ✅ Authentication via Azure AD
- ✅ Authorization via RBAC
- ✅ Audit logging
- ✅ Monitoring and alerting
- [ ] **TODO:** Annual penetration testing
- [ ] **TODO:** Regular security assessment

## Securing Configuration

### 1. Secrets Management

#### Option A: Azure Key Vault (Recommended)

```bash
# Create Key Vault
az keyvault create \
  --name "doc-uploader-kv" \
  --resource-group "$RESOURCE_GROUP"

# Store secrets
az keyvault secret set \
  --vault-name "doc-uploader-kv" \
  --name "AzureAd--ClientSecret" \
  --value "$API_CLIENT_SECRET"

# Grant access to App Service
az keyvault set-policy \
  --name "doc-uploader-kv" \
  --object-id "$PRINCIPAL_ID" \
  --secret-permissions get list
```

#### Option B: GitHub Secrets

```yaml
# .github/secrets in GitHub Actions
AZURE_AD_CLIENT_SECRET: ${{ secrets.AZURE_AD_CLIENT_SECRET }}
STORAGE_ACCOUNT_KEY: ${{ secrets.STORAGE_ACCOUNT_KEY }}
```

### 2. Environment Variables

```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT="Production"
export AzureAd__TenantId="$TENANT_ID"
export AzureAd__ClientId="$API_CLIENT_ID"
export AzureAd__ClientSecret="$API_CLIENT_SECRET"  # From Key Vault
export AzureBlobStorage__AccountName="$STORAGE_ACCOUNT"
```

## Security Recommendations

### Immediate (Week 1)
- [ ] Enable Azure AD authentication
- [ ] Configure HTTPS
- [ ] Setup CORS for known origins
- [ ] Enable storage account firewall
- [ ] Setup audit logging

### Short-term (Month 1)
- [ ] Implement rate limiting
- [ ] Setup Application Insights monitoring
- [ ] Configure security alerts
- [ ] Enable WAF on API
- [ ] Implement malware scanning

### Medium-term (Quarter 1)
- [ ] Setup private endpoints for blob storage
- [ ] Implement customer-managed encryption keys
- [ ] Enable blob versioning
- [ ] Setup disaster recovery/backup
- [ ] Conduct security assessment

### Long-term (Year 1)
- [ ] Annual penetration testing
- [ ] Implement zero-trust architecture
- [ ] Setup advanced threat detection
- [ ] Compliance audit (SOC 2, GDPR)
- [ ] Security incident response plan

## Incident Response

### Suspected Token Compromise

```bash
# 1. Revoke existing deployments
az webapp restart \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP"

# 2. Rotate client secret
az ad app credential create \
  --id "$API_CLIENT_ID" \
  --display-name "New-Secret"

# 3. Review logs for unauthorized access
az monitor log-analytics query \
  -w "$WORKSPACE_NAME" \
  -q "requests | where resultCode == 401 or resultCode == 403"
```

### Suspected Blob Storage Compromise

```bash
# 1. Rotate storage account key
az storage account keys renew \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --key "primary"

# 2. Review access logs
az storage logging show \
  --account-name "$STORAGE_ACCOUNT"

# 3. Enable immutable blobs
az storage account blob-service-properties update \
  --account-name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --enable-immutability-policy true
```

## Security Testing

### OWASP Top 10 Verification

- [ ] **A01:2021 – Broken Access Control** - JWT validation enforced
- [ ] **A02:2021 – Cryptographic Failures** - HTTPS + TLS 1.2+
- [ ] **A03:2021 – Injection** - Path traversal protection implemented
- [ ] **A04:2021 – Insecure Design** - Design review completed
- [ ] **A05:2021 – Security Misconfiguration** - Configuration hardened
- [ ] **A06:2021 – Vulnerable Components** - Dependencies updated
- [ ] **A07:2021 – Identification & Auth Failures** - Azure AD integration
- [ ] **A08:2021 – Software/Data Integrity Failures** - Checksum validation
- [ ] **A09:2021 – Logging & Monitoring** - Structured logging enabled
- [ ] **A10:2021 – SSRF** - N/A (no external requests)

## References

- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/)
- [Azure Storage Security](https://learn.microsoft.com/en-us/azure/storage/common/storage-security-guide)
- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)
- [Azure Security Baseline](https://learn.microsoft.com/en-us/security/benchmark/azure/baselines/storage-security-baseline)
