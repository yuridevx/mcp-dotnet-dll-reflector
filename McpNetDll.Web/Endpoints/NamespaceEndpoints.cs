using McpNetDll.Registry;
using McpNetDll.Repository;

namespace McpNetDll.Web.Endpoints;

public static class NamespaceEndpoints
{
    public static void MapNamespaceEndpoints(this IEndpointRouteBuilder app)
    {
        // Lightweight namespaces list for tree building
        app.MapGet("/api/namespaces/list", (ITypeRegistry registry)
            => Results.Json(registry.GetAllNamespaces()));

        app.MapGet("/api/namespaces", (IMetadataRepository repo, string[]? namespaces, int? limit, int? offset)
            => Results.Json(repo.QueryNamespaces(namespaces, limit ?? 50, offset ?? 0)));
    }
}