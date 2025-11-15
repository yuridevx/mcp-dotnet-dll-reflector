using System.Text.RegularExpressions;
using FluentAssertions;
using McpNetDll.Helpers;
using McpNetDll.Registry;
using McpNetDll.Repository;
using NSubstitute;

namespace McpNetDll.Tests.Helpers;

public class AiConsumptionFormatterTests
{
    private readonly AiConsumptionFormatter _formatter = new();
    private readonly ITypeRegistry _registry = Substitute.For<ITypeRegistry>();

    [Fact]
    public void FormatNamespaceResponse_WithError_ReturnsErrorMessage()
    {
        // Arrange
        var result = new NamespaceQueryResult
        {
            Error = "Test error occurred"
        };

        // Act
        var output = _formatter.FormatNamespaceResponse(result, _registry);

        // Assert
        output.Should().Be("ERROR: Test error occurred");
    }

    [Fact]
    public void FormatNamespaceResponse_WithNamespaces_ReturnsCompactFormat()
    {
        // Arrange
        var result = new NamespaceQueryResult
        {
            Namespaces = new List<NamespaceMetadata>
            {
                new()
                {
                    Name = "System.Collections",
                    TypeCount = 25,
                    Types = new List<TypeMetadata>
                    {
                        new() { Name = "List", Namespace = "System.Collections", TypeKind = "Class", MethodCount = 10, PropertyCount = 5 },
                        new() { Name = "Dictionary", Namespace = "System.Collections", TypeKind = "Class", MethodCount = 15, PropertyCount = 8 }
                    }
                }
            },
            Pagination = new PaginationInfo { Total = 1, Offset = 0, Limit = 50 }
        };

        // Act
        var output = _formatter.FormatNamespaceResponse(result, _registry);

        // Assert
        output.Should().Contain("// 1 namespaces found");
        output.Should().Contain("namespace System.Collections // 25 types");
        output.Should().Contain("class List (10m, 5p)");
        output.Should().Contain("class Dictionary (15m, 8p)");
    }

    [Fact]
    public void FormatTypeDetailsResponse_WithTypeDetails_ReturnsCodeFormat()
    {
        // Arrange
        var result = new TypeDetailsQueryResult
        {
            Types = new List<TypeMetadata>
            {
                new()
                {
                    Name = "TestClass",
                    Namespace = "Test.Namespace",
                    TypeKind = "Class",
                    Documentation = "A test class for unit testing",
                    Methods = new List<MethodMetadata>
                    {
                        new()
                        {
                            Name = "GetValue",
                            ReturnType = "string",
                            Documentation = "Gets a value",
                            IsStatic = false,
                            Parameters = new List<ParameterMetadata>
                            {
                                new() { Name = "id", Type = "int" }
                            }
                        }
                    },
                    Properties = new List<PropertyMetadata>
                    {
                        new()
                        {
                            Name = "Name",
                            Type = "string",
                            Documentation = "Gets or sets the name",
                            IsStatic = false
                        }
                    }
                }
            }
        };

        // Act
        var output = _formatter.FormatTypeDetailsResponse(result, _registry);

        // Assert
        output.Should().Contain("/// A test class for unit testing");
        output.Should().Contain("namespace Test.Namespace;");
        output.Should().Contain("public class TestClass");
        output.Should().Contain("/// Gets or sets the name");
        output.Should().Contain("public string Name { get; set; }");
        output.Should().Contain("/// Gets a value");
        output.Should().Contain("public string GetValue(int id);");
    }

    [Fact]
    public void FormatTypeDetailsResponse_WithEnumType_RendersEnumValues()
    {
        // Arrange
        var result = new TypeDetailsQueryResult
        {
            Types = new List<TypeMetadata>
            {
                new()
                {
                    Name = "Color",
                    Namespace = "Test.Enums",
                    TypeKind = "Enum",
                    EnumValues = new List<EnumValueMetadata>
                    {
                        new() { Name = "Red", Value = "0" },
                        new() { Name = "Green", Value = "1" },
                        new() { Name = "Blue", Value = "2" }
                    }
                }
            }
        };

        // Act
        var output = _formatter.FormatTypeDetailsResponse(result, _registry);

        // Assert
        output.Should().Contain("public enum Color");
        output.Should().Contain("Red = 0,");
        output.Should().Contain("Green = 1,");
        output.Should().Contain("Blue = 2,");
    }

    [Fact]
    public void FormatTypeDetailsResponse_WithStaticMembers_ShowsStaticModifier()
    {
        // Arrange
        var result = new TypeDetailsQueryResult
        {
            Types = new List<TypeMetadata>
            {
                new()
                {
                    Name = "Helper",
                    Namespace = "Utils",
                    TypeKind = "Class",
                    Methods = new List<MethodMetadata>
                    {
                        new()
                        {
                            Name = "StaticMethod",
                            ReturnType = "void",
                            IsStatic = true,
                            Parameters = new List<ParameterMetadata>()
                        }
                    },
                    Properties = new List<PropertyMetadata>
                    {
                        new()
                        {
                            Name = "Instance",
                            Type = "Helper",
                            IsStatic = true
                        }
                    },
                    Fields = new List<FieldMetadata>
                    {
                        new()
                        {
                            Name = "_counter",
                            Type = "int",
                            IsStatic = true
                        }
                    }
                }
            }
        };

        // Act
        var output = _formatter.FormatTypeDetailsResponse(result, _registry);

        // Assert
        output.Should().Contain("public static int _counter;");
        output.Should().Contain("public static Helper Instance { get; set; }");
        output.Should().Contain("public static void StaticMethod();");
    }

    [Fact]
    public void FormatSearchResponse_GroupsByElementType()
    {
        // Arrange
        var result = new SearchQueryResult
        {
            Results = new List<SearchResult>
            {
                new() { ElementType = "Type", Name = "String", FullName = "System.String", Namespace = "System" },
                new() { ElementType = "Type", Name = "Int32", FullName = "System.Int32", Namespace = "System" },
                new() { ElementType = "Method", Name = "Format", FullName = "System.String.Format", ParentType = "String" },
                new() { ElementType = "Property", Name = "Length", FullName = "System.String.Length", ParentType = "String" }
            },
            Pagination = new PaginationInfo { Total = 4, Offset = 0, Limit = 100 }
        };

        // Act
        var output = _formatter.FormatSearchResponse(result, _registry);

        // Assert
        output.Should().Contain("// 4 matches found");
        output.Should().Contain("// Type (2):");
        output.Should().Contain("// Method (1):");
        output.Should().Contain("// Property (1):");
        output.Should().Contain("System.String");
    }

    [Fact]
    public void FormatTypeDetailsResponse_WithStructLayout_ShowsLayoutInfo()
    {
        // Arrange
        var result = new TypeDetailsQueryResult
        {
            Types = new List<TypeMetadata>
            {
                new()
                {
                    Name = "NativeStruct",
                    Namespace = "Interop",
                    TypeKind = "Struct",
                    StructLayout = new StructLayoutMetadata
                    {
                        Kind = "Sequential",
                        Pack = 4,
                        Size = 16
                    },
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "X", Type = "int", Offset = 0 },
                        new() { Name = "Y", Type = "int", Offset = 4 }
                    }
                }
            }
        };

        // Act
        var output = _formatter.FormatTypeDetailsResponse(result, _registry);

        // Assert
        output.Should().Contain("// Layout: Sequential, Pack: 4, Size: 16");
        output.Should().Contain("public int X; // Offset: 0");
        output.Should().Contain("public int Y; // Offset: 4");
    }

    [Fact]
    public void TruncatesLongDocumentation()
    {
        // Arrange
        var longDoc = string.Join(" ", Enumerable.Repeat("word", 50));
        var result = new TypeDetailsQueryResult
        {
            Types = new List<TypeMetadata>
            {
                new()
                {
                    Name = "TestClass",
                    Namespace = "Test",
                    TypeKind = "Class",
                    Properties = new List<PropertyMetadata>
                    {
                        new()
                        {
                            Name = "LongDocProp",
                            Type = "string",
                            Documentation = longDoc
                        }
                    }
                }
            }
        };

        // Act
        var output = _formatter.FormatTypeDetailsResponse(result, _registry);

        // Assert
        output.Should().Contain("...");
        output.Should().Match(s => Regex.Matches(s, @"///.*\.\.\.", RegexOptions.Multiline).Count == 1);
    }
}