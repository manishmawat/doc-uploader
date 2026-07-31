# Quick Start: Deploy UploaderDoc to Azure with GitHub Actions

This guide gets your app deployed in 5 simple steps.

## Prerequisites

- [ ] GitHub repository with this code pushed
- [ ] Azure account with an active subscription
- [ ] Azure CLI installed (`az --version`)
- [ ] GitHub CLI installed (`gh --version`)

## Step 1: Choose Your Deployment Path (2 minutes)

### Option A: Simple Frontend Deployment ⭐ RECOMMENDED
Best for: Just deploying the web app quickly
- Pros: Faster setup, no infrastructure code needed
- Cons: Limited to frontend, need to create SWA manually first
- Time: 5 minutes setup + 2 minutes first deploy

**Choose this if**: You want the quickest path to deployment

### Option B: Complete Infrastructure as Code
Best for: Full control over Azure resources
- Pros: Reproducible, infrastructure managed as code, add services easily
- Cons: More initial setup
- Time: 10 minutes setup + 3 minutes first deploy

**Choose this if**: You want professional-grade deployment management

## Option A Setup: Static Web Apps Direct

### Step 2A: Create Azure Static Web App (3 minutes)

1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource"
3. Search for "Static Web App"
4. Click Create and fill in:
   - **Resource Group**: Create new or use existing
   - **Name**: `uploaderapp` (or your choice)
   - **Hosting Plan**: Free (or Standard for production)
   - **Region**: East US (or your preferred region)
5. Click "Create"
6. Wait for deployment to complete

### Step 3A: Get Deployment Token (2 minutes)

1. Go to your Static Web App in Azure Portal
2. Click "Deployment token" in the left menu
3. Copy the full token (starts with `Bearer `)
4. Keep it safe - you'll need it next

### Step 4A: Add GitHub Secret (2 minutes)

1. Go to your GitHub repository
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click "New repository secret"
4. Name: `AZURE_STATIC_WEB_APPS_API_TOKEN`
5. Value: (paste the token from Step 3A)
6. Click "Add secret"

### Step 5A: Deploy! (2 minutes)

```bash
# From your project root
git add .
git commit -m "Add GitHub Actions deployment"
git push origin main
```

✅ **Done!** Your app is deploying. 

Go to your repository's **Actions** tab to watch it deploy. It takes about 3-5 minutes.

Once complete, click the workflow run to see your deployment URL!

---

## Option B Setup: Full Infrastructure with azd

### Step 2B: Run Setup Script (5 minutes)

Windows:
```bash
.\scripts\setup-github-deployment.bat
```

macOS/Linux:
```bash
chmod +x scripts/setup-github-deployment.sh
./scripts/setup-github-deployment.sh
```

The script will:
- Check your Azure setup
- Create resources if needed
- Set up GitHub Secrets automatically
- Save configuration

Just follow the prompts!

### Step 3B: Review Configuration (2 minutes)

Edit `.azure/.env`:
```
AZURE_ENV_NAME=uploaderapp
AZURE_LOCATION=eastus
AZURE_SUBSCRIPTION_ID=your-subscription-id
```

Or keep defaults for quick deployment.

### Step 4B: Deploy! (3 minutes)

```bash
# From your project root
git add .
git commit -m "Add infrastructure and GitHub Actions deployment"
git push origin main
```

✅ **Done!** GitHub Actions will:
1. Build your app
2. Create Azure resources (Static Web App, Storage, etc.)
3. Deploy everything

Monitor progress in the **Actions** tab. First deployment takes 5-10 minutes.

---

## What Happens During Deployment?

### GitHub Actions runs automatically:

1. **Build** (~2 minutes)
   - Restores NuGet packages
   - Compiles Blazor WASM app
   - Publishes to `publish/wwwroot/`

2. **Deploy** (~2 minutes for Option A, ~5 minutes for Option B)
   - Option A: Uploads files to Static Web App
   - Option B: Creates Azure resources + uploads files

3. **Complete** ✅
   - App is live at generated URL
   - Automatic HTTPS enabled
   - Ready to use!

## View Your Deployment

1. Go to GitHub repository
2. Click **Actions** tab
3. Click the latest workflow run
4. Click **Deploy to Azure Static Web Apps** or **Deploy Infrastructure and App with azd**
5. Scroll down to see deployment URL

Or go directly to Azure Portal → Static Web App → Overview to see the URL.

## Test Your App

Once deployment completes:

1. Visit your app URL (shown in workflow output)
2. Test the features:
   - Authentication (if configured)
   - File upload
   - PDF operations
3. Check browser console for any errors

## Make Changes

Every time you push to `main` branch:

```bash
git add .
git commit -m "Your change description"
git push origin main
```

GitHub Actions automatically:
- Rebuilds your app
- Runs tests (if you set them up)
- Deploys to Azure
- Updates your live site

No manual deployment needed! 🚀

## Common Issues & Solutions

### Deployment fails during build
- Check .NET 9.0 is used in UploaderDoc.csproj
- Review build output in Actions log
- Ensure no syntax errors in source code

### Can't access deployed app
- Wait 2 minutes after deployment completes
- Check Static Web App URL in Azure Portal
- Verify DNS propagation (for custom domains)

### Storage/Blob errors
- For Option B: Check Storage Account was created
- Verify connection string in appsettings.json
- Check if blob container "documents" exists

### Token/Authentication errors (Option A)
- Verify Static Web App deployment token in GitHub Secrets
- Token shouldn't have "Bearer " prefix
- Try regenerating token in Azure Portal

### Azure credentials errors (Option B)
- Verify Azure CLI is logged in: `az account show`
- Check GitHub Secrets have correct values
- Ensure app registration has Contributor role

## Next Steps

### After First Successful Deployment:

1. **Configure Custom Domain** (optional)
   - Add custom domain in Static Web App settings
   - HTTPS automatically enabled

2. **Set Up Monitoring** (optional)
   - Enable Application Insights for diagnostics
   - Track performance and errors

3. **Configure Backend APIs** (if needed)
   - Deploy Azure Functions for PDF processing APIs
   - Update appsettings.json with API endpoints

4. **Add Environments** (if needed)
   - Create staging deployment for testing
   - Set up separate production

## Documentation

For detailed information, see:
- `DEPLOYMENT_GUIDE.md` - Complete reference guide
- `README.md` - Application features and setup
- `.azure/deployment-plan.md` - Full deployment plan
- GitHub Actions workflows in `.github/workflows/`

## Getting Help

If deployment fails:

1. Check GitHub Actions output for error messages
2. Review `DEPLOYMENT_GUIDE.md` troubleshooting section
3. Check Azure Portal for resource creation errors
4. Verify Azure CLI permissions and subscription access

## Success Checklist ✅

- [ ] GitHub repository created and code pushed
- [ ] Azure account ready
- [ ] GitHub Secrets configured
- [ ] First commit pushed to main branch
- [ ] Deployment completed in Actions tab
- [ ] App accessible at deployment URL
- [ ] Features working (login, file upload, PDF operations)

## Questions?

See `DEPLOYMENT_GUIDE.md` for comprehensive setup instructions with all details.

---

**You're ready to deploy! 🚀**

Just push to main and watch it go live automatically!
