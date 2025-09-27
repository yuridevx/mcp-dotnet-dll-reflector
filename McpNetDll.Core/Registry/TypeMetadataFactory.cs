using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using McpNetDll.Helpers;

namespace McpNetDll.Registry;

public static class TypeMetadataFactory
{
    public static TypeMetadata CreateTypeMetadata(TypeDef type)
    {
        var filteredMethods = GetMethods(type);
        var filteredProperties = GetProperties(type);
        var filteredFields = !type.IsEnum ? GetFields(type) : null;
        var filteredEnumValues = type.IsEnum ? GetEnumValues(type) : null;

        return new TypeMetadata
        {
            Name = type.Name.String,
            Namespace = type.Namespace.String,
            TypeKind = GetTypeKind(type),
            Documentation = GetDocumentation(type.CustomAttributes),
            MethodCount = filteredMethods.Count,
            PropertyCount = filteredProperties.Count,
            FieldCount = filteredFields?.Count,
            EnumValues = filteredEnumValues,
            Methods = filteredMethods,
            Properties = filteredProperties,
            StructLayout = !type.IsEnum ? GetStructLayout(type) : null,
            Fields = filteredFields
        };
    }

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

    private static List<MethodMetadata> GetMethods(TypeDef type)
    {
        return type.Methods
            .Where(m => m.IsPublic && !m.IsSpecialName)
            .Select(m => new MethodMetadata
            {
                Name = m.Name,
                ReturnType = m.ReturnType.FullName,
                IsStatic = m.IsStatic,
                Documentation = GetDocumentation(m.CustomAttributes),
                Parameters = (m.HasThis ? m.Parameters.Skip(1) : m.Parameters)
                    .Select(p => new ParameterMetadata
                    {
                        Name = p.Name,
                        Type = p.Type.FullName
                    }).ToList()
            })
            .Where(mm => IdentifierMeaningFilter.HasMeaningfulName(mm.Name))
            .OrderBy(m => m.Name)
            .ToList();
    }

    private static List<PropertyMetadata> GetProperties(TypeDef type)
    {
        return type.Properties
            .Where(p => p.GetMethod?.IsPublic ?? p.SetMethod?.IsPublic ?? false)
            .Select(p => new PropertyMetadata
            {
                Name = p.Name,
                Type = p.PropertySig.GetRetType().FullName,
                Documentation = GetDocumentation(p.CustomAttributes),
                IsStatic = (p.GetMethod?.IsStatic ?? false) || (p.SetMethod?.IsStatic ?? false)
            })
            .Where(pm => IdentifierMeaningFilter.HasMeaningfulName(pm.Name))
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static List<EnumValueMetadata>? GetEnumValues(TypeDef type)
    {
        return type.IsEnum
            ? type.Fields
                .Where(f => f.IsPublic && f.IsStatic && f.IsLiteral)
                .Select(f => new EnumValueMetadata
                {
                    Name = f.Name.String,
                    Value = f.Constant.Value?.ToString()
                })
                .Where(ev => IdentifierMeaningFilter.HasMeaningfulName(ev.Name))
                .ToList()
            : null;
    }

    private static StructLayoutMetadata? GetStructLayout(TypeDef type)
    {
        return type.ClassLayout is null 
            ? null 
            : new StructLayoutMetadata
            {
                Kind = type.IsExplicitLayout ? "Explicit" : type.IsSequentialLayout ? "Sequential" : "Auto",
                Pack = type.ClassLayout.PackingSize,
                Size = (int)type.ClassLayout.ClassSize
            };
    }

    private static List<FieldMetadata>? GetFields(TypeDef type)
    {
        if (type.IsEnum) return null;
        
        var fields = type.Fields
            .Where(f => !f.CustomAttributes
                .Any(a => a.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
            .Select(f => new FieldMetadata
            {
                Name = f.Name,
                Type = f.FieldType.FullName,
                Offset = (int?)f.FieldOffset,
                IsStatic = f.IsStatic,
                Documentation = GetDocumentation(f.CustomAttributes)
            })
            .Where(fm => IdentifierMeaningFilter.HasMeaningfulName(fm.Name))
            .OrderBy(f => f.Name)
            .ToList();
            
        return fields.Any() ? fields : null;
    }

    private static string? GetDocumentation(dnlib.DotNet.CustomAttributeCollection attributes)
    {
        // Prefer Description, Display(Description/Name), then Obsolete message, then any single-string custom attribute
        foreach (var attr in attributes)
        {
            var fullName = attr.AttributeType.FullName;
            if (fullName == "System.ComponentModel.DescriptionAttribute")
            {
                var doc = GetCtorStringArg(attr) ?? GetNamedString(attr, "Description");
                if (!string.IsNullOrWhiteSpace(doc)) return doc;
            }
            else if (fullName == "System.ComponentModel.DisplayNameAttribute")
            {
                var doc = GetCtorStringArg(attr) ?? GetNamedString(attr, "DisplayName");
                if (!string.IsNullOrWhiteSpace(doc)) return doc;
            }
            else if (fullName == "System.ComponentModel.DataAnnotations.DisplayAttribute")
            {
                var doc = GetNamedString(attr, "Description") ?? GetNamedString(attr, "Name") ?? GetCtorStringArg(attr);
                if (!string.IsNullOrWhiteSpace(doc)) return doc;
            }
            else if (fullName == "System.ObsoleteAttribute")
            {
                var doc = GetCtorStringArg(attr);
                if (!string.IsNullOrWhiteSpace(doc)) return doc;
            }
            else if (fullName.EndsWith(".SummaryAttribute", System.StringComparison.Ordinal) ||
                     fullName.EndsWith(".CommentAttribute", System.StringComparison.Ordinal) ||
                     fullName.EndsWith(".DescriptionAttribute", System.StringComparison.Ordinal))
            {
                var doc = GetCtorStringArg(attr) ?? GetAnyNamedString(attr);
                if (!string.IsNullOrWhiteSpace(doc)) return doc;
            }
        }

        // Fallback: first attribute with a single string ctor/named value
        foreach (var attr in attributes)
        {
            var doc = GetCtorStringArg(attr) ?? GetAnyNamedString(attr);
            if (!string.IsNullOrWhiteSpace(doc)) return doc;
        }

        return null;
    }

    private static string? GetCtorStringArg(dnlib.DotNet.CustomAttribute attr)
    {
        if (attr.ConstructorArguments.Count > 0)
        {
            var arg = attr.ConstructorArguments[0].Value as string;
            if (!string.IsNullOrWhiteSpace(arg)) return arg;
        }
        return null;
    }

    private static string? GetNamedString(dnlib.DotNet.CustomAttribute attr, string name)
    {
        foreach (var p in attr.Properties)
        {
            if (p.Name == name)
            {
                var v = p.Argument.Value as string;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        foreach (var f in attr.Fields)
        {
            if (f.Name == name)
            {
                var v = f.Argument.Value as string;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        return null;
    }

    private static string? GetAnyNamedString(dnlib.DotNet.CustomAttribute attr)
    {
        foreach (var p in attr.Properties)
        {
            var v = p.Argument.Value as string;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        foreach (var f in attr.Fields)
        {
            var v = f.Argument.Value as string;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }
}


