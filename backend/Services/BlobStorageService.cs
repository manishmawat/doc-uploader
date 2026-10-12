using Azure.Storage.Blobs;
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
