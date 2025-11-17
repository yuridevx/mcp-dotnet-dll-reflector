using McpNetDll.Core;
using McpNetDll.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var dlls = builder.Configuration.GetSection("DllPaths").Get<string[]>() ?? Array.Empty<string>();

// Services
builder.Services.AddCoreServices(dlls);

// Configure JSON serialization to use PascalCase (default for .NET)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // null = PascalCase (default)
});

var app = builder.Build();

// Static UI
app.UseDefaultFiles();
app.UseStaticFiles();

// Operational APIs
app.MapInfoEndpoints();
app.MapLoadEndpoints();
app.MapNamespaceEndpoints();
app.MapTypeEndpoints();
app.MapSearchEndpoints();
app.MapKeywordSearchEndpoints();

app.Run();