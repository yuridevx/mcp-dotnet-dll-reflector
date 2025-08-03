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
    private readonly string _testXmlPath;
    private readonly Extractor _extractor;

    public ExtractorTests()
    {
        // Adjust the path to be relative to the test project's output directory
        // Assuming MyTestLibrary.dll is in the same output directory as the test assembly
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _testDllPath = Path.Combine(assemblyLocation, "MyTestLibrary.dll");
        // _testXmlPath = Path.ChangeExtension(_testDllPath, ".xml"); // XML documentation extraction removed

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
        Assert.NotNull(myTestLibraryNamespace);
        Assert.Equal(5, myTestLibraryNamespace.GetProperty("TypeCount").GetInt32()); // MyPublicClass, IMyInterface, MyEnum, MyStruct, MyGenericClass`1
        Assert.True(myTestLibraryNamespace.TryGetProperty("Types", out var types));
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyPublicClass");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "IMyInterface");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyEnum");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyStruct");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyGenericClass`1");
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
        Assert.Equal(5, types.EnumerateArray().Count());

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
        Assert.NotNull(myPublicClass);
        Assert.True(myPublicClass.TryGetProperty("Methods", out var methods)); // Should contain detailed methods
        Assert.True(myPublicClass.TryGetProperty("Properties", out var properties)); // Should contain detailed properties
        Assert.True(methods.EnumerateArray().Any(m => m.GetProperty("Name").GetString() == "GetInstanceMessage"));
        Assert.True(properties.EnumerateArray().Any(p => p.GetProperty("Name").GetString() == "InstanceProperty"));
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
        Assert.NotNull(myPublicClass);
        Assert.True(myPublicClass.TryGetProperty("Methods", out var methods)); // Should contain detailed methods
        Assert.True(myPublicClass.TryGetProperty("Properties", out var properties)); // Should contain detailed properties
        Assert.True(methods.EnumerateArray().Any(m => m.GetProperty("Name").GetString() == "GetInstanceMessage"));
        Assert.True(properties.EnumerateArray().Any(p => p.GetProperty("Name").GetString() == "InstanceProperty"));
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
        Assert.Contains("Type(s) not found: MyTestLibrary.NonExistentClass", error.GetString());
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
        Assert.NotNull(myTestLibraryNamespace);
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

        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyPublicClass" && t.GetProperty("TypeKind").GetString() == "Class");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "IMyInterface" && t.GetProperty("TypeKind").GetString() == "Interface");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyEnum" && t.GetProperty("TypeKind").GetString() == "Enum");
        Assert.Contains(types.EnumerateArray(), t => t.GetProperty("Name").GetString() == "MyStruct" && t.GetProperty("TypeKind").GetString() == "Struct");
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
        // Assert.NotNull is not needed for JsonElement types: https://xunit.net/xunit.analyzers/rules/xUnit2002
        Assert.Equal("Enum", myEnum.GetProperty("TypeKind").GetString());
        Assert.True(myEnum.TryGetProperty("EnumValues", out var enumValues));
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueA" && ev.GetProperty("Value").GetString() == "0");
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueB" && ev.GetProperty("Value").GetString() == "10");
        Assert.Contains(enumValues.EnumerateArray(), ev => ev.GetProperty("Name").GetString() == "ValueC" && ev.GetProperty("Value").GetString() == "11");

        var myStruct = types.EnumerateArray().FirstOrDefault(t => t.GetProperty("Name").GetString() == "MyStruct");
        // Assert.NotNull is not needed for JsonElement types: https://xunit.net/xunit.analyzers/rules/xUnit2002
        Assert.Equal("Struct", myStruct.GetProperty("TypeKind").GetString());
        Assert.True(myStruct.TryGetProperty("Properties", out var structProperties));
        Assert.Contains(structProperties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "Number");
        Assert.Contains(structProperties.EnumerateArray(), p => p.GetProperty("Name").GetString() == "Text");
        Assert.True(myStruct.TryGetProperty("Methods", out var structMethods));
        Assert.Contains(structMethods.EnumerateArray(), m => m.GetProperty("Name").GetString() == "GetInfo");

        var myGenericClass = types.EnumerateArray().FirstOrDefault(t => t.GetProperty("Name").GetString() == "MyGenericClass`1");
        // Assert.NotNull is not needed for JsonElement types: https://xunit.net/xunit.analyzers/rules/xUnit2002
        Assert.Equal("Class", myGenericClass.GetProperty("TypeKind").GetString());
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
        Assert.Contains("Type(s) not found: NonExistentType", error.GetString());
    }
}