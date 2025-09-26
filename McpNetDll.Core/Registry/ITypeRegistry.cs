using System.Collections.Generic;

namespace McpNetDll.Registry;

public interface ITypeRegistry
{
    List<TypeMetadata> GetAllTypes();
    TypeMetadata? GetTypeByFullName(string fullName);
    List<TypeMetadata> GetTypesBySimpleName(string simpleName);
    List<TypeMetadata> GetTypesByNamespace(string namespaceName);
    List<string> GetAllNamespaces();
    bool TryGetType(string name, out TypeMetadata? type);
    List<string> GetLoadErrors();
}

