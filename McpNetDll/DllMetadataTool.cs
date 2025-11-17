using System.ComponentModel;
using McpNetDll.Core.Indexing;
using McpNetDll.Helpers;
using McpNetDll.Registry;
using McpNetDll.Repository;
using ModelContextProtocol.Server;

namespace McpNetDll;

[McpServerToolType]
public static class DllMetadataTool
{
    [McpServerTool]
    [Description("Lists all public namespaces and their types from loaded .NET assemblies")]
    public static string ListNamespaces(
        IMetadataRepository repository,
        IMcpResponseFormatter formatter,
        ITypeRegistry registry,
        [Description(
            "Optional: An array of specific namespace names to inspect. If omitted, all loaded namespaces will be listed.")]
        string[]? namespaces = null,
        [Description("Optional: Maximum number of namespaces to return (default: 50)")]
        int? limit = null,
        [Description("Optional: Number of namespaces to skip (default: 0)")]
        int? offset = null)
    {
        var result = repository.QueryNamespaces(namespaces, limit ?? 50, offset ?? 0);
        return formatter.FormatNamespaceResponse(result, registry);
    }

    [McpServerTool]
    [Description("Gets detailed public API information for specific .NET types from loaded assemblies")]
    public static string GetTypeDetails(
        IMetadataRepository repository,
        IMcpResponseFormatter formatter,
        ITypeRegistry registry,
        [Description(
            "An array of type names to analyze. Use full names (e.g., 'MyNamespace.MyClass') or simple names if unambiguous. Use ListNamespaces to discover available types.")]
        string[] typeNames)
    {
        var result = repository.QueryTypeDetails(typeNames);
        return formatter.FormatTypeDetailsResponse(result, registry);
    }

    [McpServerTool]
    [Description(
        "Searches across all elements (types, methods, properties, fields, enum values) matching a regular expression pattern")]
    public static string SearchElements(
        IMetadataRepository repository,
        IMcpResponseFormatter formatter,
        ITypeRegistry registry,
        [Description("Regular expression pattern to search for (e.g., '.*Service.*', 'Get.*', '^I[A-Z].*')")]
        string pattern,
        [Description(
            "Optional: Element types to search in. Options: 'all' (default), 'types', 'methods', 'properties', 'fields', 'enums'")]
        string? searchScope = null,
        [Description("Optional: Maximum number of results to return (default: 100)")]
        int? limit = null,
        [Description("Optional: Number of results to skip (default: 0)")]
        int? offset = null)
    {
        var result = repository.SearchElements(pattern, searchScope ?? "all", limit ?? 100, offset ?? 0);
        return formatter.FormatSearchResponse(result, registry);
    }

    [McpServerTool]
    [Description(
        "Performs fast keyword-based search across all indexed elements using Lucene.NET full-text search")]
    public static string SearchByKeywords(
        IIndexingService indexingService,
        IMcpResponseFormatter formatter,
        ITypeRegistry registry,
        [Description("Space-separated keywords to search for (e.g., 'http client', 'async task', 'configuration builder')")]
        string keywords,
        [Description(
            "Optional: Element types to search in. Options: 'all' (default), 'types', 'methods', 'properties', 'fields', 'enums'")]
        string? searchScope = null,
        [Description("Optional: Maximum number of results to return (default: 100)")]
        int? limit = null,
        [Description("Optional: Number of results to skip (default: 0)")]
        int? offset = null)
    {
        var result = indexingService.SearchByKeywords(keywords, searchScope ?? "all", limit ?? 100, offset ?? 0);
        return formatter.FormatKeywordSearchResponse(result, registry);
    }
}