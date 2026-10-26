# UploaderDoc

A Blazor WebAssembly app for uploading documents to Azure Blob Storage and working with PDFs in the browser, backed by a small ASP.NET Core API that issues per-user upload tokens. Sign-in uses Microsoft Entra ID (Azure AD), and access is limited to invited users.

**Live site:** https://delightful-ground-0b7a65d0f.7.azurestaticapps.net
**API:** https://doc-uploader-dev-api.azurewebsites.net (`/health` is public; everything else needs a token)

## Features

| Page | What it does | Sign-in |
|---|---|---|
| **Upload Documents** (`/docimport`) | Two-step upload. Select up to 4 files (10 MB each) and review the list, then confirm. Each file shows its status, and failed uploads can be retried. Files are stored under a folder named after the signed-in user's email. | Required |
| **My Documents** (`/mydocuments`) | List and download your own uploaded files, **share** a file with a time-limited link, and **delete** to a **recycle bin** (restore or delete forever; purged after 14 days). This tab is a separate **Angular** app (`ng-app/`) embedded in an iframe; see [Embedded Angular app](#embedded-angular-app). | Required |
| **PDF Merge** (`/pdfmerge`) | Combine up to 10 PDFs (50 MB each) into one, in the order selected. | No |
| **File to PDF** (`/fileconvert`) | Convert up to 20 images (JPEG, PNG, BMP, TIFF, WEBP) into A4 PDFs. | No |
| **PDF Optimizer** (`/pdfoptimize`) | Reduce PDF file size. | No |
| **Games** (`/games`) | 2048, Minesweeper, Memory match and Sudoku (generated puzzles with one solution, three difficulties, notes, hints and undo). They run entirely in the browser, and best scores are saved on the device. | No |

PDF merge, conversion and optimization run entirely in the browser (iText7 on WebAssembly). No file leaves the user's machine for these tools.

**Works offline.** The app is a PWA: after one online visit, a service worker (`frontend/wwwroot/service-worker.published.js`) caches the published app, so it opens with no connection and can be installed. Games and the PDF tools work offline. Sign-in, upload and My Documents need a connection, and `/ng/` (the Angular app) is never cached. After a deploy, users see a *"A new version is available. Reload"* banner.

## How upload works

```
 Browser (Blazor WASM)              Backend API (App Service)         Azure Blob Storage
 ─────────────────────              ─────────────────────────         ──────────────────
 1. Sign in with Entra ID (MSAL)
 2. User selects files, then clicks Upload
 3. POST /api/DocumentUpload/sas-token ──▶ validates token + SASKey.Write scope
    { fileName, fileSizeBytes }            checks extension and size
                                           builds path <email>/<fileName>
                                    ◀───── returns a SAS for that one blob
                                           (Create + Write, expires in 30 min)
 4. PUT file directly to blob storage with the SAS ─────────────────────▶ documents/<email>/<fileName>
```

The API never handles file contents. It only decides *where* a user may write and signs a short-lived URL for exactly that blob, so a user can't write into another user's folder.

## Embedded Angular app

The *My Documents* tab is an Angular app that is built into `/ng/` on the same Static Web App and shown in an iframe by `frontend/Pages/MyDocuments.razor`, so the Blazor nav and header stay visible.

Entra ID's sign-in page can't load inside an iframe, so the Angular app doesn't sign in itself. It asks the Blazor host for a token instead:

```
 Angular (iframe, /ng/)                 Blazor host (/mydocuments)              Backend API
 postMessage {type:'token-request'} ──▶ IAccessTokenProvider (SASKey.Write)
                                   ◀── postMessage {type:'token', accessToken, expiresOn}
 GET/DELETE /api/Documents… with Bearer token ─────────────────────────────▶ scoped to <email>/
```

Both sides only accept messages from the expected window and origin. Downloads use a 5-minute read-only SAS URL from `GET /api/Documents/{name}/download-url`.

**Share links** (`POST /api/Documents/{name}/share`) are read-only SAS URLs that expire after 1 hour, 1 day or 3 days. Anyone with the link can open the file without signing in. A SAS can't be revoked on its own, but moving the file to the recycle bin changes its path, so its share links stop working.

**Recycle bin.** `DELETE /api/Documents/{name}` moves the file to `.recycle-bin/<email>/<name>` in the same container, using a server-side copy and then a delete. `GET /api/Documents/recycle-bin` lists the bin, `POST …/recycle-bin/{name}/restore` moves a file back (409 if the name is taken), and `DELETE …/recycle-bin/{name}` deletes it permanently. The bin is app-level rather than Azure soft delete, because Azurite doesn't support soft delete, so it works the same locally. In Azure a lifecycle rule purges it after 14 days (see [AZURE_SETUP.md](docs/AZURE_SETUP.md#recycle-bin-purge-rule)).

## Repository layout

```
frontend/                 Blazor WebAssembly app (UploaderDoc.csproj)
  Pages/                  Docimport, PdfMerge, FileConverter, PdfOptimize, Games, MyDocuments, Authentication
  Games/                  Game rules (2048, Minesweeper, Memory match, Sudoku) as plain C#, unit-tested
  Components/Games/       Game UI components
  Services/               DocumentUploadService (calls the API) and the PDF services
  wwwroot/                appsettings.json (local), appsettings.Production.json, staticwebapp.config.json
tests/UploaderDoc.Tests/  xUnit tests for the game rules (run in CI before deploy)
ng-app/                   Angular app for the My Documents tab, deployed to /ng/
apphost/                  Aspire AppHost: starts API + Blazor + Angular locally with a dashboard (dev only)
package.json              Root dev scripts: `npm run dev` starts all three apps (see docs/LOCAL_DEVELOPMENT.md)
backend/                  ASP.NET Core Web API (DocUploaderApi.csproj)
  Controllers/            DocumentUploadController, DocumentsController (list/download/delete)
  Services/               BlobStorageService (SAS generation, validation)
  docker-compose.yml      Optional: API + Azurite in containers
infra/                    Bicep for the Static Web App and storage account (used by deploy-azd.yml)
.github/workflows/        CI/CD (see docs/DEPLOYMENT.md)
docs/                     Setup and deployment guides
DocumentUploader.slnx     Solution containing both projects
global.json               Pins the .NET 10 SDK
```

## Tech stack

- **.NET 10** (SDK pinned in `global.json`)
- **Frontend:** Blazor WebAssembly, Microsoft FluentUI Blazor 4.13, MSAL (`Microsoft.Authentication.WebAssembly.Msal`), Azure.Storage.Blobs, iText7
- **Local orchestration:** `npm run dev` (concurrently) or Aspire 13 AppHost; not used in deployment
- **Embedded app:** Angular 22 (standalone, zoneless, signals)
- **Backend:** ASP.NET Core Web API, Microsoft.Identity.Web 4.16, Azure.Storage.Blobs, Azure.Identity, Serilog, Swagger (Development only)
- **Azure:** Static Web Apps (frontend), App Service on Linux (API), Blob Storage, Entra ID
- **Local storage emulator:** Azurite

## Quick start (local)

Prerequisites: .NET 10 SDK, Azurite (VS Code extension or Docker), and an account that can sign in to the Entra tenant.

```bash
# 1. Start Azurite (VS Code: Command Palette → "Azurite: Start Blob Service")

# 2. Backend: http://localhost:7071 (Swagger at /swagger)
cd backend
dotnet run

# 3. Frontend: http://localhost:5168 (or https://localhost:7229 with the https profile)
cd frontend
dotnet run
```

On startup, the backend creates the `documents` container in Azurite and configures CORS for the frontend. See [docs/LOCAL_DEVELOPMENT.md](docs/LOCAL_DEVELOPMENT.md) for configuration details and troubleshooting.

## Documentation

| Guide | Contents |
|---|---|
| [docs/LOCAL_DEVELOPMENT.md](docs/LOCAL_DEVELOPMENT.md) | Running both apps with Azurite, configuration files, troubleshooting |
| [docs/AZURE_SETUP.md](docs/AZURE_SETUP.md) | Entra ID app registrations and the Azure resources, with commands to recreate them |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | GitHub Actions workflows, secrets, manual deployment, production configuration |
| [backend/README.md](backend/README.md) | API endpoints, authorization rules, SAS details, backend configuration |
