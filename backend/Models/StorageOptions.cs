namespace DocUploaderApi.Models;

/// <summary>
/// Configuration for Azure Blob Storage account and container
/// </summary>
public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";
    
    /// <summary>
    /// Name of the storage account (without .blob.core.windows.net)
    /// Used for production Azure deployments
    /// </summary>
    public string AccountName { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the blob container
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Connection string for local development with azurite
    /// Set this in appsettings.Development.json to use azurite instead of Azure
    /// </summary>
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Configuration for blob upload restrictions and behavior
/// </summary>
public class BlobUploadOptions
{
    public const string SectionName = "BlobUpload";
    
    /// <summary>
    /// Maximum file size in bytes (default: 100 MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;
    
    /// <summary>
    /// Allowed file extensions (e.g., .pdf, .docx, .xlsx)
    /// </summary>
    public string[] AllowedExtensions { get; set; } = 
    { 
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", 
        ".txt", ".csv", ".jpg", ".jpeg", ".png", ".gif", ".zip"
    };
    
    /// <summary>
    /// SAS token expiration time in minutes
    /// </summary>
    public int SasTokenExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Days a deleted document stays in the recycle bin (purged in Azure by a lifecycle rule)
    /// </summary>
    public int RecycleBinRetentionDays { get; set; } = 14;

    /// <summary>
    /// Expiry options, in hours, a user can pick for a share link
    /// (user delegation SAS can't outlive 7 days)
    /// </summary>
    public int[] ShareLinkAllowedHours { get; set; } = { 1, 24, 72 };
}
