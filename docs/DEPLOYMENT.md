# Deployment

The frontend and backend deploy separately from GitHub Actions when changes land on `main`.

| Part | Target | Workflow |
|---|---|---|
| Frontend (`frontend/`) | Static Web App `doc-uploader-dev-swa` | `.github/workflows/deploy.yml` |
| Backend (`backend/`) | App Service `doc-uploader-dev-api` | `.github/workflows/deploy-backend.yml` |

The Azure resources themselves are described in [AZURE_SETUP.md](AZURE_SETUP.md).

## Workflows

| Workflow | Triggers | What it does |
|---|---|---|
| **Deploy to Azure Static Web Apps** (`deploy.yml`) | Push to `main`; pull requests to `main` | Builds and publishes `frontend/UploaderDoc.csproj` and uploads `publish/wwwroot` to the Static Web App. For a pull request it deploys a **preview environment** and comments the URL on the PR. |
| **Deploy Backend API** (`deploy-backend.yml`) | Push to `main` touching `backend/**`, `global.json` or the workflow file; manual (*Run workflow*) | Publishes `backend/DocUploaderApi.csproj`, signs in with OIDC, deploys to `doc-uploader-dev-api`, then polls `/health`. |
| **Deploy Infrastructure and App with azd** (`deploy-azd.yml`) | Push to `main`/`develop` touching `azure.yaml` or `infra/**`; manual | Runs `azd provision` + `azd deploy` for the Static Web App and storage. **Currently failing** at packaging: `azure.yaml` sets `dist: bin/Release/net10.0/publish/wwwroot`, but azd publishes to a temporary folder. It isn't needed for the current deployment. |
| **Azure Static Web Apps CI/CD** (`azure-static-web-apps-delightful-forest-0c569ca0f.yml`) | Disabled in GitHub | Legacy workflow for an older Static Web App. Keep it disabled or delete it. |

### Secrets

| Secret | Used by | Purpose |
|---|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | `deploy.yml` | Deployment token for `doc-uploader-dev-swa` (Portal → Static Web App → *Manage deployment token*) |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | `deploy-backend.yml`, `deploy-azd.yml` | OIDC sign-in as `github-uploaderapp-deployer` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DELIGHTFUL_FOREST_0C569CA0F` | Legacy workflow (disabled) | Not used |

The repository's default `GITHUB_TOKEN` is read-only, so `deploy.yml` grants itself `pull-requests: write` to post the preview comment.

The repository also has variables (`AZURE_AD_*`, `BLOB_STORAGE_SAS_API_ENDPOINT`) left over from the B2C setup. No workflow reads them.

## Production configuration

| Setting | Where | Value |
|---|---|---|
| API endpoint used by the deployed frontend | `frontend/wwwroot/appsettings.Production.json` | `https://doc-uploader-dev-api.azurewebsites.net/api/DocumentUpload/sas-token` |
| Entra ID client, authority, API scope | `frontend/wwwroot/appsettings.json` | Shared by all environments |
| SPA routing fallback | `frontend/wwwroot/staticwebapp.config.json` | Rewrites unknown routes to `index.html`. Don't add Static Web Apps `allowedRoles` rules: they use the platform's built-in sign-in, not MSAL, and cause a login loop. |
| Tenant, API client ID, audience | `backend/appsettings.json` → `AzureAd` | |
| Storage account and container | `backend/appsettings.json` → `AzureBlobStorage` | `docuploaderdevstorage` / `documents` (managed identity, no key) |
| Allowed browser origins | `backend/appsettings.json` → `Cors:AllowedOrigins` | The Static Web App origin |

A published Blazor WebAssembly app runs as the `Production` environment, so `appsettings.Production.json` overrides the local API endpoint in `appsettings.json`. If the deployed site still calls `localhost`, Chrome shows *"…wants to access other apps and services on this device"*, and declining it surfaces in the app as `TypeError: Failed to fetch`.

App Service settings override `appsettings.json` without a code change. Use `__` for nesting, for example:

```bash
az webapp config appsettings set -n doc-uploader-dev-api -g rg-uploaderapp-dev \
  --settings BlobUpload__SasTokenExpirationMinutes=15
```

## Pull request previews

Every PR gets a preview frontend at `https://delightful-ground-0b7a65d0f-<pr>.eastus2.7.azurestaticapps.net`. **Sign-in and uploads don't work there**, because the preview origin isn't a registered redirect URI and isn't allowed by API or storage CORS. Use previews for UI checks only, and test uploads on the main site after merging.

## Manual backend deployment

Use this when CI isn't available. It's how the API was first deployed.

```bash
dotnet publish backend/DocUploaderApi.csproj -c Release -o ./api-publish
cd api-publish && zip -r ../api.zip . && cd ..      # or: Compress-Archive -Path api-publish\* -DestinationPath api.zip
az webapp deploy -n doc-uploader-dev-api -g rg-uploaderapp-dev --src-path api.zip --type zip
```

To redeploy from CI without a code change, go to GitHub → Actions → *Deploy Backend API* → *Run workflow* on `main`.

## Verify a deployment

```bash
API=https://doc-uploader-dev-api.azurewebsites.net
curl $API/health                                   # → Healthy (200)
curl -o /dev/null -w "%{http_code}\n" -X POST \
  -H "Content-Type: application/json" -d '{"fileName":"a.png"}' \
  $API/api/DocumentUpload/sas-token                # → 401 without a token
```

Then sign in on the live site and upload a file. It should appear in `docuploaderdevstorage/documents/<your-email>/`.

Logs: `az webapp log tail -n doc-uploader-dev-api -g rg-uploaderapp-dev`.
