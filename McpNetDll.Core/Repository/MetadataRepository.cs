using System.Text.RegularExpressions;
using McpNetDll.Registry;

namespace McpNetDll.Repository;

public class MetadataRepository : IMetadataRepository
{
    private readonly ITypeRegistry _typeRegistry;

    public MetadataRepository(ITypeRegistry typeRegistry)
    {
        _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
    }

    public NamespaceQueryResult QueryNamespaces(string[]? namespaces = null, int limit = 50, int offset = 0)
    {
        var loadErrors = _typeRegistry.GetLoadErrors();
        var allTypes = _typeRegistry.GetAllTypes();

        if (loadErrors.Any() && !allTypes.Any())
            return new NamespaceQueryResult
            {
                Error = $"Failed to load all assemblies: {string.Join(", ", loadErrors)}"
            };

        var types = allTypes.AsEnumerable();

        if (namespaces?.Length > 0)
        {
            var availableNs = _typeRegistry.GetAllNamespaces();
            var missing = namespaces.Where(ns => !availableNs.Contains(ns, StringComparer.OrdinalIgnoreCase)).ToList();

            if (missing.Any())
                return new NamespaceQueryResult
                {
                    Error = $"Namespace(s) not found: {string.Join(", ", missing)}"
                };

            types = namespaces.SelectMany(ns => _typeRegistry.GetTypesByNamespace(ns));
        }

        var namespaceGroups = types.GroupBy(t => t.Namespace)
            .Select(g => new NamespaceMetadata
            {
                Name = g.Key,
                TypeCount = g.Count(),
                Types = g.OrderBy(t => t.Name).ToList()
            })
            .OrderBy(ns => ns.Name)
            .ToList();

        var total = namespaceGroups.Count;
        var paginatedResult = namespaceGroups.Skip(offset).Take(limit).ToList();

        return new NamespaceQueryResult
        {
            Namespaces = paginatedResult,
            Pagination = new PaginationInfo
            {
                Total = total,
                Limit = limit,
                Offset = offset
            }
        };
    }

    public TypeDetailsQueryResult QueryTypeDetails(string[] typeNames)
    {
        if (typeNames == null || typeNames.Length == 0)
            return new TypeDetailsQueryResult
            {
                Error = "TypeNames array cannot be empty."
            };

        var loadErrors = _typeRegistry.GetLoadErrors();
        var allTypes = _typeRegistry.GetAllTypes();

        if (loadErrors.Any() && !allTypes.Any())
            return new TypeDetailsQueryResult
            {
                Error = $"Failed to load all assemblies: {string.Join(", ", loadErrors)}"
            };

        var found = new List<TypeMetadata>();
        var missing = new List<string>();

        foreach (var name in typeNames)
            if (_typeRegistry.TryGetType(name, out var type) && type != null)
                found.Add(type);
            else
                missing.Add(name);

        if (missing.Any())
        {
            var availableTypes = allTypes
                .Select(t => $"{t.Namespace}.{t.Name}")
                .OrderBy(x => x)
                .ToList();

            return new TypeDetailsQueryResult
            {
                Error = $"Type(s) not found or ambiguous: {string.Join(", ", missing)}",
                AvailableTypes = availableTypes
            };
        }

        return new TypeDetailsQueryResult
        {
            Types = found.OrderBy(t => t.Name).ThenBy(t => t.Namespace).ToList()
        };
    }

    public SearchQueryResult SearchElements(string pattern, string searchScope = "all", int limit = 100, int offset = 0)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var results = new List<SearchResult>();
            var types = _typeRegistry.GetAllTypes().OrderBy(t => t.Namespace).ThenBy(t => t.Name);

            foreach (var type in types) SearchInType(type, regex, searchScope, results);

            var total = results.Count;
            var paginatedResults = results.Skip(offset).Take(limit).ToList();

            return new SearchQueryResult
            {
                Results = paginatedResults,
                Pagination = new PaginationInfo
                {
                    Total = total,
                    Limit = limit,
                    Offset = offset
                },
                Summary = $"Found {total} result{(total == 1 ? "" : "s")} for pattern '{pattern}'"
            };
        }
        catch (ArgumentException ex)
        {
            return new SearchQueryResult
            {
                Error = $"Invalid regex pattern: {ex.Message}"
            };
        }
    }

    private void SearchInType(TypeMetadata type, Regex regex, string searchScope, List<SearchResult> results)
    {
        if ((searchScope == "all" || searchScope == "types") && regex.IsMatch(type.Name))
            results.Add(new SearchResult
            {
                ElementType = "Type",
                Name = type.Name,
                Namespace = type.Namespace,
                TypeKind = type.TypeKind,
                FullName = $"{type.Namespace}.{type.Name}"
            });

        if ((searchScope == "all" || searchScope == "methods") && type.Methods != null)
            foreach (var method in type.Methods)
                if (regex.IsMatch(method.Name))
                    results.Add(new SearchResult
                    {
                        ElementType = "Method",
                        Name = method.Name,
                        ParentType = $"{type.Namespace}.{type.Name}",
                        ReturnType = method.ReturnType,
                        Parameters = method.Parameters?.Select(p => p.Type)
                    });

        if ((searchScope == "all" || searchScope == "properties") && type.Properties != null)
            foreach (var property in type.Properties)
                if (regex.IsMatch(property.Name))
                    results.Add(new SearchResult
                    {
                        ElementType = "Property",
                        Name = property.Name,
                        ParentType = $"{type.Namespace}.{type.Name}",
                        PropertyType = property.Type
                    });

        if ((searchScope == "all" || searchScope == "fields") && type.Fields != null)
            foreach (var field in type.Fields)
                if (regex.IsMatch(field.Name))
                    results.Add(new SearchResult
                    {
                        ElementType = "Field",
                        Name = field.Name,
                        ParentType = $"{type.Namespace}.{type.Name}",
                        FieldType = field.Type
                    });

        if ((searchScope == "all" || searchScope == "enums") && type.EnumValues != null)
            foreach (var enumValue in type.EnumValues)
                if (regex.IsMatch(enumValue.Name))
                    results.Add(new SearchResult
                    {
                        ElementType = "EnumValue",
                        Name = enumValue.Name,
                        ParentType = $"{type.Namespace}.{type.Name}",
                        Value = enumValue.Value
                    });
    }
}