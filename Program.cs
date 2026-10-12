using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using UploaderDoc;
using UploaderDoc.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure options
builder.Services.Configure<BlobStorageOptions>(
    builder.Configuration.GetSection(BlobStorageOptions.SectionName));

// Configure MSAL authentication for Azure AD
// Access control is managed via Azure AD guest user invitations
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);

        // Add your API scope
    // options.ProviderOptions.DefaultAccessTokenScopes.Add("api://693eed20-68f0-448e-b5df-d6892e8c08c0/SASKey.Write");
    
    // B2C specific configuration
    options.ProviderOptions.Authentication.ValidateAuthority = false;
    options.ProviderOptions.LoginMode = "redirect";
    
    // B2C default scopes
    options.ProviderOptions.DefaultAccessTokenScopes.Add("openid");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("profile");
    
    // Configure cache for SPA
    options.ProviderOptions.Cache.CacheLocation = "localStorage";
    options.ProviderOptions.Cache.StoreAuthStateInCookie = false;
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IPdfMergeService, PdfMergeService>();
builder.Services.AddScoped<IFileConverterService, FileConverterService>();
builder.Services.AddScoped<IPdfOptimizerService, PdfOptimizerService>();
builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync();