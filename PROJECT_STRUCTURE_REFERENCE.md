# Project Structure Reference

## Complete File Organization

```
doc-uploader/
│
├── 📄 azure.yaml                          ← Azure Developer CLI config (NEW)
│                                           Tells azd how to build and deploy
│
├── 📁 .azure/                             ← Azure deployment folder (NEW)
│   ├── deployment-plan.md                 ← Full deployment strategy (NEW)
│   └── .env.example                       ← Environment template (NEW)
│
├── 📁 .github/workflows/                  ← GitHub Actions (NEW)
│   ├── deploy.yml                         ← Static Web Apps workflow (NEW)
│   └── deploy-azd.yml                     ← Full infrastructure workflow (NEW)
│
├── 📁 infra/                              ← Infrastructure as Code (NEW)
│   ├── main.bicep                         ← Resource orchestration (NEW)
│   ├── staticwebapp.bicep                 ← Static Web App definition (NEW)
│   └── storage.bicep                      ← Storage Account definition (NEW)
│
├── 📁 scripts/                            ← Setup utilities (NEW)
│   ├── setup-github-deployment.sh         ← Linux/macOS setup (NEW)
│   └── setup-github-deployment.bat        ← Windows setup (NEW)
│
├── 📄 DEPLOYMENT_SETUP_SUMMARY.md         ← This overview (NEW)
├── 📄 DEPLOYMENT_GUIDE.md                 ← Complete guide (NEW)
├── 📄 QUICK_START.md                      ← Fast setup (NEW)
├── 📄 DEPLOYMENT_CHECKLIST.md             ← Tracking checklist (NEW)
│
├── 📄 README.md                           ← App documentation (EXISTING)
├── 📄 Program.cs                          ← Main entry point
├── 📄 App.razor                           ← Root component
├── 📄 _Imports.razor                      ← Global imports
│
├── 📁 Pages/                              ← Blazor pages
│   ├── Home.razor
│   ├── Authentication.razor
│   ├── Docimport.razor
│   ├── FileConverter.razor
│   ├── PdfMerge.razor
│   └── PdfOptimize.razor
│
├── 📁 Components/
│   ├── LoginDisplay.razor
│   └── Layout/
│       ├── MainLayout.razor
│       └── NavMenu.razor
│
├── 📁 Services/                           ← Business logic
│   ├── IDocumentUploadService.cs
│   ├── DocumentUploadService.cs
│   ├── IFileConverterService.cs
│   ├── FileConverterService.cs
│   ├── IPdfMergeService.cs
│   ├── PdfMergeService.cs
│   ├── IPdfOptimizerService.cs
│   ├── PdfOptimizerService.cs
│   ├── BlobStorageOptions.cs
│   └── ...
│
├── 📁 Authentication/
│   └── ExternalEntraOptions.cs
│
├── 📁 wwwroot/                            ← Static assets
│   ├── index.html                         ← Entry point
│   ├── appsettings.json                   ← Configuration
│   ├── staticwebapp.config.json           ← SWA routing
│   ├── service-worker.js
│   ├── css/
│   │   └── app.css
│   └── _framework/                        ← Blazor WASM runtime
│       └── (auto-generated at build)
│
├── 📁 Properties/
│   └── launchSettings.json
│
├── 📄 UploaderDoc.csproj                  ← Project file
├── 📄 UploaderDoc.sln                     ← Solution file
├── 📄 package.json                        ← NPM config
└── 📁 .git/                               ← Git repository
```

## What Each Deployment File Does

### GitHub Actions Workflows

#### `.github/workflows/deploy.yml` (Static Web Apps)
```
Trigger: Push to main branch
├─ Build: dotnet publish
├─ Output: bin/Release/net9.0/publish/wwwroot/
└─ Deploy: Upload to Static Web App
```

**When to use**: Quick, simple deployment
**Requires**: Static Web App + deployment token
**Time**: ~4 minutes total

#### `.github/workflows/deploy-azd.yml` (Full Infrastructure)
```
Trigger: Push to main branch or manual
├─ Install: Azure Developer CLI
├─ Azure Login: Federated credentials
├─ Provision: Create resources (SWA, Storage)
├─ Deploy: Upload app files
└─ Output: Deployment summary
```

**When to use**: Professional, reproducible deployments
**Requires**: Azure App Registration + GitHub Secrets
**Time**: ~8 minutes total (first time), ~4 minutes (subsequent)

### Infrastructure as Code (Bicep)

#### `infra/main.bicep` (Orchestration)
```bicep
Parameters:
  - location
  - environment (dev/prod)
  - appName

Creates:
  1. Static Web App
  2. Storage Account
  3. Tags all resources

Outputs:
  - URLs and credentials
  - Storage information
```

#### `infra/staticwebapp.bicep` (Frontend)
```bicep
Resource: Azure Static Web App
Configuration:
  - Standard tier for production
  - SPA routing fallback
  - HTTPS automatic
  - Service worker support

Outputs:
  - Default hostname URL
  - Deployment token
```

#### `infra/storage.bicep` (Data)
```bicep
Resource: Azure Storage Account
Configuration:
  - Hot tier for active access
  - LRS replication (local)
  - TLS 1.2 minimum
  - Private blob access

Containers:
  - "documents" for uploads

Outputs:
  - Connection details
```

### Configuration Files

#### `azure.yaml` (azd Configuration)
```yaml
name: UploaderDoc              # App name
services:
  web:                         # Service definition
    project: .                 # Project directory
    language: csharp           # Language
    host: staticwebapp         # Azure host type
    dist: bin/Release/...      # Build output
```

**Used by**: Azure Developer CLI (azd)
**Purpose**: Tells azd how to build and deploy your app
**Modified when**: Changing build process or adding services

#### `.azure/.env` (Environment Variables)
```
AZURE_ENV_NAME=uploaderapp    # Unique name for resources
AZURE_LOCATION=eastus         # Azure region
AZURE_SUBSCRIPTION_ID=xxx     # Your subscription ID
```

**Used by**: azd during provisioning
**Customized by**: User before first deployment
**Stored in**: `.azure/` directory (not in git)

### Setup Scripts

#### `scripts/setup-github-deployment.sh` (macOS/Linux)
```bash
Interactive setup wizard that:
1. Validates prerequisites (az, gh CLI)
2. Guides resource creation
3. Configures GitHub Secrets
4. Sets up Azure RBAC roles
5. Saves configuration
```

**Usage**: `chmod +x scripts/setup-github-deployment.sh && ./scripts/setup-github-deployment.sh`

#### `scripts/setup-github-deployment.bat` (Windows)
```batch
Same functionality as shell script
Interactive prompts for setup
Configures Azure and GitHub automatically
```

**Usage**: `.\scripts\setup-github-deployment.bat`

## Documentation Files

### New Documentation (Created for You)

| File | Purpose | Read Time | When to Read |
|------|---------|-----------|--------------|
| `QUICK_START.md` | Fast 5-minute setup | 5 min | First time |
| `DEPLOYMENT_GUIDE.md` | Complete reference | 15 min | Need details |
| `DEPLOYMENT_SETUP_SUMMARY.md` | Overview (this file) | 10 min | Understanding setup |
| `DEPLOYMENT_CHECKLIST.md` | Progress tracking | 3 min | During deployment |
| `.azure/deployment-plan.md` | Full architecture plan | 10 min | Understanding strategy |

### Existing Documentation

| File | Purpose |
|------|---------|
| `README.md` | Application features and local setup |
| `.github/workflows/*.yml` | CI/CD pipeline configuration |

## Deployment Architecture Diagram

```
┌─────────────────────────────────────────┐
│   Your Development Machine              │
│   └─ Your Code                          │
└────────────────┬────────────────────────┘
                 │ git push origin main
                 ▼
┌─────────────────────────────────────────┐
│   GitHub Repository                     │
│   ├─ Source Code                        │
│   ├─ Workflows (.github/workflows/)     │
│   └─ Secrets (AZURE_STATIC_WEB_APPS...) │
└────────────────┬────────────────────────┘
                 │ Webhook triggers
                 ▼
┌─────────────────────────────────────────┐
│   GitHub Actions Runner                 │
│   ├─ Checkout Code                      │
│   ├─ Setup .NET 9.0                     │
│   ├─ Run: dotnet restore                │
│   ├─ Run: dotnet build                  │
│   ├─ Run: dotnet publish                │
│   └─ Deploy output to Azure              │
└────────────────┬────────────────────────┘
                 │
        ┌────────┴────────┐
        │                 │
        ▼                 ▼
    ┌────────┐       ┌──────────┐
    │ Option │       │ Option B │
    │   A    │       │ (azd)    │
    └────┬───┘       └─────┬────┘
         │                 │
         ▼                 ▼
    ┌────────┐       ┌──────────────┐
    │Upload  │       │Create        │
    │to SWA  │       │Resources     │
    │(fast)  │       │+ Deploy      │
    └────┬───┘       └─────┬────────┘
         │                 │
         └────────┬────────┘
                  │
                  ▼
         ┌─────────────────┐
         │ Azure Static    │
         │ Web App         │
         │ (Live & HTTPS)  │
         └────────┬────────┘
                  │
                  ▼
         ┌─────────────────┐
         │ Your App Live!  │
         │ https://xxx.    │
         │ azurestaticapps │
         │ .net            │
         └─────────────────┘
```

## How to Use These Files

### First Time Setup
1. Read: `QUICK_START.md` (5 min)
2. Run: `scripts/setup-github-deployment.bat` or `.sh` (5 min)
3. Push: `git push origin main` (1 min)
4. Wait: GitHub Actions deploys (2-5 min)
5. Done! ✅

### Understanding the System
1. Read: `DEPLOYMENT_SETUP_SUMMARY.md` (this file)
2. Reference: `DEPLOYMENT_GUIDE.md` for details
3. Track: Use `DEPLOYMENT_CHECKLIST.md` during setup

### Making Changes After Deployment
1. Edit code locally
2. Test: `dotnet run`
3. Commit: `git commit -m "..."`
4. Push: `git push origin main`
5. GitHub Actions automatically deploys

## Key Concepts

### Static Web Apps (Azure SWA)
- Hosts Blazor WASM frontend
- Auto-configured for SPA routing
- Automatic HTTPS/TLS
- Global CDN included
- Cost: ~$9/month

### Azure Storage
- Stores uploaded documents
- Blob containers
- Integrated with your app
- Private access by default
- Cost: ~$0.50/month (minimal usage)

### GitHub Actions
- Automated CI/CD pipelines
- Triggers on git push
- Runs build and deploy
- No additional cost (included in GitHub)

### Azure Developer CLI (azd)
- Infrastructure as Code orchestration
- Bicep template deployment
- Environment management
- Resource provisioning automation

## Environment Variables & Secrets

### GitHub Secrets (Secure Storage)
- Stored in GitHub repo
- Never exposed in logs
- Used during GitHub Actions runs
- Examples:
  - `AZURE_STATIC_WEB_APPS_API_TOKEN` (Option A)
  - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (Option B)

### .env Files (Local Configuration)
- Stored in `.azure/` directory
- Not committed to git
- Used by azd locally
- Examples:
  - `AZURE_ENV_NAME`
  - `AZURE_LOCATION`
  - `AZURE_SUBSCRIPTION_ID`

### appsettings.json (App Configuration)
- Embedded in app
- Azure AD B2C settings
- Blob Storage endpoints
- API configuration

## Resource Naming Conventions

Generated by Bicep templates:

```
Static Web App: uploaderapp-dev-swa
Storage Account: uploaderappdestroage
Resource Group: uploaderapp-dev-rg
```

All tagged with:
- `environment`: dev/prod
- `project`: UploaderDoc
- `managed_by`: azd

## Deployment Cost Estimate

### Monthly Costs (Production)
```
Static Web App (Standard)        $9.00
Azure Storage (LRS)              $0.50
Compute (included)               $0.00
Bandwidth (first 50GB free)      $0.00
                                ─────
Estimated Total                  ~$10/month
```

### Usage Scenarios
- Personal project: $9/month
- Small team (10-50 users): $9-15/month
- Medium app (50-500 users): $15-25/month (with App Insights)

## Security Checklist

✅ **Enabled by Default**
- HTTPS/TLS encryption
- Blob storage private access
- Azure AD B2C integration
- Network isolation

✅ **Recommended to Configure**
- [ ] Custom domain (optional)
- [ ] Application Insights (optional)
- [ ] WAF rules (optional)
- [ ] Rate limiting (optional)

## Troubleshooting Quick Reference

| Problem | File to Check |
|---------|---------------|
| Build fails | `.github/workflows/deploy.yml` |
| Deployment fails | `.github/workflows/deploy-azd.yml` |
| Wrong Azure resources | `infra/main.bicep` |
| Environment variables wrong | `.azure/.env` |
| Routing issues | `wwwroot/staticwebapp.config.json` |
| App config wrong | `wwwroot/appsettings.json` |

**Full troubleshooting**: See `DEPLOYMENT_GUIDE.md`

---

**End of Structure Reference**

Ready to deploy? Start with `QUICK_START.md`! 🚀
