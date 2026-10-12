# GitHub Actions Deployment Setup Guide

This guide helps you set up automated deployment from GitHub Actions to Azure.

## Two Deployment Options

### Option 1: Static Web Apps Direct Deployment (Recommended for Frontend-Only)
**File**: `.github/workflows/deploy.yml`

This workflow builds the Blazor WASM app and deploys directly to Azure Static Web Apps using the deployment token.

**When to use**: 
- Frontend deployment only
- No Azure infrastructure management needed
- Faster, simpler deployment

**Setup Steps**:
1. Create an Azure Static Web App in the Azure Portal
2. Get the Static Web Apps API token
3. Add as GitHub Secret: `AZURE_STATIC_WEB_APPS_API_TOKEN`
4. Push to `main` branch to trigger deployment

### Option 2: Full Infrastructure Deployment with azd (Recommended for Full Stack)
**File**: `.github/workflows/deploy-azd.yml`

This workflow uses Azure Developer CLI to provision all infrastructure and deploy the app.

**When to use**:
- Full infrastructure as code
- Need to provision Storage Account, Static Web App, etc.
- Multi-environment deployments
- Reproducible infrastructure

**Setup Steps**:
1. Create an Azure App Registration with federated credentials
2. Grant necessary RBAC roles
3. Add GitHub Secrets (see below)
4. Push to trigger deployment

## GitHub Secrets Setup

### For Static Web Apps Deployment (Option 1)

1. **Get Static Web Apps Deployment Token**:
   ```bash
   # In Azure Portal: Go to Static Web App > Deployment token
   ```

2. **Add to GitHub**:
   - Repository Settings → Secrets and Variables → Actions
   - New repository secret:
     - Name: `AZURE_STATIC_WEB_APPS_API_TOKEN`
     - Value: (paste deployment token)

### For Azure Developer CLI Deployment (Option 2)

1. **Create Azure App Registration** (if needed):
   ```bash
   az ad app create --display-name "github-deployer" \
     --web-redirect-uris "https://github.com"
   ```

2. **Get required values**:
   - **Subscription ID**: `az account show --query id -o tsv`
   - **Tenant ID**: `az account show --query tenantId -o tsv`
   - **Client ID**: From App Registration

3. **Create federated credential**:
   ```bash
   az identity federated-credential create \
     --resource-group <rg-name> \
     --identity-name <identity-name> \
     --name github-deployer \
     --issuer https://token.actions.githubusercontent.com \
     --subject repo:<owner>/<repo>:ref:refs/heads/main
   ```

4. **Assign Azure RBAC roles**:
   ```bash
   az role assignment create \
     --assignee-object-id <client-id> \
     --role Contributor \
     --scope /subscriptions/<subscription-id>
   ```

5. **Add GitHub Secrets**:
   - `AZURE_SUBSCRIPTION_ID`: Your subscription ID
   - `AZURE_TENANT_ID`: Your tenant ID
   - `AZURE_CLIENT_ID`: Your app registration client ID

## Environment Variables

Create `.azure/.env` file with:
```env
AZURE_ENV_NAME=uploaderapp
AZURE_LOCATION=eastus
AZURE_SUBSCRIPTION_ID=<your-subscription-id>
```

## First-Time Deployment

### Using Option 1 (Static Web Apps):
```bash
# Push to main branch
git add .
git commit -m "Initial deployment setup"
git push origin main
```

### Using Option 2 (azd):
```bash
# Initialize azd environment
azd init --no-prompt

# Provision infrastructure
azd provision

# Deploy application
azd deploy

# View deployment outputs
azd env get-values
```

## Monitoring Deployments

1. Go to your repository → Actions tab
2. Select the workflow run
3. Check build and deployment logs
4. View deployment URL in workflow output

## Troubleshooting

### Deployment Token not found
- Ensure Static Web App exists in Azure
- Verify token is copied correctly (no extra spaces)

### azd deployment fails
- Check Azure credentials in GitHub Secrets
- Verify RBAC permissions
- Review Azure resource group creation

### Build failures
- Ensure .NET 9.0 is available
- Check dependencies in UploaderDoc.csproj
- Review build output in GitHub Actions logs

## Rollback

To rollback to a previous deployment:
1. Go to Azure Portal → Static Web App
2. View deployment history
3. Select previous version and restore (if available)

Or redeploy a previous commit:
```bash
git revert <commit-hash>
git push origin main
```

## Cost Optimization

- Static Web Apps Standard tier: ~$9/month
- Storage Account (LRS): ~$0.023/GB/month
- Consider using Azure Reservations for discounts

## Next Steps

1. Choose deployment option (1 or 2)
2. Set up GitHub Secrets
3. Push to main branch
4. Monitor workflow in Actions tab
5. Verify deployment at generated URL
