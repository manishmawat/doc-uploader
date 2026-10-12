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
    options.ProviderOptions.LoginMode = "redirect";

    // Request consent for the SAS API scope at sign-in so uploads don't trigger a second redirect
    var apiScope = builder.Configuration[$"{BlobStorageOptions.SectionName}:ApiScope"];
    if (!string.IsNullOrEmpty(apiScope))
    {
        options.ProviderOptions.AdditionalScopesToConsent.Add(apiScope);
    }

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