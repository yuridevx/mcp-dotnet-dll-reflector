using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpNetDll.Core.Indexing;
using McpNetDll.Registry;
using McpNetDll.Repository;

namespace McpNetDll.Helpers;

public class McpResponseFormatter : IMcpResponseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string FormatNamespaceResponse(NamespaceQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
            return JsonSerializer.Serialize(new { error = result.Error }, JsonOptions);

        var enhancedResult = new
        {
            Summary = $"Found {result.Pagination.Total} namespaces in loaded assemblies",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            result.Namespaces,
            result.Pagination
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    public string FormatTypeDetailsResponse(TypeDetailsQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            var errorResponse = new
            {
                error = result.Error,
                availableTypes = result.AvailableTypes
            };
            return JsonSerializer.Serialize(errorResponse, JsonOptions);
        }

        var enhancedResult = new
        {
            Summary = $"Type details for {result.Types.Count} requested type(s)",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            result.Types
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    public string FormatSearchResponse(SearchQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
            return JsonSerializer.Serialize(new { error = result.Error }, JsonOptions);

        var enhancedResult = new
        {
            Summary = $"Found {result.Pagination.Total} matching elements",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            result.Results,
            result.Pagination
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    public string FormatKeywordSearchResponse(KeywordSearchResult result, ITypeRegistry registry)
    {
        var enhancedResult = new
        {
            Summary = $"Found {result.Pagination.Total} matches for '{result.SearchTerms}' in {result.SearchTimeMs:F1}ms",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            result.Results,
            result.FacetCounts,
            result.Pagination,
            result.SearchTimeMs
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    private string GetNamespaceInfo(ITypeRegistry registry)
    {
        var namespaces = registry.GetAllNamespaces();
        if (!namespaces.Any()) return "";

        if (namespaces.Count <= 8)
            return $" Currently loaded namespaces ({namespaces.Count}):\n{BuildNamespaceTree(namespaces)}";

        var topNamespaces = namespaces.Take(8).ToList();
        var tree = BuildNamespaceTree(topNamespaces);
        return
            $" Currently loaded namespaces ({namespaces.Count}, showing first 8):\n{tree}\n... (+{namespaces.Count - 8} more)";
    }

    private string BuildNamespaceTree(IEnumerable<string> namespaces)
    {
        var tree = new Dictionary<string, object>();

        foreach (var ns in namespaces.OrderBy(x => x))
        {
            var parts = ns.Split('.');
            var current = tree;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part)) current[part] = new Dictionary<string, object>();
                current = (Dictionary<string, object>)current[part];
            }
        }

        return RenderTree(tree, "", true);
    }

    private string RenderTree(Dictionary<string, object> node, string prefix, bool isRoot)
    {
        var result = new StringBuilder();
        var items = node.Keys.OrderBy(x => x).ToList();

        for (var i = 0; i < items.Count; i++)
        {
            var key = items[i];
            var isLast = i == items.Count - 1;
            var currentPrefix = isRoot ? "" : prefix;
            var connector = isRoot ? "" : isLast ? "└── " : "├── ";

            result.AppendLine($"{currentPrefix}{connector}{key}");

            var childDict = (Dictionary<string, object>)node[key];
            if (childDict.Any())
            {
                var nextPrefix = isRoot ? "" : prefix + (isLast ? "    " : "│   ");
                result.Append(RenderTree(childDict, nextPrefix, false));
            }
        }

        return result.ToString();
    }
}