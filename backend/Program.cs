using Azure.Identity;
using Azure.Storage.Blobs;
using BlobCorsRule = Azure.Storage.Blobs.Models.BlobCorsRule;
using Azure.Storage.Blobs.Specialized;
using DocUploaderApi.Models;
using DocUploaderApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Development: include issuer/audience values in token validation errors instead of "[PII is hidden]"
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// Configure options
builder.Services.Configure<AzureBlobStorageOptions>(
    builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));

builder.Services.Configure<BlobUploadOptions>(
    builder.Configuration.GetSection(BlobUploadOptions.SectionName));

// Register Azure services - Support both Azurite (local) and Azure (production)
builder.Services.AddSingleton(sp =>
{
    var options = builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName).Get<AzureBlobStorageOptions>()
        ?? throw new InvalidOperationException("AzureBlobStorage configuration is missing");
    
    // Support both Azure and local azurite development
    if (builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(options.ConnectionString))
    {
        // Use azurite connection string for local development
        var blobServiceClient = new BlobServiceClient(options.ConnectionString);
        return blobServiceClient.GetBlobContainerClient(options.ContainerName);
    }
    else
    {
        // Use DefaultAzureCredential for Azure (Managed Identity)
        var credential = new DefaultAzureCredential();
        return new BlobContainerClient(
            new Uri($"https://{options.AccountName}.blob.core.windows.net/{options.ContainerName}"),
            credential);
    }
});

// Register blob storage service
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Add API documentation
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policyBuilder =>
    {
        var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "https://localhost:7000" };
        policyBuilder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("WWW-Authenticate") // lets the client show why a token was rejected
            .AllowCredentials();
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(DocUploaderApi.Controllers.DocumentUploadController.UploadPolicy,
        policy => policy.RequireScope("SASKey.Write"));
});
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Local development: make sure Azurite has the container and allows browser uploads from the frontend
var blobStorageOptions = app.Configuration.GetSection(AzureBlobStorageOptions.SectionName).Get<AzureBlobStorageOptions>();
if (app.Environment.IsDevelopment() && !string.IsNullOrEmpty(blobStorageOptions?.ConnectionString))
{
    try
    {
        var containerClient = app.Services.GetRequiredService<BlobContainerClient>();
        await containerClient.CreateIfNotExistsAsync();

        // Azurite rejects a properties payload with only CORS, so update the existing properties
        var corsOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var serviceClient = containerClient.GetParentBlobServiceClient();
        var serviceProperties = (await serviceClient.GetPropertiesAsync()).Value;
        serviceProperties.Cors =
        [
            new BlobCorsRule
            {
                AllowedOrigins = string.Join(",", corsOrigins),
                AllowedMethods = "GET,PUT,OPTIONS",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600
            }
        ];
        await serviceClient.SetPropertiesAsync(serviceProperties);

        Log.Information("Azurite ready: container {Container}, CORS origins {Origins}",
            containerClient.Name, corsOrigins);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not prepare Azurite. Is it running on the configured BlobEndpoint?");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "Document Uploader API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
