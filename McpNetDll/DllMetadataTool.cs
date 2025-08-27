using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpNetDll;

[McpServerToolType]
public static class DllMetadataTool
{
    private static string GetNamespaceInfo(Extractor extractor)
    {
        var namespaces = extractor.GetAvailableNamespaces();
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

    private static string BuildNamespaceTree(IEnumerable<string> namespaces)
    {
        var tree = new Dictionary<string, object>();
        
        foreach (var ns in namespaces.OrderBy(x => x))
        {
            var parts = ns.Split('.');
            var current = tree;
            
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part))
                {
                    current[part] = new Dictionary<string, object>();
                }
                current = (Dictionary<string, object>)current[part];
            }
        }
        
        return RenderTree(tree, "", true);
    }
    
    private static string RenderTree(Dictionary<string, object> node, string prefix, bool isRoot)
    {
        var result = new System.Text.StringBuilder();
        var items = node.Keys.OrderBy(x => x).ToList();
        
        for (int i = 0; i < items.Count; i++)
        {
            var key = items[i];
            var isLast = i == items.Count - 1;
            var currentPrefix = isRoot ? "" : prefix;
            var connector = isRoot ? "" : (isLast ? "└── " : "├── ");
            
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

    [McpServerTool,
     Description("Lists all public namespaces and their types from loaded .NET assemblies")]
    public static string ListNamespaces(
        Extractor extractor,
        [Description("Optional: An array of specific namespace names to inspect. If omitted, all loaded namespaces will be listed.")]
        string[]? namespaces = null)
    {
        var result = extractor.ListNamespaces(namespaces);
        
        // Add dynamic context to help Claude understand what's available
        if (namespaces == null || namespaces.Length == 0)
        {
            var availableNamespaces = extractor.GetAvailableNamespaces();
            var contextualResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
            
            if (contextualResult.TryGetProperty("Namespaces", out var namespacesElement))
            {
                var enhancedResult = new
                {
                    Summary = $"Found {availableNamespaces.Count} namespaces in loaded assemblies",
                    LoadedAssemblyInfo = GetNamespaceInfo(extractor).Trim(),
                    Namespaces = namespacesElement
                };
                return System.Text.Json.JsonSerializer.Serialize(enhancedResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
        }
        
        return result;
    }

    [McpServerTool,
     Description("Gets detailed public API information for specific .NET types from loaded assemblies")]
    public static string GetTypeDetails(
        Extractor extractor,
        [Description("An array of type names to analyze. Use full names (e.g., 'MyNamespace.MyClass') or simple names if unambiguous. Use ListNamespaces to discover available types.")]
        string[] typeNames)
    {
        var result = extractor.GetTypeDetails(typeNames);
        
        // Add contextual information about what assemblies are loaded
        var contextualResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
        if (contextualResult.TryGetProperty("Types", out var typesElement))
        {
            var enhancedResult = new
            {
                Summary = $"Type details for {typeNames.Length} requested type(s)",
                LoadedAssemblyInfo = GetNamespaceInfo(extractor).Trim(),
                Types = typesElement
            };
            return System.Text.Json.JsonSerializer.Serialize(enhancedResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        
        return result;
    }
}