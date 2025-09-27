using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using McpNetDll.Helpers;

namespace McpNetDll.Registry;

public class TypeRegistry : ITypeRegistry
{
    private readonly List<TypeMetadata> _types = new();
    private readonly XmlDocCommentIndex _xmlDocs = new();
    private readonly Dictionary<string, TypeMetadata> _typeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _simpleNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _namespaceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadErrors = new();

    public void LoadAssembly(string assemblyPath)
    {
        try
        {
            var path = PathHelper.ConvertWslPath(assemblyPath);
            var module = ModuleDefMD.Load(path);
            // Load side-by-side XML docs if present
            var xmlPath = System.IO.Path.ChangeExtension(path, ".xml");
            _xmlDocs.AddFromXml(xmlPath);
            foreach (var type in module.Types.Where(t => t.IsPublic))
            {
                var metadata = TypeMetadataFactory.CreateTypeMetadata(type);
                // Enrich with XML docs
                var fullTypeName = $"{metadata.Namespace}.{metadata.Name}";
                var typeDoc = _xmlDocs.GetTypeDoc(fullTypeName);
                if (!string.IsNullOrWhiteSpace(typeDoc))
                {
                    metadata = new TypeMetadata
                    {
                        Name = metadata.Name,
                        Namespace = metadata.Namespace,
                        TypeKind = metadata.TypeKind,
                        Documentation = metadata.Documentation ?? typeDoc,
                        MethodCount = metadata.MethodCount,
                        PropertyCount = metadata.PropertyCount,
                        FieldCount = metadata.FieldCount,
                        EnumValues = metadata.EnumValues,
                        StructLayout = metadata.StructLayout,
                        Fields = metadata.Fields,
                        Methods = metadata.Methods,
                        Properties = metadata.Properties
                    };
                }
                if (metadata.Methods != null)
                {
                    var newMethods = new List<MethodMetadata>(metadata.Methods.Count);
                    foreach (var m in metadata.Methods)
                    {
                        var doc = _xmlDocs.GetMethodDoc(fullTypeName, m.Name);
                        newMethods.Add(new MethodMetadata
                        {
                            Name = m.Name,
                            ReturnType = m.ReturnType,
                            Documentation = m.Documentation ?? doc,
                            IsStatic = m.IsStatic,
                            Parameters = m.Parameters
                        });
                    }
                    metadata = new TypeMetadata
                    {
                        Name = metadata.Name,
                        Namespace = metadata.Namespace,
                        TypeKind = metadata.TypeKind,
                        Documentation = metadata.Documentation,
                        MethodCount = metadata.MethodCount,
                        PropertyCount = metadata.PropertyCount,
                        FieldCount = metadata.FieldCount,
                        EnumValues = metadata.EnumValues,
                        StructLayout = metadata.StructLayout,
                        Fields = metadata.Fields,
                        Methods = newMethods,
                        Properties = metadata.Properties
                    };
                }
                if (metadata.Properties != null)
                {
                    var newProps = new List<PropertyMetadata>(metadata.Properties.Count);
                    foreach (var p in metadata.Properties)
                    {
                        var doc = _xmlDocs.GetPropertyDoc(fullTypeName, p.Name);
                        newProps.Add(new PropertyMetadata
                        {
                            Name = p.Name,
                            Type = p.Type,
                            Documentation = p.Documentation ?? doc,
                            IsStatic = p.IsStatic
                        });
                    }
                    metadata = new TypeMetadata
                    {
                        Name = metadata.Name,
                        Namespace = metadata.Namespace,
                        TypeKind = metadata.TypeKind,
                        Documentation = metadata.Documentation,
                        MethodCount = metadata.MethodCount,
                        PropertyCount = metadata.PropertyCount,
                        FieldCount = metadata.FieldCount,
                        EnumValues = metadata.EnumValues,
                        StructLayout = metadata.StructLayout,
                        Fields = metadata.Fields,
                        Methods = metadata.Methods,
                        Properties = newProps
                    };
                }
                if (metadata.Fields != null)
                {
                    var newFields = new List<FieldMetadata>(metadata.Fields.Count);
                    foreach (var f in metadata.Fields)
                    {
                        var doc = _xmlDocs.GetFieldDoc(fullTypeName, f.Name);
                        newFields.Add(new FieldMetadata
                        {
                            Name = f.Name,
                            Type = f.Type,
                            Offset = f.Offset,
                            IsStatic = f.IsStatic,
                            Documentation = f.Documentation ?? doc
                        });
                    }
                    metadata = new TypeMetadata
                    {
                        Name = metadata.Name,
                        Namespace = metadata.Namespace,
                        TypeKind = metadata.TypeKind,
                        Documentation = metadata.Documentation,
                        MethodCount = metadata.MethodCount,
                        PropertyCount = metadata.PropertyCount,
                        FieldCount = metadata.FieldCount,
                        EnumValues = metadata.EnumValues,
                        StructLayout = metadata.StructLayout,
                        Fields = newFields,
                        Methods = metadata.Methods,
                        Properties = metadata.Properties
                    };
                }
                RegisterType(metadata);
            }
        }
        catch (Exception ex)
        {
            _loadErrors.Add($"Failed to load {assemblyPath}: {ex.Message}");
        }
    }

    public void LoadAssemblies(IEnumerable<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            LoadAssembly(path);
        }
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

    public List<TypeMetadata> GetAllTypes() => new(_types);

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

    public List<string> GetLoadErrors() => new(_loadErrors);
}


