// Local development only: starts the API, the Blazor app and the Angular app together.
// Run with `dotnet run --project apphost` (or `aspire run`); Ctrl+C stops all three.
// Azurite is started separately (VS Code extension or docker compose), see docs/LOCAL_DEVELOPMENT.md.

var builder = DistributedApplication.CreateBuilder(args);

// Fixed ports, no Aspire proxy: browser-side config (CORS origins, AngularApp:Url,
// ng-app environments) refers to these exact URLs from launchSettings.json / angular.json.
var api = builder.AddProject<Projects.DocUploaderApi>("api")
    .WithEndpoint("http", e => e.IsProxied = false);

builder.AddProject<Projects.UploaderDoc>("web", launchProfileName: "https")
    .WithEndpoint("https", e => e.IsProxied = false)
    .WithEndpoint("http", e => e.IsProxied = false)
    .WaitFor(api);

builder.AddJavaScriptApp("ng", "../ng-app", "start")
    .WithHttpEndpoint(port: 4200, isProxied: false)
    .WaitFor(api);

builder.Build().Run();
