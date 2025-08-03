using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpNetDll;

[McpServerToolType]
public static class DllMetadataTool
{
    [McpServerTool,
     Description(
         "Lists all public namespaces and their types in the assembly. If no namespaces are specified, it returns all namespaces. If namespaces are provided, it returns detailed type information for those namespaces only.")]
    public static string ListNamespaces(
        Extractor extractor,
        [Description("The absolute path to the DLL file.")]
        string dllPath,
        [Description("Optional: An array of namespace names to inspect. If omitted, all namespaces will be listed.")]
        string[]? namespaces = null)
    {
        return extractor.ListNamespaces(dllPath, namespaces);
    }

    [McpServerTool,
     Description("Gets detailed public API information for one or more .NET types (including classes, structs, enums, and interfaces), including methods, properties, and other members. You can use full or simple type names.")]
    public static string GetTypeDetails(
        Extractor extractor,
        [Description("The absolute path to the DLL file.")]
        string dllPath,
        [Description("An array of type names (e.g., 'MyClass', 'MyNamespace.MyClass') to get details for. Use `ListTypesInNamespaces` to discover types.")]
        string[] typeNames)
    {
        return extractor.GetTypeDetails(dllPath, typeNames);
    }
}