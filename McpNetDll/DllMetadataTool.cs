using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpNetDll;

[McpServerToolType]
public static class DllMetadataTool
{
    [McpServerTool,
     Description(
         "Lists all public namespaces and their types in the loaded assemblies. If no namespaces are specified, it returns all namespaces. If namespaces are provided, it returns detailed type information for those namespaces only.")]
    public static string ListNamespaces(
        Extractor extractor,
        [Description("Optional: An array of namespace names to inspect. If omitted, all namespaces will be listed.")]
        string[]? namespaces = null)
    {
        return extractor.ListNamespaces(namespaces);
    }

    [McpServerTool,
     Description("Gets detailed public API information for one or more .NET types (including classes, structs, enums, and interfaces), including methods, properties, and other members. You can use full or simple type names.")]
    public static string GetTypeDetails(
        Extractor extractor,
        [Description("An array of type names (e.g., 'MyClass', 'MyNamespace.MyClass') to get details for. Use `ListNamespaces` to discover types.")]
        string[] typeNames)
    {
        return extractor.GetTypeDetails(typeNames);
    }
}