using McpNetDll.Registry;
using McpNetDll.Repository;
using McpNetDll.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSingleton<ITypeRegistry>(sp =>
{
    var reg = new TypeRegistry();
    var dlls = builder.Configuration.GetSection("DllPaths").Get<string[]>() ?? Array.Empty<string>();
    reg.LoadAssemblies(dlls);
    return reg;
});
builder.Services.AddSingleton<IMetadataRepository, MetadataRepository>();
builder.Services.AddSingleton<IMcpResponseFormatter, McpResponseFormatter>();

var app = builder.Build();

app.MapGet("/", () => "McpNetDll Web: DLL documentation browser");

app.MapGet("/api/namespaces", (IMetadataRepository repo, IMcpResponseFormatter formatter, ITypeRegistry registry, string[]? namespaces, int? limit, int? offset)
    => Results.Text(formatter.FormatNamespaceResponse(repo.QueryNamespaces(namespaces, limit ?? 50, offset ?? 0), registry), "application/json"));

app.MapGet("/api/types", (IMetadataRepository repo, IMcpResponseFormatter formatter, ITypeRegistry registry, string[] typeNames)
    => Results.Text(formatter.FormatTypeDetailsResponse(repo.QueryTypeDetails(typeNames), registry), "application/json"));

app.MapGet("/api/search", (IMetadataRepository repo, IMcpResponseFormatter formatter, ITypeRegistry registry, string pattern, string? scope, int? limit, int? offset)
    => Results.Text(formatter.FormatSearchResponse(repo.SearchElements(pattern, scope ?? "all", limit ?? 100, offset ?? 0), registry), "application/json"));

app.Run();
