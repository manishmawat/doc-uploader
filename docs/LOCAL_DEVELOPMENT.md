# Local development

Run the frontend and backend on your machine, with Azurite emulating Azure Blob Storage. Sign-in still goes through the real Entra ID tenant.

## Prerequisites

- **Node.js LTS** and npm, for the Angular app in `ng-app/` (only needed for the *My Documents* tab).
- **.NET 10 SDK.** `global.json` pins 10.0.x (`rollForward: latestFeature`).
- **Azurite**, from either:
  - the VS Code extension *Azurite* (`azurite.azurite`), or
  - Docker: `docker compose up azurite` in `backend/`
- **An account in tenant `bc4e51bb-2207-4ec4-9d25-57c74d363241`**, either a member or an invited guest assigned to the app (see [AZURE_SETUP.md](AZURE_SETUP.md)).

## Run

1. **Start Azurite.**
   - VS Code: Command Palette → *Azurite: Start Blob Service*. It listens on `127.0.0.1:10000` and writes its data files (`__azurite_db_*.json`, `__blobstorage__/`) to the workspace root. Both are git-ignored.
   - Docker: `cd backend && docker compose up azurite`.

2. **Start the backend.**
   ```bash
   cd backend
   dotnet run          # http profile → http://localhost:7071
   ```
   In Development, startup creates the `documents` container and sets Azurite's CORS rules from `Cors:AllowedOrigins`. Look for this line:
   ```
   Azurite ready: container documents, CORS origins ["https://localhost:7229", "http://localhost:5168", ...]
   ```
   If you see `Could not prepare Azurite` instead, Azurite isn't running. Start it, then restart the backend.

   Swagger UI is available at http://localhost:7071/swagger.

3. **Start the frontend.**
   ```bash
   cd frontend
   dotnet run                         # http://localhost:5168
   dotnet run --launch-profile https  # https://localhost:7229
   ```

4. **Upload a file.** Open the frontend, sign in, go to *Upload Documents*, select files, then click **Upload**.

   To check the result, use Azure Storage Explorer (*Emulator* → `documents`), or list the blobs with:
   ```bash
   az storage blob list -c documents --connection-string "UseDevelopmentStorage=true" --query "[].name" -o tsv
   ```

5. **(Optional) Start the Angular app for *My Documents*.**
   ```bash
   cd ng-app
   npm install         # first time only
   npm start           # http://localhost:4200
   ```
   Or run the VS Code task **ng-serve**. Then open *My Documents* in the Blazor app. Locally, `AngularApp:Url` in `frontend/wwwroot/appsettings.json` points the iframe at `http://localhost:4200/`, and the Angular dev build calls the API at `http://localhost:7071` and accepts tokens from the Blazor dev origins (`ng-app/src/environments/environment.development.ts`).

   Opening http://localhost:4200 directly only shows "Open this page from the Document Uploader app". That's expected, because tokens come from the Blazor host.

   Tests: `npx ng test --watch=false`.

### Alternative: API and Azurite in Docker

```bash
cd backend
docker compose up --build    # API on http://localhost:7071, Azurite on 10000-10002
```

This uses the same port as `dotnet run`, so run one or the other, not both.

## Configuration

### Frontend (`frontend/wwwroot/`)

| File | Used when | Key settings |
|---|---|---|
| `appsettings.json` | Always (base) | `AzureAd:ClientId` (SPA app registration), `AzureAd:Authority`, `BlobStorage:SasApiEndpoint` (local API), `BlobStorage:ApiScope` |
| `appsettings.Production.json` | Published build (Static Web App) | `BlobStorage:SasApiEndpoint` → the deployed API |
| `appsettings.Development.json` | `dotnet run` (optional, git-ignored) | Personal overrides |

Current values:

| Setting | Value |
|---|---|
| `AzureAd:ClientId` | `2b207a38-776b-4cf1-b09b-5353d79b6289` (app registration *UploaderDoc-CIAM*) |
| `AzureAd:Authority` | `https://login.microsoftonline.com/bc4e51bb-2207-4ec4-9d25-57c74d363241` |
| `BlobStorage:ApiScope` | `api://3a742ac5-add7-414a-ba33-dbda1263109a/SASKey.Write` |
| `BlobStorage:SasApiEndpoint` (local) | `http://localhost:7071/api/DocumentUpload/sas-token` |

These are public identifiers, not secrets. A browser app can't keep secrets, and none are needed.

### Backend (`backend/`)

| File | Used when | Notes |
|---|---|---|
| `appsettings.json` | Always (base, production values) | Storage account `docuploaderdevstorage`, CORS for the Static Web App |
| `appsettings.Development.json` | `ASPNETCORE_ENVIRONMENT=Development` (the launch profile sets this) | Azurite connection string, local CORS origins |
| `Properties/launchSettings.json` | `dotnet run` | Port 7071, Development environment |

The Azurite connection string uses the **standard, publicly documented emulator key**, which ends `...XJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==`. It isn't a secret, but it has to match exactly.

To allow the frontend on another port, add its origin to `Cors:AllowedOrigins` in `appsettings.Development.json`, then restart the backend. That updates both the API's CORS and Azurite's CORS.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Upload doesn't appear in Azurite; the browser console shows **403 AuthorizationFailure** from `127.0.0.1:10000` | The Azurite account key in `backend/appsettings.Development.json` is wrong. Use the standard emulator key (see above). |
| Browser console shows **CORS** errors for `127.0.0.1:10000` | Azurite has no CORS rules. Start Azurite *before* the backend, or restart the backend so it applies them. |
| Upload fails with **"Unauthorized access … API says: Bearer error=…"** | The API rejected the access token. The text after `API says:` names the failed check, and the backend console has the detailed `IDX…` message (full values are shown in Development). Common causes: wrong `Audience`/`ClientId` in backend settings, or the user hasn't consented to `SASKey.Write` (sign out and back in). |
| Upload fails with **403 Forbidden** from the API | The token is valid but lacks the `SASKey.Write` scope. Check the API permission and admin consent on the SPA registration. |
| Upload fails with **"File type '.xyz' is not allowed"** | The extension isn't in `BlobUpload:AllowedExtensions`. The upload page also only offers images (`Accept="image/*"`). |
| `dotnet build` fails with **"file is locked by: DocUploaderApi"** | The backend is still running. Stop it, then build. |
| *My Documents* says **"The host app did not provide an access token"** | The Blazor origin isn't in `hostOrigins` in `ng-app/src/environments/environment.development.ts`, or `AngularApp:Url` doesn't match where `ng serve` runs. |
| Sign-in fails with **AADSTS50011** | The redirect URI isn't registered. Add `<origin>/authentication/login-callback` to the SPA registration. |
| Sign-in fails with **AADSTS50105** | The user isn't assigned to the enterprise application. Assign the user, or turn off *Assignment required*. |
| Sign-in fails with **AADSTS65001** | Admin consent for `SASKey.Write` is missing. |
