using Azure.Storage.Sas;
using DocUploaderApi.Models;

namespace DocUploaderApi.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Generates a time-limited SAS token for a blob upload
    /// </summary>
    /// <param name="fileName">Name of the file to be uploaded</param>
    /// <param name="userIdentifier">User email or ID for organizing uploads</param>
    /// <param name="cancellationToken"></param>
    /// <returns>SAS token response with URI and expiration</returns>
    Task<SasTokenResponse> GenerateUploadSasTokenAsync(
        string fileName, 
        string userIdentifier,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates file before upload
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileSizeBytes">Size in bytes</param>
    /// <returns>Validation result with any error messages</returns>
    ValidationResult ValidateFile(string fileName, long? fileSizeBytes = null);
    
    /// <summary>
    /// Confirms receipt and metadata of an uploaded blob
    /// </summary>
    /// <param name="blobPath">Path in the container</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Upload confirmation response</returns>
    Task<UploadConfirmationResponse> ConfirmUploadAsync(
        string blobPath, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
