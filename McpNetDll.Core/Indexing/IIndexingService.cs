using System.Collections.Generic;
using McpNetDll.Repository;

namespace McpNetDll.Core.Indexing
{
    /// <summary>
    /// Service for indexing and searching DLL metadata using keyword-based search
    /// </summary>
    public interface IIndexingService
    {
        /// <summary>
        /// Build or rebuild the search index from the current registry
        /// </summary>
        void BuildIndex();

        /// <summary>
        /// Search for elements using keywords (space-separated terms)
        /// </summary>
        /// <param name="keywords">Space-separated keywords to search for</param>
        /// <param name="searchScope">Scope: all, types, methods, properties, fields, enums</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="offset">Number of results to skip</param>
        /// <returns>Search results with relevance scoring</returns>
        KeywordSearchResult SearchByKeywords(string keywords, string searchScope = "all", int limit = 100, int offset = 0);

        /// <summary>
        /// Get index statistics
        /// </summary>
        IndexStatistics GetStatistics();

        /// <summary>
        /// Clear the current index
        /// </summary>
        void ClearIndex();

        /// <summary>
        /// Update index when new DLLs are loaded
        /// </summary>
        void UpdateIndex();
    }

    /// <summary>
    /// Result of a keyword search
    /// </summary>
    public record KeywordSearchResult(
        List<KeywordSearchHit> Results,
        PaginationInfo Pagination,
        string SearchTerms,
        double SearchTimeMs,
        Dictionary<string, int> FacetCounts
    );

    /// <summary>
    /// Individual search hit with relevance scoring
    /// </summary>
    public record KeywordSearchHit(
        string ElementType,
        string Name,
        string FullName,
        string? ParentType,
        string? ReturnType,
        string? Documentation,
        double RelevanceScore,
        List<string> MatchedTerms,
        Dictionary<string, List<int>> HighlightPositions
    );

    /// <summary>
    /// Statistics about the search index
    /// </summary>
    public record IndexStatistics(
        int TotalDocuments,
        int TotalTerms,
        int TypesIndexed,
        int MethodsIndexed,
        int PropertiesIndexed,
        int FieldsIndexed,
        int EnumsIndexed,
        long IndexSizeBytes,
        string LastBuildTime
    );
}