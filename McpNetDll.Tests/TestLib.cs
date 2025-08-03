using Xunit;
using McpNetDll;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;

namespace McpNetDll.Tests;

public class ExtractorTests
{
    private readonly string _testDllPath;
    private readonly Extractor _extractor;

    public ExtractorTests()
    {
        // Adjust the path to be relative to the test project's output directory
        // Assuming MyTestLibrary.dll is in the same output directory as the test assembly
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
       _testDllPath = Path.Combine(assemblyLocation ?? "", "MyTestLibrary.dll");

       _extractor = new Extractor();
    }

    [Fact]
    public void ListNamespaces_ShouldReturnNamespaceInfo_WhenNoFiltersAreApplied()
    {
        // Arrange & Act
        var resultJson = _extractor.ListNamespaces(_testDllPath);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Namespaces", out var namespaces));
        Assert.Single(namespaces.EnumerateArray()); // Still only one namespace, 'MyTestLibrary'.

        var myTestLibraryNamespace = namespaces.EnumerateArray().First(ns => ns.GetProperty("Name").GetString() == "MyTestLibrary");
        Assert.Equal(6, myTestLibraryNamespace.GetProperty("TypeCount").GetInt32()); // MyPublicClass, IMyInterface, MyEnum, MyStruct, MyGenericClass`1, MyExplicitStruct
        Assert.True(myTestLibraryNamespace.TryGetProperty("Types", out var types));
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "IMyInterface");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyEnum");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyStruct");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyGenericClass`1");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyExplicitStruct");
    }

    [Fact]
    public void ListNamespaces_ShouldReturnFilteredTypeInfo_WhenFilteredByNamespace()
    {
        // Arrange
        string[] namespacesFilter = { "MyTestLibrary" };

        // Act
        var resultJson = _extractor.ListNamespaces(_testDllPath, namespacesFilter);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Namespaces", out var namespaces));
        Assert.Single(namespaces.EnumerateArray());

        var myTestLibraryNamespace = namespaces.EnumerateArray().First();
        Assert.True(myTestLibraryNamespace.TryGetProperty("Types", out var types));
        Assert.Equal(6, types.EnumerateArray().Count());

        var myPublicClass = types.EnumerateArray().First(t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.Equal("MyTestLibrary", myPublicClass.GetProperty("Namespace").GetString());
        Assert.Equal("MyTestLibrary.MyPublicClass", myPublicClass.GetProperty("FullName").GetString());
        Assert.True(myPublicClass.GetProperty("MethodCount").GetInt32() > 0);
        Assert.True(myPublicClass.GetProperty("PropertyCount").GetInt32() > 0);
        Assert.False(myPublicClass.TryGetProperty("Methods", out _));
        Assert.False(myPublicClass.TryGetProperty("Properties", out _));
    }

    [Fact]
    public void Extractor_ShouldNotIncludeDocumentationInfo()
    {
        // Act
        var resultJsonNamespace = _extractor.ListNamespaces(_testDllPath);
        var resultJsonClass = _extractor.ListNamespaces(_testDllPath, new[] { "MyTestLibrary" });
        var resultJsonMember = _extractor.GetTypeDetails(_testDllPath, new[] { "MyTestLibrary.MyPublicClass" });

        var resultNamespace = JsonSerializer.Deserialize<JsonElement>(resultJsonNamespace);
        var resultClass = JsonSerializer.Deserialize<JsonElement>(resultJsonClass);
        var resultMember = JsonSerializer.Deserialize<JsonElement>(resultJsonMember);

        // Assert for Namespace data (no 'Documentation' field expected)
        Assert.True(resultNamespace.TryGetProperty("Namespaces", out var namespaces));
        var myTestLibraryNamespace = namespaces.EnumerateArray().First(ns => ns.GetProperty("Name").GetString() == "MyTestLibrary");
        Assert.False(myTestLibraryNamespace.TryGetProperty("Documentation", out _));

        // Assert for Class data (no 'Documentation' field expected)
        Assert.True(resultClass.TryGetProperty("Namespaces", out var namespacesClass));
        var myTestLibraryNamespaceForClasses = namespacesClass.EnumerateArray().First();
        Assert.True(myTestLibraryNamespaceForClasses.TryGetProperty("Types", out var typesClass));
        var myPublicClassForClasses = typesClass.EnumerateArray().First(t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.False(myPublicClassForClasses.TryGetProperty("Documentation", out _));

        // Assert for Member data (no 'Documentation' field expected for TypeMetadata, MethodMetadata, PropertyMetadata)
        Assert.True(resultMember.TryGetProperty("Types", out var typesMember));
        var myPublicClassForMembers = typesMember.EnumerateArray().First(t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.False(myPublicClassForMembers.TryGetProperty("Documentation", out _));

        Assert.True(myPublicClassForMembers.TryGetProperty("Methods", out var methods));
        var firstMethod = methods.EnumerateArray().FirstOrDefault();
        if (firstMethod.ValueKind != JsonValueKind.Undefined)
        {
            Assert.False(firstMethod.TryGetProperty("Documentation", out _));
        }

        Assert.True(myPublicClassForMembers.TryGetProperty("Properties", out var properties));
        var firstProperty = properties.EnumerateArray().FirstOrDefault();
        if (firstProperty.ValueKind != JsonValueKind.Undefined)
        {
            Assert.False(firstProperty.TryGetProperty("Documentation", out _));
        }
    }

    [Fact]
    public void GetTypeDetails_ShouldReturnMemberInfo_WhenFilteredByClassName()
    {
        // Arrange
        string[] classNames = { "MyTestLibrary.MyPublicClass" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, classNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Types", out var types));
        Assert.Single(types.EnumerateArray());

        var myPublicClass = types.EnumerateArray().First(t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.True(myPublicClass.TryGetProperty("Methods", out var methods)); // Should contain detailed methods
        Assert.True(myPublicClass.TryGetProperty("Properties", out var properties)); // Should contain detailed properties
        Assert.Contains(methods.EnumerateArray(), m => m.GetProperty("Name").GetString() == "GetInstanceMessage");
        Assert.Contains(properties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "InstanceProperty");
    }

    [Fact]
    public void GetTypeDetails_ShouldReturnMemberInfo_WhenFilteredBySimpleClassName()
    {
        // Arrange
        string[] classNames = { "MyPublicClass" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, classNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Types", out var types));
        Assert.Single(types.EnumerateArray());

        var myPublicClass = types.EnumerateArray().First(t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.True(myPublicClass.TryGetProperty("Methods", out var methods)); // Should contain detailed methods
        Assert.True(myPublicClass.TryGetProperty("Properties", out var properties)); // Should contain detailed properties
        Assert.Contains(methods.EnumerateArray(), m => m.GetProperty("Name").GetString() == "GetInstanceMessage");
        Assert.Contains(properties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "InstanceProperty");
    }

    [Fact]
    public void Extractor_ShouldReturnError_WhenAssemblyNotFound()
    {
        // Arrange
        var nonExistentPath = "C:\\NonExistent\\Assembly.dll";

        // Act
        var resultJson = _extractor.ListNamespaces(nonExistentPath);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("Assembly file not found.", error.GetString());
    }


    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenEmptyClassNamesArray()
    {
        // Arrange
        string[] classNames = { };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, classNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("TypeNames array cannot be empty.", error.GetString());
    }
    
    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenInvalidClassNameFormat()
    {
        // Arrange
        // The check for class name format (containing '.') is removed from ExtractMemberInfo
        // So this test case is no longer directly applicable. The new logic handles simple names.
        // Instead, we will test for a truly non-existent simple class name.
        string[] classNames = { "NonExistentClassSimple" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, classNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("Type(s) not found", error.GetString());
    }

    [Fact]
    public void ListNamespaces_ShouldReturnError_WhenNamespaceNotFound()
    {
        // Arrange
        string[] namespaces = { "NonExistentNamespace" };

        // Act
        var resultJson = _extractor.ListNamespaces(_testDllPath, namespaces);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("Namespace(s) not found: NonExistentNamespace", error.GetString());
    }

    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenClassNotFound()
    {
        // Arrange
        string[] classNames = { "MyTestLibrary.NonExistentClass" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, classNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("Type(s) not found or ambiguous: MyTestLibrary.NonExistentClass", error.GetString());
    }
    [Fact]
    public void Extractor_ShouldIncludeEnumsAndStructsInNamespaceInfo()
    {
        // Arrange & Act
        var resultJson = _extractor.ListNamespaces(_testDllPath);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Namespaces", out var namespaces));
        var myTestLibraryNamespace = namespaces.EnumerateArray().First(ns => ns.GetProperty("Name").GetString() == "MyTestLibrary");
        Assert.True(myTestLibraryNamespace.TryGetProperty("Types", out var types));
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyEnum");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyStruct");
    }

    [Fact]
    public void Extractor_ShouldIncludeTypeKindInMetadata()
    {
        // Arrange & Act
        string[] filter = { "MyTestLibrary" };
        var resultJson = _extractor.ListNamespaces(_testDllPath, filter);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Namespaces", out var namespaces));
        var myTestLibraryNamespace = namespaces.EnumerateArray().First();
        Assert.True(myTestLibraryNamespace.TryGetProperty("Types", out var types));

        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyPublicClass" && t.GetProperty("TypeKind").GetString() == "class");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "IMyInterface" && t.GetProperty("TypeKind").GetString() == "interface");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyEnum" && t.GetProperty("TypeKind").GetString() == "enum");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyStruct" && t.GetProperty("TypeKind").GetString() == "struct");
    }

    [Fact]
    public void GetTypeDetails_ShouldReturnTypeInfo_WhenFilteredBySimpleTypeName()
    {
        // Arrange
        string[] typeNames = { "MyEnum", "MyStruct", "MyGenericClass`1" }; // Note: Generic types often have `1 for one parameter.

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, typeNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Types", out var types));
        Assert.Equal(3, types.EnumerateArray().Count());

        var myEnum = types.EnumerateArray().FirstOrDefault(t => t.GetProperty("Name").GetString() == "MyEnum");
        Assert.Equal("enum", myEnum.GetProperty("TypeKind").GetString());
        Assert.True(myEnum.TryGetProperty("EnumValues", out var enumValues));
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueA" && ev.GetProperty("Value").GetString() == "0");
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueB" && ev.GetProperty("Value").GetString() == "10");
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueC" && ev.GetProperty("Value").GetString() == "11");

        var myStruct = types.EnumerateArray().FirstOrDefault(t => t.GetProperty("Name").GetString() == "MyStruct");
        Assert.Equal("struct", myStruct.GetProperty("TypeKind").GetString());
        Assert.True(myStruct.TryGetProperty("Properties", out var structProperties));
        Assert.Contains(structProperties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "Number");
        Assert.Contains(structProperties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "Text");
        Assert.True(myStruct.TryGetProperty("Methods", out var structMethods));
        Assert.Contains(structMethods.EnumerateArray(), m => m.GetProperty("Name").GetString() == "GetInfo");

        var myGenericClass = types.EnumerateArray().FirstOrDefault(t => t.GetProperty("Name").GetString() == "MyGenericClass`1");
        Assert.Equal("class", myGenericClass.GetProperty("TypeKind").GetString());
        Assert.True(myGenericClass.TryGetProperty("Properties", out var genericProperties));
        Assert.Contains(genericProperties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "Value");
        Assert.True(myGenericClass.TryGetProperty("Methods", out var genericMethods));
        Assert.Contains(genericMethods.EnumerateArray(), m => m.GetProperty("Name").GetString() == "GetValueType");
    }
    
    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenEmptyTypeNamesArray()
    {
        // Arrange
        string[] typeNames = { };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, typeNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("TypeNames array cannot be empty.", error.GetString());
    }

    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenTypeNotFound()
    {
        // Arrange
        string[] typeNames = { "NonExistentType" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, typeNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Contains("Type(s) not found or ambiguous: NonExistentType", error.GetString());
    }
    [Fact]
    public void GetTypeDetails_ShouldReturnStructLayoutAndFieldOffsets()
    {
        // Arrange
        string[] typeNames = { "MyTestLibrary.MyExplicitStruct" };

        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, typeNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("Types", out var types));
        Assert.Single(types.EnumerateArray());

        var myStruct = types.EnumerateArray().First();
        Assert.Equal("MyExplicitStruct", myStruct.GetProperty("Name").GetString());
        Assert.Equal("struct", myStruct.GetProperty("TypeKind").GetString());

        // Check StructLayout
        Assert.True(myStruct.TryGetProperty("StructLayout", out var layout));
        Assert.Equal("Explicit", layout.GetProperty("Kind").GetString());
        Assert.Equal(1, layout.GetProperty("Pack").GetInt32());
        Assert.Equal(8, layout.GetProperty("Size").GetInt32());

        // Check Fields and FieldOffsets
        Assert.True(myStruct.TryGetProperty("Fields", out var fields));
        Assert.Equal(3, fields.EnumerateArray().Count());

        var allBitsField = fields.EnumerateArray().First(f => f.GetProperty("Name").GetString() == "AllBits");
        Assert.Equal("System.Int64", allBitsField.GetProperty("Type").GetString());
        Assert.Equal(0, allBitsField.GetProperty("Offset").GetInt32());

        var int1Field = fields.EnumerateArray().First(f => f.GetProperty("Name").GetString() == "Int1");
        Assert.Equal("System.Int32", int1Field.GetProperty("Type").GetString());
        Assert.Equal(0, int1Field.GetProperty("Offset").GetInt32());

        var int2Field = fields.EnumerateArray().First(f => f.GetProperty("Name").GetString() == "Int2");
        Assert.Equal("System.Int32", int2Field.GetProperty("Type").GetString());
        Assert.Equal(4, int2Field.GetProperty("Offset").GetInt32());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ListNamespaces_ShouldReturnError_WhenPathIsNullOrEmpty(string dllPath)
    {
        // Act
        var resultJson = _extractor.ListNamespaces(dllPath);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("Assembly path cannot be null or empty.", error.GetString());
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetTypeDetails_ShouldReturnError_WhenPathIsNullOrEmpty(string dllPath)
    {
        // Arrange
        string[] typeNames = { "MyPublicClass" };

        // Act
        var resultJson = _extractor.GetTypeDetails(dllPath, typeNames);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("Assembly path cannot be null or empty.", error.GetString());
    }
    
    [Fact]
    public void GetTypeDetails_ShouldReturnError_WhenTypeNamesIsNull()
    {
        // Act
        var resultJson = _extractor.GetTypeDetails(_testDllPath, null);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.Equal("TypeNames array cannot be empty.", error.GetString());
    }
}