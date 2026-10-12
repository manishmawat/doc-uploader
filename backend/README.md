# Document Uploader API

ASP.NET Core Web API (.NET 10) that issues short-lived, per-file SAS tokens so the Blazor frontend can upload directly to Azure Blob Storage. The API never receives file contents.

Run it locally with `dotnet run` (http://localhost:7071, Swagger at `/swagger`). See [docs/LOCAL_DEVELOPMENT.md](../docs/LOCAL_DEVELOPMENT.md).

## Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/DocumentUpload/sas-token` | `SASKey.Write` | Body: `{ "fileName": "report.pdf", "fileSizeBytes": 12345, "mimeType": "application/pdf" }` (size and MIME type optional). Validates the file, then returns `{ blobUri, signature, expiresAt, expiresInSeconds, containerName }`. The client uploads with `PUT {blobUri}?{signature}`. |
| `POST` | `/api/DocumentUpload/confirm-upload?blobPath=<path>` | `SASKey.Write` | Returns the size and creation time of an uploaded blob. The frontend doesn't currently call it. |
| `GET` | `/api/DocumentUpload/user-info` | `SASKey.Write` | Returns the caller's identifier and all token claims. Useful for debugging sign-in. |
| `GET` | `/api/DocumentUpload/health` | Anonymous | `{ status, timestamp }` |
| `GET` | `/health` | Anonymous | ASP.NET Core health check (`Healthy`) |

Error responses use `{ code, message, requestId }`. Codes include `INVALID_FILE_NAME`, `FILE_VALIDATION_FAILED`, `USER_NOT_IDENTIFIED` and `INTERNAL_ERROR`.

`doc-uploader-api.postman_collection.json` has sample requests.

## Authorization

- **Authentication:** JWT bearer tokens from Entra ID, validated by Microsoft.Identity.Web using the `AzureAd` section (`TenantId`, `ClientId`, `Audience`). Both v1 and v2 tokens are accepted.
- **Authorization:** every controller action except the health checks requires the policy `SasKeyWrite`, which requires the delegated scope **`SASKey.Write`**. A valid token without that scope gets **403**; a missing or invalid token gets **401**.
- **Rejection details:** CORS exposes `WWW-Authenticate`, so the frontend can show why a token was rejected. In Development, token validation errors include the full issuer and audience values (`ShowPII`).

## Where files go

- **Path:** `documents/<user>/<fileName>`.
- **`<user>`** is the first claim found among `email`, the mapped email claim, `preferred_username`, `emails`, and the name identifier. Configure the `email` optional claim on the API's access token (see [AZURE_SETUP.md](../docs/AZURE_SETUP.md)); otherwise guests get their `#EXT#` UPN.
- **Sanitizing:** both parts are cleaned to prevent path traversal. Slashes become `-`, `..` is removed, and only letters, digits, whitespace, `.`, `-`, `_` and `@` are kept.
- **Overwrites:** uploading a file with the same name overwrites the earlier one.

## SAS tokens

- Scoped to **one blob** with **Create + Write** permissions only (no read, list or delete).
- They expire after `BlobUpload:SasTokenExpirationMinutes`: 30 minutes by default, 60 in Development.
- How they're signed:
  - **Azure:** a **user delegation key** obtained with the App Service's managed identity (`DefaultAzureCredential`). No account key is used. The identity needs *Storage Blob Data Contributor*.
  - **Local (Azurite):** the account key from `AzureBlobStorage:ConnectionString`.

## Configuration

| Key | Default / example | Notes |
|---|---|---|
| `AzureAd:Instance` | `https://login.microsoftonline.com/` | |
| `AzureAd:TenantId` | `bc4e51bb-…` | |
| `AzureAd:ClientId` | `3a742ac5-…` | The **API** registration's client ID |
| `AzureAd:Audience` | `api://3a742ac5-…` | Application ID URI |
| `AzureBlobStorage:AccountName` | `docuploaderdevstorage` | Used with the managed identity outside Development |
| `AzureBlobStorage:ContainerName` | `documents` | |
| `AzureBlobStorage:ConnectionString` | Azurite connection string | **Development only.** When set, the API uses Azurite, and on startup creates the container and sets Azurite CORS. |
| `BlobUpload:MaxFileSizeBytes` | `104857600` (100 MB) | Checked when `fileSizeBytes` is sent |
| `BlobUpload:AllowedExtensions` | `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .csv .jpg .jpeg .png .gif .zip` | |
| `BlobUpload:SasTokenExpirationMinutes` | `30` | |
| `Cors:AllowedOrigins` | The Static Web App origin (prod); localhost ports (dev) | Also applied to Azurite in Development |

In App Service, override any key with an app setting that uses `__` for nesting, for example `BlobUpload__MaxFileSizeBytes`.

## Docker

```bash
docker compose up --build    # API on http://localhost:7071 + Azurite
```

The `Dockerfile` `HEALTHCHECK` runs `dotnet run`, which isn't available in the runtime image, so containers report *unhealthy*. Use `/health` instead.

## Known limitations

- `confirm-upload` doesn't check that `blobPath` is inside the caller's folder, so any signed-in user can read another blob's size and creation time.
- The upload page only offers images (`Accept="image/*"`), although the API accepts the wider extension list above.
- `infra/main.bicep` in this folder is an older template for a different hosting design and isn't used. The live resources are documented in [AZURE_SETUP.md](../docs/AZURE_SETUP.md).
