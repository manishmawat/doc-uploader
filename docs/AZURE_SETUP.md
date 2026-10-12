# Azure setup

This is everything the app needs in Microsoft Entra ID and Azure, as currently configured, with commands to recreate it.

- **Tenant:** `bc4e51bb-2207-4ec4-9d25-57c74d363241` (workforce Entra ID; access is managed by inviting guests)
- **Subscription:** `ef0f600e-d8aa-4ea1-8b75-424305e4519d`
- **Resource group:** `rg-uploaderapp-dev`

## Entra ID app registrations

| Registration | Application (client) ID | Purpose |
|---|---|---|
| **DocUploader API** | `3a742ac5-add7-414a-ba33-dbda1263109a` | The backend API. Exposes the scope `api://3a742ac5-add7-414a-ba33-dbda1263109a/SASKey.Write`. |
| **UploaderDoc-CIAM** | `2b207a38-776b-4cf1-b09b-5353d79b6289` | The Blazor SPA. Signs users in and requests tokens for the API. (The name is a leftover from the earlier B2C/CIAM setup; it's a normal workforce registration.) |

### 1. API registration (DocUploader API)

1. **App registrations → New registration.** Use *Accounts in this organizational directory only* and no redirect URI.
2. **Expose an API.**
   - Set the Application ID URI to `api://<api-client-id>`.
   - Add the scope `SASKey.Write`, with *Who can consent: Admins and users* and state *Enabled*.
3. **Token configuration → Add optional claim → Access → `email`.** The API names each user's folder after this claim. Without it, guest users get a folder named after their `#EXT#` UPN, such as `jane_gmail.com#EXT#@tenant…`.

The backend reads `AzureAd:TenantId`, `AzureAd:ClientId` (the API's client ID) and `AzureAd:Audience` (`api://<api-client-id>`) from `backend/appsettings.json`.

### 2. SPA registration (UploaderDoc-CIAM)

1. **Authentication → Add a platform → Single-page application.** It must be the SPA platform; with *Web* you get AADSTS9002326. Redirect URIs currently registered:
   - `https://delightful-ground-0b7a65d0f.7.azurestaticapps.net/authentication/login-callback`
   - `https://localhost:7229/authentication/login-callback`
   - `http://localhost:5168/authentication/login-callback`
   - `http://localhost:5000/authentication/login-callback`
   - `http://localhost:3000/authentication/login-callback`

   Leave the implicit grant checkboxes unchecked.
2. **API permissions → Add a permission → *APIs my organization uses*.**
   - Search for the API's client ID → *Delegated* → `SASKey.Write`.
   - Then **Grant admin consent**, because guests usually can't consent themselves.
   - The *My APIs* tab only lists APIs you own, so it may show nothing.
3. *(Optional)* **Token configuration → Add optional claim → ID → `email`.** The header shows the user's `name` claim and falls back to `preferred_username`, then `email`. Folder names come from the **API's access token** (section 1), not from this ID token.

The frontend reads `AzureAd:ClientId`, `AzureAd:Authority` and `BlobStorage:ApiScope` from `frontend/wwwroot/appsettings.json`.

### 3. Limit who can sign in

1. **Enterprise applications → UploaderDoc-CIAM → Properties → Assignment required = Yes.**
2. **Users and groups → Add** the users or a group.
3. Invite external users from **Users → Invite external user**, then assign them as in step 2.

## Azure resources (`rg-uploaderapp-dev`)

| Resource | Type / SKU | Region | Notes |
|---|---|---|---|
| `doc-uploader-dev-swa` | Static Web App, Standard | East US 2 | Hosts the frontend at `delightful-ground-0b7a65d0f.7.azurestaticapps.net` |
| `docuploaderdevstorage` | Storage account (StorageV2) | East US 2 | Container `documents` (private). Blob CORS allows `PUT/GET/OPTIONS` from the Static Web App origin. |
| `doc-uploader-dev-asp` | App Service plan, Linux **F1 (Free)** | Canada Central | The subscription has no F1 quota in East US 2, hence Canada Central. |
| `doc-uploader-dev-api` | Web App, `DOTNETCORE:10.0`, HTTPS only | Canada Central | System-assigned managed identity with **Storage Blob Data Contributor** on `docuploaderdevstorage` |

The API signs upload SAS tokens with a **user delegation key** obtained through its managed identity, so no storage account key is stored anywhere. *Storage Blob Data Contributor* includes the permission needed to request that key, and the list and delete permissions used by the *My Documents* endpoints (`/api/Documents`).

Free tier limits: 60 CPU-minutes per day, and the app sleeps when idle, so the first request after a break can take 10–20 seconds. For always-on hosting, scale the plan up: `az appservice plan update -n doc-uploader-dev-asp -g rg-uploaderapp-dev --sku B1`.

### Recreate the backend resources

These are the commands used to create the API hosting. In Git Bash, set `MSYS_NO_PATHCONV=1` first. Otherwise Git Bash rewrites `/subscriptions/...` scopes into Windows paths, and the role assignment fails with `MissingSubscription`.

```bash
export MSYS_NO_PATHCONV=1
RG=rg-uploaderapp-dev
SWA=https://delightful-ground-0b7a65d0f.7.azurestaticapps.net

az appservice plan create -n doc-uploader-dev-asp -g $RG -l canadacentral --sku F1 --is-linux

az webapp create -n doc-uploader-dev-api -g $RG -p doc-uploader-dev-asp \
  --runtime "DOTNETCORE:10.0" --assign-identity "[system]" --https-only true

PRINCIPAL=$(az webapp identity show -n doc-uploader-dev-api -g $RG --query principalId -o tsv)
STORAGE_ID=$(az storage account show -n docuploaderdevstorage -g $RG --query id -o tsv)
az role assignment create --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" --scope "$STORAGE_ID"

# Let the browser upload directly to blob storage from the Static Web App
az storage cors add --services b --methods GET PUT OPTIONS --origins "$SWA" \
  --allowed-headers "*" --exposed-headers "*" --max-age 3600 --account-name docuploaderdevstorage
```

Then deploy the code (see [DEPLOYMENT.md](DEPLOYMENT.md)).

`infra/` contains Bicep for the Static Web App and storage account, used by the `deploy-azd.yml` workflow. `backend/infra/main.bicep` is an older template for a different design (B2 plan, user-assigned identity, container registry, Key Vault). It is **not** what's deployed.

### GitHub Actions identity

CI signs in to Azure with OIDC, so no client secret is stored.

| | |
|---|---|
| App registration | `github-uploaderapp-deployer` |
| Federated credential subject | `repo:manishmawat/doc-uploader:ref:refs/heads/main` (only runs on `main` can sign in) |
| Role | Contributor on the subscription |
| GitHub secrets | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` |

## If the Static Web App URL changes

The origin appears in four places. Update all of them:

1. SPA registration redirect URI: `<new-origin>/authentication/login-callback`
2. `backend/appsettings.json` → `Cors:AllowedOrigins`
3. Storage CORS: `az storage cors add ... --origins <new-origin>` (and remove the old rule)
4. The README links
