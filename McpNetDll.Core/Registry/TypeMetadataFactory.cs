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
                Type = p.PropertySig.GetRetType().FullName
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
            .Where(f => !f.IsStatic && !f.CustomAttributes
                .Any(a => a.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
            .Select(f => new FieldMetadata
            {
                Name = f.Name,
                Type = f.FieldType.FullName,
                Offset = (int?)f.FieldOffset
            })
            .Where(fm => IdentifierMeaningFilter.HasMeaningfulName(fm.Name))
            .OrderBy(f => f.Name)
            .ToList();
            
        return fields.Any() ? fields : null;
    }
}


