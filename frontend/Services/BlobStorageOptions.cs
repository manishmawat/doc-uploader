namespace UploaderDoc.Services;
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    
    public string SasApiEndpoint { get; set; } = string.Empty;
    public string ApiScope { get; set; } = string.Empty;
    
    public string[] Scopes => !string.IsNullOrEmpty(ApiScope) 
        ? new[] { ApiScope } 
        : Array.Empty<string>();
}