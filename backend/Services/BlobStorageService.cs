using Azure.Storage.Blobs;
using BlobCopyFromUriOptions = Azure.Storage.Blobs.Models.BlobCopyFromUriOptions;
using BlobTraits = Azure.Storage.Blobs.Models.BlobTraits;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using DocUploaderApi.Models;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DocUploaderApi.Services;

/// <summary>
/// Service for managing blob storage operations and SAS token generation
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly IOptions<AzureBlobStorageOptions> _storageOptions;
    private readonly IOptions<BlobUploadOptions> _uploadOptions;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        BlobContainerClient containerClient,
        IOptions<AzureBlobStorageOptions> storageOptions,
        IOptions<BlobUploadOptions> uploadOptions,
        ILogger<BlobStorageService> logger)
    {
        _containerClient = containerClient;
        _storageOptions = storageOptions;
        _uploadOptions = uploadOptions;
        _logger = logger;
    }

    public async Task<SasTokenResponse> GenerateUploadSasTokenAsync(
        string fileName,
        string userIdentifier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            if (string.IsNullOrWhiteSpace(userIdentifier))
                throw new ArgumentException("User identifier cannot be empty", nameof(userIdentifier));

            var validation = ValidateFile(fileName);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage);

            // Sanitize inputs to prevent path traversal and special characters
            var sanitizedUserFolder = SanitizePath(userIdentifier);
            var sanitizedFileName = SanitizePath(Path.GetFileName(fileName));

            // Create blob path: user-folder/filename
            var blobPath = $"{sanitizedUserFolder}/{sanitizedFileName}";
            var blobClient = _containerClient.GetBlobClient(blobPath);

            // Generate SAS token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerClient.Name,
                BlobName = blobPath,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_uploadOptions.Value.SasTokenExpirationMinutes)
            };
            // Upload only needs Create (new blob) and Write (overwrite / block list commit)
            sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

            // Sign the SAS token
            var sasToken = await SignSasAsync(blobClient, sasBuilder, cancellationToken);

            _logger.LogInformation(
                "Generated SAS token for user {User} file {File}. Expires in {Minutes} minutes",
                userIdentifier, fileName, _uploadOptions.Value.SasTokenExpirationMinutes);

            return new SasTokenResponse
            {
                BlobUri = new Uri(blobClient.Uri.ToString().Split('?')[0]),
                Signature = sasToken.Query,
                ExpiresAt = sasBuilder.ExpiresOn.UtcDateTime,
                ContainerName = _containerClient.Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS token for {File}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Signs the SAS with the account key when available (Azurite / connection string),
    /// otherwise with a user delegation key (Managed Identity in Azure)
    /// </summary>
    private async Task<Uri> SignSasAsync(
        BlobClient blobClient,
        BlobSasBuilder sasBuilder,
        CancellationToken cancellationToken)
    {
        if (blobClient.CanGenerateSasUri)
            return blobClient.GenerateSasUri(sasBuilder);

        var serviceClient = _containerClient.GetParentBlobServiceClient();
        var delegationKey = await serviceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            sasBuilder.ExpiresOn,
            cancellationToken);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(delegationKey.Value, serviceClient.AccountName)
        };
        return uriBuilder.ToUri();
    }

    public ValidationResult ValidateFile(string fileName, long? fileSizeBytes = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new ValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "File name is required" 
                };

            // Check file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var allowedExtensions = _uploadOptions.Value.AllowedExtensions;

            if (!allowedExtensions.Contains(extension))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}"
                };
            }

            // Check file size if provided
            if (fileSizeBytes.HasValue && fileSizeBytes > _uploadOptions.Value.MaxFileSizeBytes)
            {
                var maxSizeMb = _uploadOptions.Value.MaxFileSizeBytes / (1024 * 1024);
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds maximum allowed size of {maxSizeMb} MB"
                };
            }

            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation error for file {File}", fileName);
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "An error occurred during file validation"
            };
        }
    }

    public async Task<UploadConfirmationResponse> ConfirmUploadAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Confirmed upload for blob {BlobPath}. Size: {Size} bytes", 
                blobPath, properties.Value.ContentLength);

            return new UploadConfirmationResponse
            {
                BlobUri = blobClient.Uri,
                FileName = Path.GetFileName(blobPath),
                FileSizeBytes = properties.Value.ContentLength,
                UploadedAt = properties.Value.CreatedOn.UtcDateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming upload for {BlobPath}", blobPath);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentItem>> ListUserDocumentsAsync(
        string userIdentifier,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{SanitizePath(userIdentifier)}/";
        var documents = new List<DocumentItem>();

        await foreach (var blob in _containerClient.GetBlobsAsync(
            prefix: prefix, cancellationToken: cancellationToken))
        {
            documents.Add(new DocumentItem
            {
                Name = blob.Name[prefix.Length..],
                SizeBytes = blob.Properties.ContentLength ?? 0,
                ContentType = blob.Properties.ContentType,
                LastModified = blob.Properties.LastModified
            });
        }

        return documents.OrderByDescending(d => d.LastModified).ToList();
    }

    public async Task<DownloadUrlResponse?> GenerateDownloadSasAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetUserBlobClient(userIdentifier, name);
        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(DownloadSasExpirationMinutes);
        var sasUri = await CreateReadSasUriAsync(
            blobClient, expiresOn, $"attachment; filename=\"{name}\"", cancellationToken);

        _logger.LogInformation("Generated download SAS for user {User} file {File}", userIdentifier, name);

        return new DownloadUrlResponse { Url = sasUri, ExpiresAt = expiresOn.UtcDateTime };
    }

    public async Task<DownloadUrlResponse?> CreateShareLinkAsync(
        string userIdentifier,
        string name,
        int expiresInHours,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetUserBlobClient(userIdentifier, name);
        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        // Inline so recipients can view PDFs and images in the browser
        var expiresOn = DateTimeOffset.UtcNow.AddHours(expiresInHours);
        var sasUri = await CreateReadSasUriAsync(
            blobClient, expiresOn, $"inline; filename=\"{name}\"", cancellationToken);

        _logger.LogInformation("Created share link for user {User} file {File}, expires {ExpiresOn}",
            userIdentifier, name, expiresOn);

        return new DownloadUrlResponse { Url = sasUri, ExpiresAt = expiresOn.UtcDateTime };
    }

    public async Task<bool> MoveToRecycleBinAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default)
    {
        var source = GetUserBlobClient(userIdentifier, name);
        if (!await source.ExistsAsync(cancellationToken))
            return false;

        // Replaces an older copy with the same name already in the bin
        var target = GetRecycleBinBlobClient(userIdentifier, name);
        var metadata = new Dictionary<string, string> { [DeletedOnMetadataKey] = DateTimeOffset.UtcNow.ToString("O") };
        await MoveBlobAsync(source, target, metadata, cancellationToken);

        _logger.LogInformation("Moved {BlobPath} to the recycle bin for user {User}", source.Name, userIdentifier);
        return true;
    }

    public async Task<IReadOnlyList<RecycleBinItem>> ListRecycleBinAsync(
        string userIdentifier,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{RecycleBinPrefix}{SanitizePath(userIdentifier)}/";
        var retention = TimeSpan.FromDays(_uploadOptions.Value.RecycleBinRetentionDays);
        var items = new List<RecycleBinItem>();

        await foreach (var blob in _containerClient.GetBlobsAsync(
            traits: BlobTraits.Metadata, prefix: prefix, cancellationToken: cancellationToken))
        {
            var deletedOn = blob.Metadata.TryGetValue(DeletedOnMetadataKey, out var value)
                && DateTimeOffset.TryParse(value, out var parsed)
                    ? parsed
                    : blob.Properties.LastModified;

            items.Add(new RecycleBinItem
            {
                Name = blob.Name[prefix.Length..],
                SizeBytes = blob.Properties.ContentLength ?? 0,
                DeletedOn = deletedOn,
                PurgeOn = deletedOn + retention
            });
        }

        return items.OrderByDescending(i => i.DeletedOn).ToList();
    }

    public async Task<RestoreResult> RestoreFromRecycleBinAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default)
    {
        var source = GetRecycleBinBlobClient(userIdentifier, name);
        if (!await source.ExistsAsync(cancellationToken))
            return RestoreResult.NotFound;

        var target = GetUserBlobClient(userIdentifier, name);
        if (await target.ExistsAsync(cancellationToken))
            return RestoreResult.NameConflict;

        await MoveBlobAsync(source, target, new Dictionary<string, string>(), cancellationToken);

        _logger.LogInformation("Restored {BlobPath} from the recycle bin for user {User}", target.Name, userIdentifier);
        return RestoreResult.Restored;
    }

    public async Task<bool> DeleteFromRecycleBinAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetRecycleBinBlobClient(userIdentifier, name);
        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        if (response.Value)
            _logger.LogInformation("Permanently deleted {BlobPath} for user {User}", blobClient.Name, userIdentifier);

        return response.Value;
    }

    /// <summary>
    /// Read-only download links are short-lived; the browser follows them immediately
    /// </summary>
    private const int DownloadSasExpirationMinutes = 5;

    /// <summary>
    /// Deleted documents live under .recycle-bin/&lt;user&gt;/ in the same container. User folders are
    /// emails or IDs, so they can't collide with it, and listing &lt;user&gt;/ never includes it.
    /// An Azure lifecycle rule purges this prefix after BlobUpload:RecycleBinRetentionDays.
    /// </summary>
    private const string RecycleBinPrefix = ".recycle-bin/";

    private const string DeletedOnMetadataKey = "deletedOn";

    private async Task<Uri> CreateReadSasUriAsync(
        BlobClient blobClient,
        DateTimeOffset expiresOn,
        string? contentDisposition,
        CancellationToken cancellationToken)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobClient.Name,
            ExpiresOn = expiresOn,
            ContentDisposition = contentDisposition
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return await SignSasAsync(blobClient, sasBuilder, cancellationToken);
    }

    /// <summary>
    /// Server-side copy, then delete. The copy source is a short-lived read SAS, which works
    /// with both an account key (Azurite) and a managed identity (Azure).
    /// </summary>
    private async Task MoveBlobAsync(
        BlobClient source,
        BlobClient target,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var sourceUri = await CreateReadSasUriAsync(
            source, DateTimeOffset.UtcNow.AddMinutes(DownloadSasExpirationMinutes), null, cancellationToken);

        var copy = await target.StartCopyFromUriAsync(
            sourceUri, new BlobCopyFromUriOptions { Metadata = metadata }, cancellationToken);
        await copy.WaitForCompletionAsync(cancellationToken);

        await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobClient GetRecycleBinBlobClient(string userIdentifier, string name) =>
        _containerClient.GetBlobClient($"{RecycleBinPrefix}{GetUserBlobClient(userIdentifier, name).Name}");

    /// <summary>
    /// Resolves a file name inside the user's own folder. Upload stores sanitized names,
    /// so anything that changes under sanitization (slashes, "..") can't be a user's file.
    /// </summary>
    private BlobClient GetUserBlobClient(string userIdentifier, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != SanitizePath(name))
            throw new ArgumentException("Invalid document name", nameof(name));

        return _containerClient.GetBlobClient($"{SanitizePath(userIdentifier)}/{name}");
    }

    /// <summary>
    /// Sanitizes path segments to prevent directory traversal and special characters
    /// </summary>
    private static string SanitizePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        // Replace path separators and traversal attempts
        var sanitized = input
            .Replace("\\", "-")
            .Replace("/", "-")
            .Replace("..", "")
            .Trim();

        // Remove special characters but keep dots and dashes
        sanitized = Regex.Replace(sanitized, @"[^\w\s\.\-@]", "");

        // Ensure it's not empty after sanitization
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
