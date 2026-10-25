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
    /// Creates a read-only link anyone can open until it expires. It can't be revoked,
    /// but moving the document to the recycle bin makes it stop working.
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="expiresInHours">Link lifetime in hours</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Share link, or null when the document doesn't exist</returns>
    Task<DownloadUrlResponse?> CreateShareLinkAsync(
        string userIdentifier,
        string name,
        int expiresInHours,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves one of the user's documents to their recycle bin
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if the document existed and was moved</returns>
    Task<bool> MoveToRecycleBinAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the user's recycle bin, most recently deleted first
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="cancellationToken"></param>
    Task<IReadOnlyList<RecycleBinItem>> ListRecycleBinAsync(
        string userIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a document from the recycle bin back to the user's folder
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>NameConflict when a document with that name already exists</returns>
    Task<RestoreResult> RestoreFromRecycleBinAsync(
        string userIdentifier,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a document from the recycle bin
    /// </summary>
    /// <param name="userIdentifier">User email or ID that owns the folder</param>
    /// <param name="name">File name relative to the user's folder</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if the document was in the recycle bin and was deleted</returns>
    Task<bool> DeleteFromRecycleBinAsync(
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
