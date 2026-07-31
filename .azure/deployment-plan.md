# Azure Deployment Plan for UploaderDoc

**Status**: ✅ INFRASTRUCTURE PREPARED - READY FOR DEPLOYMENT  
**Created**: 2026-07-31  
**Plan Version**: 1.0  

---

### Application Type
- **Framework**: Blazor WebAssembly (.NET 9.0)
- **Architecture**: Single Page Application (SPA)
- **UI Components**: FluentUI (Microsoft)
- **Authentication**: Azure AD B2C
- **Features**: PDF processing, document upload, file conversion
- **Storage**: Azure Blob Storage
- **PWA**: Enabled (service worker support)

### Key Components
1. **Frontend**: Blazor WASM static content
2. **Services**:
   - Document Upload (Azure Blob Storage)
   - PDF Merge
   - File Conversion
   - PDF Optimization
3. **Authentication**: Azure AD B2C
4. **Configuration**: Static Web App config + appsettings.json

## Deployment Strategy

### Azure Services to Deploy
1. **Azure Static Web Apps** - Primary hosting for Blazor WASM frontend
2. **Azure Blob Storage** - Document storage
3. **Azure AD B2C** - Authentication (already configured)
4. **Optional Backend Function App** - For PDF processing APIs (currently running on localhost:7071)

### Deployment Method
- **Primary**: GitHub Actions CI/CD with automated builds and deployments
- **Tool**: Azure Developer CLI (azd) + Bicep Infrastructure as Code
- **Build**: `dotnet publish --configuration Release`
- **Output**: Static site files from `bin/Release/net9.0/publish/wwwroot/`
- **Trigger**: Push to `main` branch automatically triggers deployment

## Deployment Plan Phases

### Phase 1: Infrastructure Setup (COMPLETED)
- [x] Generate azure.yaml for azd
- [x] Create Bicep infrastructure templates
  - [x] main.bicep - Resource orchestration
  - [x] staticwebapp.bicep - Static Web App
  - [x] storage.bicep - Azure Storage
- [x] Create GitHub Actions workflows
  - [x] deploy.yml - Static Web Apps direct deployment
  - [x] deploy-azd.yml - Full infrastructure with azd

### Phase 2: GitHub Setup (REQUIRED - USER ACTION)
- [ ] Choose deployment option:
  - Option A: Static Web Apps Direct (simpler, frontend-only)
  - Option B: Full azd Infrastructure (full control)
- [ ] Create Azure Static Web App resource (if Option A)
- [ ] Create Azure App Registration with federated credentials (if Option B)
- [ ] Set up GitHub Secrets (AZURE_STATIC_WEB_APPS_API_TOKEN or Azure credentials)
- [ ] Configure repository permissions
- [ ] Enable GitHub Actions in repository

### Phase 3: Environment Configuration (REQUIRED - USER ACTION)
- [ ] Update `.azure/.env` with your Azure details
- [ ] Configure Azure AD B2C settings if needed
- [ ] Set up Storage Account connection details
- [ ] Update appsettings.json for production environment

### Phase 4: First Deployment (USER ACTION)
- [ ] Commit all changes to repository
- [ ] Push to main branch
- [ ] Monitor GitHub Actions workflow
- [ ] Verify deployment to Azure
- [ ] Test application at deployment URL

### Phase 5: Post-Deployment Configuration (OPTIONAL)
- [ ] Configure custom domain
- [ ] Enable monitoring with Application Insights
- [ ] Set up traffic analysis
- [ ] Configure CDN caching (if needed)
- [ ] Set up alerts and notifications

## GitHub Actions CI/CD Workflows

### Two Deployment Options Available:

#### Option A: Static Web Apps Direct Deployment (Simpler)
**File**: `.github/workflows/deploy.yml`
- Deploys frontend directly to Static Web Apps
- Minimal setup required
- Best for frontend-only applications
- Deployment token from Static Web App

**Steps**:
1. Create Static Web App in Azure Portal
2. Get deployment token
3. Add `AZURE_STATIC_WEB_APPS_API_TOKEN` GitHub Secret
4. Push to main → Automatic deployment

#### Option B: Full Infrastructure with azd (Complete)
**File**: `.github/workflows/deploy-azd.yml`
- Provisions all resources (Static Web App, Storage Account, etc.)
- Infrastructure as Code with Bicep
- Full RBAC and identity management
- Requires Azure App Registration with federated credentials

**Steps**:
1. Create Azure App Registration
2. Configure federated credentials for GitHub
3. Add GitHub Secrets (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID)
4. Push to main → Automatic infrastructure + deployment

## Outstanding Questions (RESOLVED)

### Deployment Configuration Decisions:
1. **Deployment Option**: 
   - [x] Option A: Quick Static Web Apps deployment
   - [x] Option B: Full infrastructure control with azd
   - User can choose based on needs

2. **Infrastructure Resources**:
   - [x] Static Web App ✓
   - [x] Azure Storage Account for documents ✓
   - [x] Optional: Azure Function backend (can be added later)

3. **GitHub Actions Setup**:
   - [x] Workflow for automatic builds ✓
   - [x] Workflow for infrastructure deployment ✓
   - [x] Setup scripts provided ✓

4. **Configuration Files**:
   - [x] azure.yaml ✓
   - [x] Bicep templates ✓
   - [x] Environment configuration template ✓
   - [x] Deployment guide ✓
   - [x] Setup script ✓

## Next Steps (ACTION REQUIRED)

### IMMEDIATE - Complete these steps to deploy:

1. **Choose Deployment Option** (Option A or B above)

2. **Set Up GitHub Secrets** (Run setup script or manual steps):
   ```bash
   # Option A: For Static Web Apps deployment
   # Add secret: AZURE_STATIC_WEB_APPS_API_TOKEN
   
   # Option B: For azd deployment with full infrastructure
   # Add secrets: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID
   ```

3. **Configure Environment** (Option B only):
   ```bash
   # Copy template
   cp .azure/.env.example .azure/.env
   
   # Edit with your values
   # AZURE_ENV_NAME, AZURE_LOCATION, AZURE_SUBSCRIPTION_ID
   ```

4. **Commit & Push**:
   ```bash
   git add .
   git commit -m "Add GitHub Actions deployment configuration"
   git push origin main
   ```

5. **Monitor Deployment**:
   - Go to GitHub repository → Actions tab
   - Watch workflow execution
   - Get deployment URL from workflow output

### Complete Setup Using Script (Recommended):

```bash
# Make script executable
chmod +x scripts/setup-github-deployment.sh

# Run setup wizard
./scripts/setup-github-deployment.sh
```

The script will:
- Validate Azure CLI and GitHub CLI
- Help you choose deployment option
- Set up Azure resources
- Configure GitHub Secrets automatically
- Verify permissions

### Manual Setup (Alternative):

See `DEPLOYMENT_GUIDE.md` for detailed step-by-step instructions for:
- Getting Static Web Apps deployment token
- Creating Azure App Registration
- Configuring federated credentials
- Setting up GitHub Secrets manually

## Support & Troubleshooting

For detailed information, see:
- `DEPLOYMENT_GUIDE.md` - Complete deployment guide
- `.github/workflows/deploy.yml` - Direct SWA deployment workflow
- `.github/workflows/deploy-azd.yml` - Full infrastructure deployment workflow
- `infra/main.bicep` - Infrastructure as Code definition
