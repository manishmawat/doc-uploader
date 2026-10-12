using System.Net.Http.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace UploaderDoc.Services
{
    public class DocumentUploadService : IDocumentUploadService
    {
        private readonly HttpClient _httpClient;
        private readonly BlobStorageOptions blobStorageOptions;
        private readonly IAccessTokenProvider _accessTokenProvider;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        
        public DocumentUploadService(HttpClient httpClient, IAccessTokenProvider accessTokenProvider, 
            IOptions<BlobStorageOptions> blobStorageOptions, AuthenticationStateProvider authenticationStateProvider)
        {
            this._httpClient = httpClient;
            this.blobStorageOptions = blobStorageOptions.Value;
            _accessTokenProvider = accessTokenProvider;
            _authenticationStateProvider = authenticationStateProvider;
        }
        
        public async Task UploadDocumentAsync(Stream fileStream, string fileName)
        {
            Console.WriteLine("Uploading document...");
            
            // Get logged-in user's email
            var userEmail = await GetUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new Exception("User is not authenticated");
            
            // Sanitize email to use as folder name (replace @ with .)
            var userFolder = userEmail.Replace("@", ".").Replace(" ", "_");
            
            var sasInfo = await GetBlobSAS();

            var sasUri = new UriBuilder(sasInfo!.BlobUri!)
            {
                Query = sasInfo.Signature
            };

            // Append user folder and filename to the blob URI
            var blobPath = $"{userFolder}/{fileName}";
            var userSpecificUri = new UriBuilder(sasUri.Uri)
            {
                Path = $"{sasUri.Uri.AbsolutePath}/{blobPath}",
                Query = sasInfo.Signature
            };

            var blob = new BlobClient(userSpecificUri.Uri);
            await blob.UploadAsync(fileStream, overwrite: true);
            
            Console.WriteLine($"Document uploaded to: {blobPath}");
        }
        
        private async Task<string?> GetUserEmailAsync()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (!user.Identity?.IsAuthenticated ?? false)
                return null;
            
            // Try to get email from claims (Entra ID includes 'email' claim for guests)
            var email = user.FindFirst("email")?.Value ?? 
                       user.FindFirst(ClaimTypes.Email)?.Value ??
                       user.FindFirst("preferred_username")?.Value ??
                       user.Identity?.Name;
            
            return email;
        }
        private async Task<StorageEntitySas?> GetBlobSAS()
        {
            try
            {
                Console.WriteLine("Getting SAS URL from API...");

                // Validate configuration
                if (string.IsNullOrEmpty(blobStorageOptions.ApiScope))
                {
                    throw new InvalidOperationException("API scope is not configured. Please check appsettings.json BlobStorage:ApiScope setting.");
                }

                // Get access token using configured scope
                var tokenRequest = new AccessTokenRequestOptions
                {
                    Scopes = blobStorageOptions.Scopes
                };

                var tokenResult = await _accessTokenProvider.RequestAccessToken(tokenRequest);
                if (!tokenResult.TryGetToken(out var token))
                {
                    throw new Exception("Failed to acquire access token");
                }

                // Add Authorization header
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);
                
                Console.WriteLine($"Using configured API scope: {blobStorageOptions.ApiScope}");

                // Call your authenticated API
                var response = await _httpClient.GetAsync(blobStorageOptions.SasApiEndpoint);

                if (response.IsSuccessStatusCode)
                {
                    var sasInfo = await response.Content.ReadFromJsonAsync<StorageEntitySas>();
                    if (sasInfo == null || sasInfo.BlobUri == null || string.IsNullOrEmpty(sasInfo.Signature))
                    {
                        throw new Exception("Failed to get SAS info from API.");
                    }
                    return sasInfo;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // throw new AccessTokenNotAvailableException();
                    throw new Exception("Unauthorized access - token may be invalid or expired.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API call failed: {response.StatusCode} - {error}");
                }
            }
            catch (AccessTokenNotAvailableException)
            {
                // Redirect to login
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting SAS token: {ex.Message}");
                throw;
            }
        }
        // public class BlobStorageOptions
        // {
        //     public string SasApiEndpoint { get; set; } = "api/file-services/access";
        // }

        public class StorageEntitySas
        {
            public Uri? BlobUri { get; set; }
            public string? Signature { get; set; }
        }
    }
}