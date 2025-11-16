using McpNetDll.Repository;

namespace McpNetDll.Web.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", (IMetadataRepository repo, string pattern, string? scope, int? limit, int? offset)
            => Results.Json(repo.SearchElements(pattern, scope ?? "all", limit ?? 100, offset ?? 0)));
    }
}