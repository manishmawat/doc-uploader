namespace DocUploaderApi.Models;

/// <summary>
/// Request model for obtaining a SAS token for blob upload
/// </summary>
public class SasTokenRequest
{
    /// <summary>
    /// The file name to be uploaded
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional: File size in bytes for validation
    /// </summary>
    public long? FileSizeBytes { get; set; }
    
    /// <summary>
    /// Optional: File MIME type for validation
    /// </summary>
    public string? MimeType { get; set; }
}

/// <summary>
/// Response model containing SAS token and metadata for client upload
/// </summary>
public class SasTokenResponse
{
    /// <summary>
    /// URI with embedded SAS token for direct blob upload
    /// </summary>
    public Uri BlobUri { get; set; } = null!;
    
    /// <summary>
    /// The SAS token signature (query string)
    /// </summary>
    public string? Signature { get; set; }
    
    /// <summary>
    /// SAS token expiration time in UTC
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Seconds until token expiration
    /// </summary>
    public int ExpiresInSeconds => Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds);
    
    /// <summary>
    /// Container name for reference
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;
}

/// <summary>
/// Response model for successful upload confirmation
/// </summary>
public class UploadConfirmationResponse
{
    /// <summary>
    /// Unique blob URI
    /// </summary>
    public Uri BlobUri { get; set; } = null!;
    
    /// <summary>
    /// File name as stored in blob
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Size of uploaded file in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Timestamp of upload
    /// </summary>
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code/type
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Request ID for correlation (only in responses)
    /// </summary>
    public string? RequestId { get; set; }
}

/// <summary>
/// A document stored in the signed-in user's folder
/// </summary>
public class DocumentItem
{
    /// <summary>
    /// File name relative to the user's folder
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Content type recorded on the blob, if any
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Last modified time in UTC
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }
}

/// <summary>
/// Response model containing a short-lived read-only download URL
/// </summary>
public class DownloadUrlResponse
{
    /// <summary>
    /// Blob URI with an embedded read-only SAS
    /// </summary>
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// SAS expiration time in UTC
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
