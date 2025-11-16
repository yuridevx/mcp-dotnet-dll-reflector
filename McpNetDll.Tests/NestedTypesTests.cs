using McpNetDll.Registry;
using McpNetDll.Repository;
using Xunit;

namespace McpNetDll.Tests;

public class NestedTypesTests
{
    private readonly ITypeRegistry _typeRegistry;
    private readonly IMetadataRepository _repository;

    public NestedTypesTests()
    {
        _typeRegistry = new TypeRegistry();
        var testDllPath = Path.Combine(Directory.GetCurrentDirectory(), "MyTestLibrary.dll");
        _typeRegistry.LoadAssembly(testDllPath);
        _repository = new MetadataRepository(_typeRegistry);
    }

    [Fact]
    public void Should_Load_Public_Nested_Classes()
    {
        // Act
        var nestedClass = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedClass");

        // Assert
        Assert.NotNull(nestedClass);
        Assert.Equal("MyTestLibrary.NestedTypes", nestedClass.Namespace);
        Assert.Equal("TopLevelClass+PublicNestedClass", nestedClass.Name);
        Assert.Equal("nested class", nestedClass.TypeKind);
    }

    [Fact]
    public void Should_Load_Deeply_Nested_Classes()
    {
        // Act
        var deeplyNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedClass+DeeplyNestedClass");
        var veryDeeplyNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedClass+DeeplyNestedClass+VeryDeeplyNestedClass");

        // Assert
        Assert.NotNull(deeplyNested);
        Assert.Equal("TopLevelClass+PublicNestedClass+DeeplyNestedClass", deeplyNested.Name);

        Assert.NotNull(veryDeeplyNested);
        Assert.Equal("TopLevelClass+PublicNestedClass+DeeplyNestedClass+VeryDeeplyNestedClass", veryDeeplyNested.Name);
    }

    [Fact]
    public void Should_Load_Different_Nested_Type_Kinds()
    {
        // Act
        var nestedStruct = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedStruct");
        var nestedEnum = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedEnum");
        var nestedDelegate = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedDelegate");
        var nestedInterface = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+IPublicNestedInterface");
        var nestedStaticClass = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedStaticClass");

        // Assert
        Assert.NotNull(nestedStruct);
        Assert.Equal("nested struct", nestedStruct.TypeKind);

        Assert.NotNull(nestedEnum);
        Assert.Equal("nested enum", nestedEnum.TypeKind);
        Assert.NotNull(nestedEnum.EnumValues);
        Assert.Equal(3, nestedEnum.EnumValues.Count);

        Assert.NotNull(nestedDelegate);
        Assert.Equal("nested delegate", nestedDelegate.TypeKind);

        Assert.NotNull(nestedInterface);
        Assert.Equal("nested interface", nestedInterface.TypeKind);

        Assert.NotNull(nestedStaticClass);
        Assert.Equal("nested static class", nestedStaticClass.TypeKind);
    }

    [Fact]
    public void Should_Not_Load_Private_Or_Internal_Nested_Types()
    {
        // Act
        var privateNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+PrivateNestedClass");
        var internalNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+InternalNestedClass");

        // Assert
        Assert.Null(privateNested);
        Assert.Null(internalNested);
    }

    [Fact]
    public void Should_Load_Protected_Nested_Types()
    {
        // Protected types are visible from outside the assembly
        // Act
        var protectedNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+ProtectedNestedClass");
        var protectedInternalNested = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.TopLevelClass+ProtectedInternalNestedClass");

        // Assert
        Assert.NotNull(protectedNested);
        Assert.Equal("nested class", protectedNested.TypeKind);

        Assert.NotNull(protectedInternalNested);
        Assert.Equal("nested class", protectedInternalNested.TypeKind);
    }

    [Fact]
    public void Should_Load_Nested_Types_In_Static_Classes()
    {
        // Act
        var nestedInStatic = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.StaticTopLevelClass+NestedInStatic");
        var staticNestedInStatic = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.StaticTopLevelClass+StaticNestedInStatic");

        // Assert
        Assert.NotNull(nestedInStatic);
        Assert.Equal("nested class", nestedInStatic.TypeKind);

        Assert.NotNull(staticNestedInStatic);
        Assert.Equal("nested static class", staticNestedInStatic.TypeKind);
    }

    [Fact]
    public void Should_Load_Nested_Types_In_Abstract_Classes()
    {
        // Act
        var nestedAbstract = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.AbstractTopLevelClass+NestedAbstractClass");
        var nestedSealed = _typeRegistry.GetTypeByFullName("MyTestLibrary.NestedTypes.AbstractTopLevelClass+NestedSealedClass");

        // Assert
        Assert.NotNull(nestedAbstract);
        Assert.Equal("nested abstract class", nestedAbstract.TypeKind);

        Assert.NotNull(nestedSealed);
        Assert.Equal("nested sealed class", nestedSealed.TypeKind);
    }

    [Fact]
    public void Should_Search_Nested_Types()
    {
        // Act
        var searchResult = _repository.SearchElements("PublicNested", "types");

        // Assert
        Assert.Null(searchResult.Error);
        Assert.NotNull(searchResult.Results);

        var nestedTypes = searchResult.Results.Where(r => r.Name.Contains("+")).ToList();
        Assert.NotEmpty(nestedTypes);

        // Should find PublicNestedClass, PublicNestedStruct, PublicNestedEnum, etc.
        Assert.Contains(searchResult.Results, r => r.Name == "TopLevelClass+PublicNestedClass");
        Assert.Contains(searchResult.Results, r => r.Name == "TopLevelClass+PublicNestedStruct");
        Assert.Contains(searchResult.Results, r => r.Name == "TopLevelClass+PublicNestedEnum");
        Assert.Contains(searchResult.Results, r => r.Name == "TopLevelClass+PublicNestedDelegate");
        Assert.Contains(searchResult.Results, r => r.Name == "TopLevelClass+PublicNestedStaticClass");
    }

    [Fact]
    public void Should_Include_Nested_Types_In_Namespace_Query()
    {
        // Act
        var namespaceResult = _repository.QueryNamespaces(new[] { "MyTestLibrary.NestedTypes" });

        // Assert
        Assert.Null(namespaceResult.Error);
        Assert.NotNull(namespaceResult.Namespaces);
        Assert.Single(namespaceResult.Namespaces);

        var ns = namespaceResult.Namespaces[0];
        Assert.Equal("MyTestLibrary.NestedTypes", ns.Name);

        // Should include both top-level and nested types
        var nestedTypes = ns.Types.Where(t => t.Name.Contains("+")).ToList();
        Assert.NotEmpty(nestedTypes);

        // Check for specific nested types
        Assert.Contains(ns.Types, t => t.Name == "TopLevelClass+PublicNestedClass");
        Assert.Contains(ns.Types, t => t.Name == "TopLevelClass+PublicNestedStruct");
        Assert.Contains(ns.Types, t => t.Name == "TopLevelClass+PublicNestedEnum");
    }

    [Fact]
    public void Should_Get_Type_Details_For_Nested_Types()
    {
        // Act
        var typeDetails = _repository.QueryTypeDetails(new[]
        {
            "MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedClass",
            "MyTestLibrary.NestedTypes.TopLevelClass+PublicNestedStaticClass"
        });

        // Assert
        Assert.Null(typeDetails.Error);
        Assert.NotNull(typeDetails.Types);
        Assert.Equal(2, typeDetails.Types.Count);

        var nestedClass = typeDetails.Types.First(t => t.Name == "TopLevelClass+PublicNestedClass");
        Assert.NotNull(nestedClass);
        Assert.Equal("nested class", nestedClass.TypeKind);
        Assert.NotNull(nestedClass.Properties);
        Assert.Contains(nestedClass.Properties, p => p.Name == "Name");

        var staticClass = typeDetails.Types.First(t => t.Name == "TopLevelClass+PublicNestedStaticClass");
        Assert.NotNull(staticClass);
        Assert.Equal("nested static class", staticClass.TypeKind);
        Assert.NotNull(staticClass.Methods);
        Assert.Contains(staticClass.Methods, m => m.Name == "StaticMethod");
    }

    [Fact]
    public void Should_Handle_Generic_Nested_Types()
    {
        // Generic types have complex naming, but nested non-generic types in generic classes should work
        // Act
        var allTypes = _typeRegistry.GetAllTypes();
        var genericNestedTypes = allTypes.Where(t =>
            t.Name.StartsWith("GenericTopLevelClass") &&
            t.Name.Contains("+")).ToList();

        // Assert
        Assert.NotEmpty(genericNestedTypes);

        // We should at least find the non-generic nested class
        var nonGenericNested = allTypes.FirstOrDefault(t =>
            t.Name.Contains("GenericTopLevelClass") &&
            t.Name.Contains("+NonGenericNested"));

        Assert.NotNull(nonGenericNested);
    }

    [Fact]
    public void Should_TryGetType_Work_With_Simple_Nested_Names()
    {
        // TryGetType should work with simple names if they're unique
        // Act
        var found = _typeRegistry.TryGetType("TopLevelClass+PublicNestedEnum", out var type);

        // Assert
        Assert.True(found);
        Assert.NotNull(type);
        Assert.Equal("TopLevelClass+PublicNestedEnum", type.Name);
        Assert.Equal("MyTestLibrary.NestedTypes", type.Namespace);
    }

    [Fact]
    public void Should_Count_Nested_Types_Correctly()
    {
        // Act
        var allTypes = _typeRegistry.GetAllTypes();
        var nestedTypes = allTypes.Where(t => t.Name.Contains("+")).ToList();
        var topLevelTypes = allTypes.Where(t => !t.Name.Contains("+")).ToList();

        // Assert
        Assert.NotEmpty(nestedTypes);
        Assert.NotEmpty(topLevelTypes);

        // We should have at least the nested types we defined
        Assert.True(nestedTypes.Count >= 10); // We defined at least 10 public nested types
    }
}