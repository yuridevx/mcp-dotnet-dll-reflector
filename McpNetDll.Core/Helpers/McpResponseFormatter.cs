using System.Linq;
using System.Text;
using System.Text.Json;
using McpNetDll.Registry;
using McpNetDll.Repository;

namespace McpNetDll.Helpers;

public class McpResponseFormatter : IMcpResponseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true, 
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull 
    };

    public string FormatNamespaceResponse(NamespaceQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return JsonSerializer.Serialize(new { error = result.Error }, JsonOptions);
        }

        var enhancedResult = new
        {
            Summary = $"Found {result.Pagination.Total} namespaces in loaded assemblies",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            Namespaces = result.Namespaces,
            Pagination = result.Pagination
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
            Types = result.Types
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    public string FormatSearchResponse(SearchQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return JsonSerializer.Serialize(new { error = result.Error }, JsonOptions);
        }

        var enhancedResult = new
        {
            Summary = $"Found {result.Pagination.Total} matching elements",
            LoadedAssemblyInfo = GetNamespaceInfo(registry).Trim(),
            Results = result.Results,
            Pagination = result.Pagination
        };

        return JsonSerializer.Serialize(enhancedResult, JsonOptions);
    }

    private string GetNamespaceInfo(ITypeRegistry registry)
    {
        var namespaces = registry.GetAllNamespaces();
        if (!namespaces.Any()) return "";

        if (namespaces.Count <= 8)
        {
            return $" Currently loaded namespaces ({namespaces.Count}):\n{BuildNamespaceTree(namespaces)}";
        }
        else
        {
            var topNamespaces = namespaces.Take(8).ToList();
            var tree = BuildNamespaceTree(topNamespaces);
            return $" Currently loaded namespaces ({namespaces.Count}, showing first 8):\n{tree}\n... (+{namespaces.Count - 8} more)";
        }
    }

    private string BuildNamespaceTree(System.Collections.Generic.IEnumerable<string> namespaces)
    {
        var tree = new System.Collections.Generic.Dictionary<string, object>();

        foreach (var ns in namespaces.OrderBy(x => x))
        {
            var parts = ns.Split('.');
            var current = tree;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part))
                {
                    current[part] = new System.Collections.Generic.Dictionary<string, object>();
                }
                current = (System.Collections.Generic.Dictionary<string, object>)current[part];
            }
        }

        return RenderTree(tree, "", true);
    }

    private string RenderTree(System.Collections.Generic.Dictionary<string, object> node, string prefix, bool isRoot)
    {
        var result = new StringBuilder();
        var items = node.Keys.OrderBy(x => x).ToList();

        for (int i = 0; i < items.Count; i++)
        {
            var key = items[i];
            var isLast = i == items.Count - 1;
            var currentPrefix = isRoot ? "" : prefix;
            var connector = isRoot ? "" : (isLast ? "└── " : "├── ");

            result.AppendLine($"{currentPrefix}{connector}{key}");

            var childDict = (System.Collections.Generic.Dictionary<string, object>)node[key];
            if (childDict.Any())
            {
                var nextPrefix = isRoot ? "" : prefix + (isLast ? "    " : "│   ");
                result.Append(RenderTree(childDict, nextPrefix, false));
            }
        }

        return result.ToString();
    }
}


