using System.Collections.Generic;

namespace McpNetDll.Repository;

public interface IMetadataRepository
{
    NamespaceQueryResult QueryNamespaces(string[]? namespaces = null, int limit = 50, int offset = 0);
    TypeDetailsQueryResult QueryTypeDetails(string[] typeNames);
    SearchQueryResult SearchElements(string pattern, string searchScope = "all", int limit = 100, int offset = 0);
}

public class NamespaceQueryResult
{
    public List<NamespaceInfo> Namespaces { get; init; } = new();
    public PaginationInfo Pagination { get; init; } = new();
    public string? Error { get; init; }
}

public class TypeDetailsQueryResult
{
    public List<TypeMetadata> Types { get; init; } = new();
    public string? Error { get; init; }
    public List<string>? AvailableTypes { get; init; }
}

public class SearchQueryResult
{
    public List<SearchResult> Results { get; init; } = new();
    public PaginationInfo Pagination { get; init; } = new();
    public string? Error { get; init; }
}

public class NamespaceInfo
{
    public required string Name { get; init; }
    public required int TypeCount { get; init; }
    public required List<TypeSummary> Types { get; init; }
}

public class TypeSummary
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string TypeKind { get; init; }
    public int? MethodCount { get; init; }
    public int? PropertyCount { get; init; }
    public int? FieldCount { get; init; }
    public List<EnumValueMetadata>? EnumValues { get; init; }
}

public class SearchResult
{
    public required string ElementType { get; init; }
    public required string Name { get; init; }
    public string? Namespace { get; init; }
    public string? ParentType { get; init; }
    public string? TypeKind { get; init; }
    public string? FullName { get; init; }
    public string? ReturnType { get; init; }
    public string? PropertyType { get; init; }
    public string? FieldType { get; init; }
    public string? Value { get; init; }
    public IEnumerable<string>? Parameters { get; init; }
}

public class PaginationInfo
{
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
    public bool HasMore => (Offset + Limit) < Total;
}

