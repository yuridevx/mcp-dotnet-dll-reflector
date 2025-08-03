using dnlib.DotNet;
using System.Text.Json;
using System;

namespace McpNetDll;

public class Extractor
{
    // New public API
    public string ListNamespaces(string assemblyPath)
    {
        var allTypes = LoadPublicTypes(assemblyPath, out var error);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error });
        }

        var namespaceInfo = allTypes
            .GroupBy(t => t.Namespace.String)
            .Select(g => new NamespaceMetadata
            {
                Name = g.Key,
                TypeCount = g.Count(),
                TypeNames = g.Select(t => t.Name.String).ToList()
            })
            .OrderBy(ns => ns.Name)
            .ToList();

        return JsonSerializer.Serialize(new { Namespaces = namespaceInfo },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }

    public string ListTypesInNamespaces(string assemblyPath, string[] namespaces)
    {
        if (namespaces == null || namespaces.Length == 0)
        {
            return JsonSerializer.Serialize(new { error = "Namespaces array cannot be empty." });
        }
        
        var allTypes = LoadPublicTypes(assemblyPath, out var error);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error });
        }

        var availableNamespaces = allTypes.Select(t => t.Namespace.String).Distinct().ToList();
        var notFoundNamespaces = namespaces.Where(ns => !availableNamespaces.Contains(ns)).ToList();
        
        if (notFoundNamespaces.Any())
        {
            return JsonSerializer.Serialize(new {
                error = $"Namespace(s) not found: {string.Join(", ", notFoundNamespaces)}",
                availableNamespaces = availableNamespaces.OrderBy(ns => ns).ToList()
            });
        }

        var filteredTypes = allTypes
            .Where(t => namespaces.Contains(t.Namespace.String))
            .Select(type => new TypeMetadata
            {
                Name = type.Name.String,
                Namespace = type.Namespace.String,
                FullName = type.FullName,
                TypeKind = GetTypeKind(type),
                MethodCount = type.Methods.Count(m => m.IsPublic && !m.IsConstructor && !m.IsGetter && !m.IsSetter),
                PropertyCount = type.Properties.Count(p => p.GetMethods.Any(gm => gm?.IsPublic ?? false)),
                Methods = null,
                Properties = null,
                EnumValues = type.IsEnum ? GetEnumValues(type) : null
            })
            .OrderBy(t => t.Namespace)
            .ThenBy(t => t.Name)
            .ToList();

        return JsonSerializer.Serialize(new { Types = filteredTypes },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }

    public string GetTypeDetails(string assemblyPath, string[] typeNames)
    {
        if (typeNames == null || typeNames.Length == 0)
        {
            return JsonSerializer.Serialize(new { error = "TypeNames array cannot be empty." });
        }

        var allTypes = LoadPublicTypes(assemblyPath, out var error);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error });
        }
        
        var notFoundTypes = new List<string>();
        var typesToExtract = new List<TypeDef>();

        foreach (var nameToFind in typeNames)
        {
            var foundTypes = allTypes.Where(t =>
                t.FullName.Equals(nameToFind, StringComparison.OrdinalIgnoreCase) ||
                t.Name.String.Equals(nameToFind, StringComparison.OrdinalIgnoreCase)).ToList();

            if (foundTypes.Any())
            {
                typesToExtract.AddRange(foundTypes);
            }
            else
            {
                notFoundTypes.Add(nameToFind);
            }
        }
        
        typesToExtract = typesToExtract.Distinct().ToList();

        if (notFoundTypes.Any())
        {
            return JsonSerializer.Serialize(new {
                error = $"Type(s) not found: {string.Join(", ", notFoundTypes)}",
                availableTypes = allTypes.Select(t => t.FullName).OrderBy(cn => cn).ToList()
            });
        }

        var filteredTypes = typesToExtract
            .Select(type => new TypeMetadata
            {
                Name = type.Name.String,
                Namespace = type.Namespace.String,
                FullName = type.FullName,
                TypeKind = GetTypeKind(type),
                MethodCount = type.Methods.Count(m => m.IsPublic && !m.IsConstructor && !m.IsGetter && !m.IsSetter),
                PropertyCount = type.Properties.Count(p => p.GetMethods.Any(gm => gm?.IsPublic ?? false)),
                Methods = type.Methods
                   .Where(m => m.IsPublic && !m.IsConstructor && !m.IsGetter && !m.IsSetter && m.Parameters.Count > 0)
                   .Select(method => new MethodMetadata
                   {
                       Name = method.Name.String,
                       ReturnType = method.MethodSig.RetType.FullName,
                       Parameters = method.Parameters.Select(p => new ParameterMetadata
                       {
                           Name = p.Name,
                           Type = p.Type.FullName
                       }).ToList()
                   }).ToList(),
                Properties = type.Properties
                   .Where(p => p.GetMethods.Any(gm => gm?.IsPublic ?? false))
                   .Select(prop => new PropertyMetadata
                   {
                       Name = prop.Name.String,
                       Type = prop.PropertySig.RetType.FullName
                   }).ToList(),
                EnumValues = type.IsEnum ? GetEnumValues(type) : null
            })
            .OrderBy(t => t.FullName)
            .ToList();
            
        return JsonSerializer.Serialize(new { Types = filteredTypes },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }

    // Private helpers
    private List<TypeDef>? LoadPublicTypes(string assemblyPath, out string? error)
    {
        error = null;
        assemblyPath = ConvertWslPathToWindowsPath(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            error = "Assembly file not found.";
            return null;
        }

        try
        {
            var modCtx = ModuleDef.CreateModuleContext();
            var module = ModuleDefMD.Load(assemblyPath, modCtx);
            return module.Types
                .Where(t => t.IsPublic && (t.IsClass || t.IsInterface || t.IsEnum || t.IsValueType))
                .ToList();
        }
        catch (Exception ex)
        {
            error = $"Failed to load assembly: {ex.Message}";
            return null;
        }
    }

    private string ConvertWslPathToWindowsPath(string path)
    {
        if (OperatingSystem.IsWindows() && path.StartsWith("/mnt/") && path.Length > 6)
        {
            char driveLetter = path[5];
            string restOfPath = path.Substring(6);
            return $"{char.ToUpper(driveLetter)}:{restOfPath.Replace('/', '\\')}";
        }
        return path;
    }

    private string GetTypeKind(TypeDef type)
    {
        if (type.IsEnum) return "Enum";
        if (type.IsValueType && !type.IsPrimitive) return "Struct";
        if (type.IsInterface) return "Interface";
        if (type.IsClass) return "Class";
        return "Other";
    }

    private List<EnumValueMetadata> GetEnumValues(TypeDef type)
    {
        return type.Fields
            .Where(f => f.IsPublic && f.IsStatic && f.IsLiteral)
            .Select(f => new EnumValueMetadata
            {
                Name = f.Name.String,
                Value = f.Constant.Value?.ToString()
            })
            .ToList();
    }
}

// Data model for serialization
public class AssemblyMetadata
{
    public required string Name { get; init; }
    public required List<TypeMetadata> Types { get; init; }
}

public class NamespaceMetadata
{
    public required string Name { get; init; }
    public required int TypeCount { get; init; }
    public required List<string> TypeNames { get; init; }
}

public class TypeMetadata
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string FullName { get; init; }
    public required string TypeKind { get; init; }
    public int? MethodCount { get; init; }
    public int? PropertyCount { get; init; }
    public List<MethodMetadata>? Methods { get; init; }
    public List<PropertyMetadata>? Properties { get; init; }
    public List<EnumValueMetadata>? EnumValues { get; init; }
}

public class MethodMetadata
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public required List<ParameterMetadata> Parameters { get; init; }
}

public class PropertyMetadata
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

public class ParameterMetadata
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

public class EnumValueMetadata
{
    public required string Name { get; init; }
    public string? Value { get; init; }
}