using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using McpNetDll.Helpers;

namespace McpNetDll.Registry;

public class TypeRegistry : ITypeRegistry
{
    private readonly List<TypeMetadata> _types = new();
    private readonly Dictionary<string, TypeMetadata> _typeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _simpleNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _namespaceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadErrors = new();

    public void LoadAssembly(string assemblyPath)
    {
        try
        {
            var module = ModuleDefMD.Load(PathHelper.ConvertWslPath(assemblyPath));
            foreach (var type in module.Types.Where(t => t.IsPublic))
            {
                var metadata = TypeMetadataFactory.CreateTypeMetadata(type);
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


