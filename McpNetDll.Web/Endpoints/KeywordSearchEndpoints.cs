using McpNetDll.Core.Indexing;
using Microsoft.AspNetCore.Mvc;

namespace McpNetDll.Web.Endpoints
{
    /// <summary>
    /// Endpoints for keyword-based search using the indexing service
    /// </summary>
    public static class KeywordSearchEndpoints
    {
        public static void MapKeywordSearchEndpoints(this WebApplication app)
        {
            // Keyword search endpoint
            app.MapGet("/api/search/keywords", SearchByKeywords)
                .WithName("SearchByKeywords");

            // Index statistics endpoint
            app.MapGet("/api/search/index/stats", GetIndexStatistics)
                .WithName("GetIndexStatistics");

            // Rebuild index endpoint
            app.MapPost("/api/search/index/rebuild", RebuildIndex)
                .WithName("RebuildIndex");
        }

        private static IResult SearchByKeywords(
            [FromServices] IIndexingService indexingService,
            [FromQuery] string keywords,
            [FromQuery] string scope = "all",
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return Results.BadRequest(new { error = "Keywords parameter is required" });
            }

            try
            {
                var results = indexingService.SearchByKeywords(keywords, scope, limit, offset);
                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Search failed"
                );
            }
        }

        private static IResult GetIndexStatistics([FromServices] IIndexingService indexingService)
        {
            try
            {
                var stats = indexingService.GetStatistics();
                return Results.Ok(stats);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to get index statistics"
                );
            }
        }

        private static IResult RebuildIndex([FromServices] IIndexingService indexingService)
        {
            try
            {
                indexingService.BuildIndex();
                var stats = indexingService.GetStatistics();
                return Results.Ok(new
                {
                    message = "Index rebuilt successfully",
                    statistics = stats
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to rebuild index"
                );
            }
        }
    }
}