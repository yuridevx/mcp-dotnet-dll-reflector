using dnlib.DotNet;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace McpNetDll;

public class Extractor
{
    // New public API
    public string ListNamespaces(string assemblyPath, string[]? namespaces = null)
    {
        var result = LoadAndProcessAssembly(assemblyPath, allTypes =>
        {
            IEnumerable<TypeDef> typesToProcess = allTypes;
            if (namespaces != null && namespaces.Length > 0)
            {
                var availableNamespaces = allTypes.Select(t => t.Namespace.String).Distinct().ToList();
                var notFoundNamespaces = namespaces.Where(ns => !availableNamespaces.Contains(ns)).ToList();
                if (notFoundNamespaces.Any())
                {
                    return (object)new {
                        error = $"Namespace(s) not found: {string.Join(", ", notFoundNamespaces)}",
                        availableNamespaces = availableNamespaces.OrderBy(ns => ns).ToList()
                    };
                }
                typesToProcess = allTypes.Where(t => namespaces.Contains(t.Namespace.String));
            }

            var namespaceInfo = typesToProcess
                .GroupBy(t => t.Namespace.String)
                .Select(g => new NamespaceMetadata
                {
                    Name = g.Key,
                    TypeCount = g.Count(),
                    Types = g.Select(type => ExtractTypeMetadata(type, withMemberDetails: false))
                               .OrderBy(t => t.Name).ToList()
                })
                .OrderBy(ns => ns.Name)
                .ToList();

            return new { Namespaces = namespaceInfo };
        });

        return SerializeResult(result);
    }

    public string GetTypeDetails(string assemblyPath, string[] typeNames)
    {
        if (typeNames == null || typeNames.Length == 0)
        {
            return SerializeResult(new { error = "TypeNames array cannot be empty." });
        }

        var result = LoadAndProcessAssembly(assemblyPath, allTypes =>
        {
            var typeMap = allTypes.GroupBy(t => t.FullName.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
            var simpleNameMap = allTypes.GroupBy(t => t.Name.String.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.ToList());

            var typesToExtract = new HashSet<TypeDef>();
            var notFoundTypes = new List<string>();

            foreach (var name in typeNames)
            {
                var nameLower = name.ToLowerInvariant();
                if (typeMap.TryGetValue(nameLower, out var foundType))
                {
                    typesToExtract.Add(foundType);
                }
                else if (simpleNameMap.TryGetValue(nameLower, out var foundTypes) && foundTypes.Count == 1)
                {
                    typesToExtract.Add(foundTypes.First());
                }
                else
                {
                    notFoundTypes.Add(name);
                }
            }

            if (notFoundTypes.Any())
            {
                return (object)new {
                    error = $"Type(s) not found or ambiguous: {string.Join(", ", notFoundTypes)}",
                    availableTypes = allTypes.Select(t => t.FullName).OrderBy(n => n).ToList()
                };
            }

            var filteredTypes = typesToExtract
                .Select(type => ExtractTypeMetadata(type, withMemberDetails: true))
                .OrderBy(t => t.FullName)
                .ToList();

            return new { Types = filteredTypes };
        });

        return SerializeResult(result);
    }
    
    // Private helpers
    private object LoadAndProcessAssembly(string assemblyPath, Func<List<TypeDef>, object> processor)
    {
        if (string.IsNullOrEmpty(assemblyPath))
        {
            return new { error = "Assembly path cannot be null or empty." };
        }
        
        assemblyPath = ConvertWslPathToWindowsPath(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return new { error = "Assembly file not found." };
        }

        try
        {
            var module = ModuleDefMD.Load(assemblyPath);
            var publicTypes = module.Types
                .Where(t => t.IsPublic)
                .ToList();
            return processor(publicTypes);
        }
        catch (Exception ex)
        {
            return new { error = $"Failed to load assembly: {ex.Message}" };
        }
    }

    private TypeMetadata ExtractTypeMetadata(TypeDef type, bool withMemberDetails)
    {
        return new TypeMetadata
        {
            Name = type.Name.String,
            Namespace = type.Namespace.String,
            FullName = type.FullName,
            TypeKind = GetTypeKind(type),
            MethodCount = type.Methods.Count(m => m.IsPublic && !m.IsSpecialName),
            PropertyCount = type.Properties.Count(p => p.GetMethod?.IsPublic ?? p.SetMethod?.IsPublic ?? false),
            EnumValues = type.IsEnum ? GetEnumValues(type) : null,
            Methods = withMemberDetails
                ? type.Methods
                    .Where(m => m.IsPublic && !m.IsSpecialName)
                    .Select(m => new MethodMetadata
                    {
                        Name = m.Name,
                        ReturnType = m.ReturnType.FullName,
                        Parameters = (m.HasThis ? m.Parameters.Skip(1) : m.Parameters)
                            .Select(p => new ParameterMetadata
                            {
                                Name = p.Name,
                                Type = p.Type.FullName
                            }).ToList()
                    })
                    .OrderBy(m => m.Name).ToList()
                : null,
            Properties = withMemberDetails
                ? type.Properties
                    .Where(p => p.GetMethod?.IsPublic ?? p.SetMethod?.IsPublic ?? false)
                    .Select(p => new PropertyMetadata
                    {
                        Name = p.Name,
                        Type = p.PropertySig.GetRetType().FullName
                    })
                    .OrderBy(p => p.Name).ToList()
                : null,
            StructLayout = withMemberDetails && !type.IsEnum ? GetStructLayout(type) : null,
            Fields = withMemberDetails && !type.IsEnum ? GetFields(type) : null
        };
    }
    
    private string SerializeResult(object result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
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
        if (type.IsPrimitive) return "Primitive";
        if (type.BaseType is { FullName: "System.MulticastDelegate" }) return "Delegate";
        if (type.IsValueType) return "Struct";
        if (type.IsInterface) return "Interface";
        if (type.IsAbstract && type.IsSealed) return "Static Class";
        if (type.IsAbstract) return "Abstract Class";
        if (type.IsSealed) return "Sealed Class";
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

    private StructLayoutMetadata? GetStructLayout(TypeDef type)
    {
        if (type.ClassLayout is null) return null;

        var layout = type.ClassLayout;
        var kindName = type.IsExplicitLayout ? "Explicit" : type.IsSequentialLayout ? "Sequential" : "Auto";

        return new StructLayoutMetadata
        {
            Kind = kindName,
            Pack = layout.PackingSize,
            Size = (int)layout.ClassSize
        };
    }

    private List<FieldMetadata>? GetFields(TypeDef type)
    {
        if (type.IsEnum) return null;

        var fields = new List<FieldMetadata>();
        if (type.IsValueType || type.IsClass)
        {
            fields.AddRange(type.Fields
                .Where(f => !f.IsStatic && !f.CustomAttributes.Any(a => a.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                .Select(f => new FieldMetadata
                {
                    Name = f.Name,
                    Type = f.FieldType.FullName,
                    Offset = (int?)f.FieldOffset
                }));
        }

        return fields.Any() ? fields.OrderBy(f => f.Name).ToList() : null;
    }
}
