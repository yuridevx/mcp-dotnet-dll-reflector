namespace McpNetDll;

public class AssemblyMetadata
{
    public required string Name { get; init; }
    public required List<TypeMetadata> Types { get; init; }
}

public class NamespaceMetadata
{
    public required string Name { get; init; }
    public required int TypeCount { get; init; }
    public required List<TypeMetadata> Types { get; init; }
}

public class TypeMetadata
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string TypeKind { get; init; }
    public int? MethodCount { get; init; }
    public int? PropertyCount { get; init; }
    public int? FieldCount { get; init; }
    public List<MethodMetadata>? Methods { get; init; }
    public List<PropertyMetadata>? Properties { get; init; }
    public List<EnumValueMetadata>? EnumValues { get; init; }
    public StructLayoutMetadata? StructLayout { get; init; }
    public List<FieldMetadata>? Fields { get; init; }
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

public class FieldMetadata
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public int? Offset { get; init; }
}

public class StructLayoutMetadata
{
    public required string Kind { get; init; }
    public int? Pack { get; init; }
    public int? Size { get; init; }
}

