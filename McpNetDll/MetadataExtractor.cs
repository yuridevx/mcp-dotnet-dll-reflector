using dnlib.DotNet;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace McpNetDll;

public class Extractor
{
    private readonly List<TypeMetadata> _types = new();
    private readonly Dictionary<string, TypeMetadata> _typeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeMetadata>> _simpleNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadErrors = new();

    public Extractor(string[] assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                var module = ModuleDefMD.Load(ConvertWslPath(path));
                foreach (var type in module.Types.Where(t => t.IsPublic))
                {
                    var metadata = CreateTypeMetadata(type);
                    _types.Add(metadata);
                    
                    var fullName = $"{metadata.Namespace}.{metadata.Name}";
                    _typeMap[fullName] = metadata;
                    
                    if (!_simpleNameMap.ContainsKey(metadata.Name))
                        _simpleNameMap[metadata.Name] = new List<TypeMetadata>();
                    _simpleNameMap[metadata.Name].Add(metadata);
                }
            }
            catch (Exception ex)
            {
                _loadErrors.Add($"Failed to load {path}: {ex.Message}");
            }
        }
    }

    public Extractor() : this(Array.Empty<string>()) { }

    public string ListNamespaces(string[]? namespaces = null)
    {
        if (_loadErrors.Any() && !_types.Any())
            return JsonSerializer.Serialize(new { error = $"Failed to load all assemblies: {string.Join(", ", _loadErrors)}" }, JsonOptions);

        var types = _types.AsEnumerable();
        if (namespaces?.Length > 0)
        {
            var availableNs = _types.Select(t => t.Namespace).Distinct().ToList();
            var missing = namespaces.Where(ns => !availableNs.Contains(ns, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Any())
                return JsonSerializer.Serialize(new { error = $"Namespace(s) not found: {string.Join(", ", missing)}", availableNamespaces = availableNs.OrderBy(x => x) }, JsonOptions);
            
            types = _types.Where(t => namespaces.Contains(t.Namespace, StringComparer.OrdinalIgnoreCase));
        }

        var result = types.GroupBy(t => t.Namespace)
            .Select(g => new {
                Name = g.Key,
                TypeCount = g.Count(),
                Types = g.Select(t => new {
                    t.Name, t.Namespace, t.TypeKind, t.MethodCount, t.PropertyCount, t.FieldCount, t.EnumValues
                }).OrderBy(t => t.Name)
            })
            .OrderBy(ns => ns.Name);

        return JsonSerializer.Serialize(new { Namespaces = result }, JsonOptions);
    }

    public string GetTypeDetails(string[] typeNames)
    {
        if (typeNames == null || typeNames.Length == 0)
            return JsonSerializer.Serialize(new { error = "TypeNames array cannot be empty." }, JsonOptions);

        if (_loadErrors.Any() && !_types.Any())
            return JsonSerializer.Serialize(new { error = $"Failed to load all assemblies: {string.Join(", ", _loadErrors)}" }, JsonOptions);

        var found = new List<TypeMetadata>();
        var missing = new List<string>();

        foreach (var name in typeNames)
        {
            if (_typeMap.TryGetValue(name, out var type))
                found.Add(type);
            else if (_simpleNameMap.TryGetValue(name, out var types) && types.Count == 1)
                found.Add(types[0]);
            else
                missing.Add(name);
        }

        if (missing.Any())
            return JsonSerializer.Serialize(new {
                error = $"Type(s) not found or ambiguous: {string.Join(", ", missing)}",
                availableTypes = _typeMap.Keys.OrderBy(x => x)
            }, JsonOptions);

        return JsonSerializer.Serialize(new { Types = found.OrderBy(t => t.Name).ThenBy(t => t.Namespace) }, JsonOptions);
    }

    // Backward compatibility methods for tests
    public string ListNamespaces(string assemblyPath, string[]? namespaces = null)
    {
        if (string.IsNullOrEmpty(assemblyPath))
            return JsonSerializer.Serialize(new { error = "Assembly path cannot be null or empty." }, JsonOptions);
        if (!File.Exists(assemblyPath))
            return JsonSerializer.Serialize(new { error = "Assembly file not found." }, JsonOptions);
        
        return new Extractor(new[] { assemblyPath }).ListNamespaces(namespaces);
    }

    public string GetTypeDetails(string assemblyPath, string[] typeNames)
    {
        if (string.IsNullOrEmpty(assemblyPath))
            return JsonSerializer.Serialize(new { error = "Assembly path cannot be null or empty." }, JsonOptions);
        if (!File.Exists(assemblyPath))
            return JsonSerializer.Serialize(new { error = "Assembly file not found." }, JsonOptions);
        
        return new Extractor(new[] { assemblyPath }).GetTypeDetails(typeNames);
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private TypeMetadata CreateTypeMetadata(TypeDef type) => new()
    {
        Name = type.Name.String,
        Namespace = type.Namespace.String,
        TypeKind = GetTypeKind(type),
        MethodCount = type.Methods.Count(m => m.IsPublic && !m.IsSpecialName),
        PropertyCount = type.Properties.Count(p => p.GetMethod?.IsPublic ?? p.SetMethod?.IsPublic ?? false),
        FieldCount = GetFields(type)?.Count,
        EnumValues = type.IsEnum ? GetEnumValues(type) : null,
        Methods = type.Methods.Where(m => m.IsPublic && !m.IsSpecialName)
            .Select(m => new MethodMetadata
            {
                Name = m.Name,
                ReturnType = m.ReturnType.FullName,
                Parameters = (m.HasThis ? m.Parameters.Skip(1) : m.Parameters)
                    .Select(p => new ParameterMetadata { Name = p.Name, Type = p.Type.FullName }).ToList()
            }).OrderBy(m => m.Name).ToList(),
        Properties = type.Properties.Where(p => p.GetMethod?.IsPublic ?? p.SetMethod?.IsPublic ?? false)
            .Select(p => new PropertyMetadata { Name = p.Name, Type = p.PropertySig.GetRetType().FullName })
            .OrderBy(p => p.Name).ToList(),
        StructLayout = !type.IsEnum ? GetStructLayout(type) : null,
        Fields = !type.IsEnum ? GetFields(type) : null
    };

    private static string ConvertWslPath(string path) => 
        OperatingSystem.IsWindows() && path.StartsWith("/mnt/") && path.Length > 6 
            ? $"{char.ToUpper(path[5])}:{path[6..].Replace('/', '\\')}" 
            : path;

    private static string GetTypeKind(TypeDef type) => type switch
    {
        _ when type.IsEnum => "enum",
        _ when type.IsPrimitive => "primitive", 
        _ when type.BaseType?.FullName == "System.MulticastDelegate" => "delegate",
        _ when type.IsValueType => "struct",
        _ when type.IsInterface => "interface",
        _ when type.IsAbstract && type.IsSealed => "static class",
        _ when type.IsAbstract => "abstract class",
        _ when type.IsSealed => "sealed class",
        _ when type.IsClass => "class",
        _ => "other"
    };

    private static List<EnumValueMetadata>? GetEnumValues(TypeDef type) =>
        type.IsEnum ? type.Fields.Where(f => f.IsPublic && f.IsStatic && f.IsLiteral)
            .Select(f => new EnumValueMetadata { Name = f.Name.String, Value = f.Constant.Value?.ToString() }).ToList() 
            : null;

    private static StructLayoutMetadata? GetStructLayout(TypeDef type) =>
        type.ClassLayout is null ? null : new StructLayoutMetadata
        {
            Kind = type.IsExplicitLayout ? "Explicit" : type.IsSequentialLayout ? "Sequential" : "Auto",
            Pack = type.ClassLayout.PackingSize,
            Size = (int)type.ClassLayout.ClassSize
        };

    private static List<FieldMetadata>? GetFields(TypeDef type)
    {
        if (type.IsEnum) return null;
        var fields = type.Fields.Where(f => !f.IsStatic && !f.CustomAttributes.Any(a => a.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
            .Select(f => new FieldMetadata { Name = f.Name, Type = f.FieldType.FullName, Offset = (int?)f.FieldOffset })
            .OrderBy(f => f.Name).ToList();
        return fields.Any() ? fields : null;
    }
}
