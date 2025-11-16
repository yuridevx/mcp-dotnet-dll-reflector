using dnlib.DotNet;
using McpNetDll.Helpers;

namespace McpNetDll.Registry;

public class TypeRegistry : ITypeRegistry
{
    private readonly List<string> _loadErrors = new();
    private readonly Dictionary<string, List<TypeMetadata>> _namespaceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _simpleNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TypeMetadata> _typeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TypeMetadata> _types = new();
    private readonly XmlDocCommentIndex _xmlDocs = new();

    public void LoadAssembly(string assemblyPath)
    {
        try
        {
            var path = PathHelper.ConvertWslPath(assemblyPath);
            var module = ModuleDefMD.Load(path);
            // Load side-by-side XML docs if present
            var xmlPath = Path.ChangeExtension(path, ".xml");
            _xmlDocs.AddFromXml(xmlPath);
            foreach (var type in module.Types.Where(t => t.IsPublic))
            {
                ProcessType(type, type.Namespace, type.Name.String);
            }
        }
        catch (Exception ex)
        {
            _loadErrors.Add($"Failed to load {assemblyPath}: {ex.Message}");
        }
    }

    public void LoadAssemblies(string[] assemblyPaths)
    {
        foreach (var path in assemblyPaths) LoadAssembly(path);
    }

    private void ProcessType(TypeDef type, string parentNamespace, string parentName)
    {
        var metadata = TypeMetadataFactory.CreateTypeMetadata(type);

        // For nested types, adjust the namespace and name
        if (type.IsNested)
        {
            // Keep the parent's namespace for nested types
            metadata = metadata with { Namespace = parentNamespace };
            // Use the + notation for nested type names (e.g., "LokiPoe+ClientFunctions")
            metadata = metadata with { Name = $"{parentName}+{type.Name}" };
        }

        var fullTypeName = $"{metadata.Namespace}.{metadata.Name}";

        // Enrich with XML docs
        var typeDoc = _xmlDocs.GetTypeDoc(fullTypeName);
        if (!string.IsNullOrWhiteSpace(typeDoc))
            metadata = metadata with { Documentation = metadata.Documentation ?? typeDoc };

        if (metadata.Methods != null)
        {
            var newMethods = new List<MethodMetadata>(metadata.Methods.Count);
            foreach (var m in metadata.Methods)
            {
                var doc = _xmlDocs.GetMethodDoc(fullTypeName, m.Name);
                newMethods.Add(doc is not null ? m with { Documentation = m.Documentation ?? doc } : m);
            }

            metadata = metadata with { Methods = newMethods };
        }

        if (metadata.Properties != null)
        {
            var newProps = new List<PropertyMetadata>(metadata.Properties.Count);
            foreach (var p in metadata.Properties)
            {
                var doc = _xmlDocs.GetPropertyDoc(fullTypeName, p.Name);
                newProps.Add(doc is not null ? p with { Documentation = p.Documentation ?? doc } : p);
            }

            metadata = metadata with { Properties = newProps };
        }

        if (metadata.Fields != null)
        {
            var newFields = new List<FieldMetadata>(metadata.Fields.Count);
            foreach (var f in metadata.Fields)
            {
                var doc = _xmlDocs.GetFieldDoc(fullTypeName, f.Name);
                newFields.Add(doc is not null ? f with { Documentation = f.Documentation ?? doc } : f);
            }

            metadata = metadata with { Fields = newFields };
        }

        RegisterType(metadata);

        // Process public nested types recursively
        // Include public, protected, and protected internal nested types
        foreach (var nestedType in type.NestedTypes.Where(t =>
            t.IsNestedPublic ||
            t.IsNestedFamily || // protected
            t.IsNestedFamilyOrAssembly)) // protected internal
        {
            // For nested types, use the current type's name as the parent name
            var nestedParentName = type.IsNested ? metadata.Name : type.Name.String;
            ProcessType(nestedType, parentNamespace, nestedParentName);
        }
    }

    public List<TypeMetadata> GetAllTypes()
    {
        return new List<TypeMetadata>(_types);
    }

    public TypeMetadata? GetTypeByFullName(string fullName)
    {
        return _typeMap.TryGetValue(fullName, out var type) ? type : null;
    }

    public List<TypeMetadata> GetTypesBySimpleName(string simpleName)
    {
        return _simpleNameMap.TryGetValue(simpleName, out var types)
            ? new List<TypeMetadata>(types)
            : new List<TypeMetadata>();
    }

    public List<TypeMetadata> GetTypesByNamespace(string namespaceName)
    {
        return _namespaceMap.TryGetValue(namespaceName, out var types)
            ? new List<TypeMetadata>(types)
            : new List<TypeMetadata>();
    }

    public List<string> GetAllNamespaces()
    {
        return _namespaceMap.Keys.OrderBy(ns => ns).ToList();
    }

    public bool TryGetType(string name, out TypeMetadata? type)
    {
        type = null;

        if (_typeMap.TryGetValue(name, out type))
            return true;

        if (_simpleNameMap.TryGetValue(name, out var types) && types.Count == 1)
        {
            type = types[0];
            return true;
        }

        return false;
    }

    public List<string> GetLoadErrors()
    {
        return new List<string>(_loadErrors);
    }

    private void RegisterType(TypeMetadata metadata)
    {
        _types.Add(metadata);

        var fullName = $"{metadata.Namespace}.{metadata.Name}";
        _typeMap[fullName] = metadata;

        if (!_simpleNameMap.ContainsKey(metadata.Name))
            _simpleNameMap[metadata.Name] = new List<TypeMetadata>();
        _simpleNameMap[metadata.Name].Add(metadata);

        if (!_namespaceMap.ContainsKey(metadata.Namespace))
            _namespaceMap[metadata.Namespace] = new List<TypeMetadata>();
        _namespaceMap[metadata.Namespace].Add(metadata);
    }
}