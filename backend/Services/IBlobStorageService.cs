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

    /// <summary>
    /// Lists the documents in the user's folder
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Documents, newest first</returns>
    Task<IReadOnlyList<DocumentItem>> ListUserDocumentsAsync(
        string userIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a short-lived read-only SAS for one of the user's documents
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Download URL, or null when the document doesn't exist</returns>
    Task<DownloadUrlResponse?> GenerateDownloadSasAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one of the user's documents
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if the document existed and was deleted</returns>
    Task<bool> DeleteUserDocumentAsync(
        string userIdentifier,
        string name,
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
