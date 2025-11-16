using McpNetDll.Helpers;
using McpNetDll.Registry;

namespace McpNetDll.Web.Endpoints;

public static class LoadEndpoints
{
    public static void MapLoadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/load", (ITypeRegistry registry, string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Path is required" });

            try
            {
                registry.LoadAssembly(PathHelper.ConvertWslPath(path));
                var errors = registry.GetLoadErrors();

                // If there are load errors and no types were loaded, consider it a failure
                if (errors.Any() && registry.GetAllTypes().Count == 0)
                {
                    return Results.BadRequest(new { error = $"Failed to load assembly: {string.Join("; ", errors)}" });
                }

                return Results.Json(new
                {
                    message = "Loaded",
                    path,
                    namespaces = registry.GetAllNamespaces().Count,
                    types = registry.GetAllTypes().Count,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Failed to load assembly: {ex.Message}" });
            }
        });
    }
}