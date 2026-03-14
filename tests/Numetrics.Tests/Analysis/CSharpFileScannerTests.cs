using Microsoft.CodeAnalysis.CSharp;
using Numetrics.Analysis;

namespace Numetrics.Tests.Analysis;

public class CSharpFileScannerTests
{
    [Fact]
    public void AnalyzeSyntaxTrees_SingleConcreteClass_ReturnsTypeInfo()
    {
        const string code = """
            namespace MyApp;
            class MyType { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.Name.ShouldBe("MyType");
        type.Namespace.ShouldBe("MyApp");
        type.IsAbstract.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_AbstractClass_IsAbstractTrue()
    {
        const string code = """
            namespace MyApp;
            abstract class AbstractService { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.IsAbstract.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_Interface_IsAbstractTrue()
    {
        const string code = """
            namespace MyApp;
            interface IService { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.Name.ShouldBe("IService");
        type.IsAbstract.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_UsingDirectives_StoredForResolutionContext()
    {
        const string code = """
            using MyApp.Models;
            using System.Linq;

            namespace MyApp.Services;
            class ServiceA { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldContain("MyApp.Models");
        type.UsingDirectives.ShouldContain("System.Linq");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MultipleTypesInFile_AllReturned()
    {
        const string code = """
            namespace MyApp;
            class TypeA { }
            class TypeB { }
            interface ITypeC { }
            """;

        var types = ScanCode(code);

        types.Count.ShouldBe(3);
        types.Select(t => t.Name).ShouldContain("TypeA");
        types.Select(t => t.Name).ShouldContain("TypeB");
        types.Select(t => t.Name).ShouldContain("ITypeC");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MultipleTypesShareUsingDirectives()
    {
        const string code = """
            using MyApp.Models;

            namespace MyApp.Services;
            class ServiceA { }
            class ServiceB { }
            """;

        var types = ScanCode(code);

        types.Count.ShouldBe(2);
        foreach (var type in types)
        {
            type.UsingDirectives.ShouldContain("MyApp.Models");
        }
    }

    [Fact]
    public void AnalyzeSyntaxTrees_GlobalUsingDirective_IsIncludedInTypeUsingDirectives()
    {
        const string code = """
            global using MyApp.Models;

            namespace MyApp.Services;
            class ServiceA { }
            """;

        var trees = new[] { (CSharpSyntaxTree.ParseText(code), "TestAssembly") };
        var types = CSharpFileScanner.AnalyzeSyntaxTrees(trees);

        // Global usings are folded into each type's UsingDirectives for resolution.
        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldContain("MyApp.Models");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_AssemblyNameIsPreserved()
    {
        const string code = """
            namespace MyApp;
            class MyType { }
            """;

        var types = ScanCode(code, "My.Assembly");

        var type = types.ShouldHaveSingleItem();
        type.AssemblyName.ShouldBe("My.Assembly");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_BlockScopedNamespace_TypesExtracted()
    {
        const string code = """
            namespace MyApp.Services
            {
                class ServiceA { }
                abstract class BaseService { }
            }
            """;

        var types = ScanCode(code);

        types.Count.ShouldBe(2);
        types.All(t => t.Namespace == "MyApp.Services").ShouldBeTrue();
        types.Single(t => t.Name == "BaseService").IsAbstract.ShouldBeTrue();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_StructType_IsConcreteByDefault()
    {
        const string code = """
            namespace MyApp;
            struct MyStruct { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.IsAbstract.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_RecordType_ExtractedCorrectly()
    {
        const string code = """
            namespace MyApp;
            record MyRecord(string Name);
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.Name.ShouldBe("MyRecord");
        type.Namespace.ShouldBe("MyApp");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_NoNamespace_UsesGlobalNamespace()
    {
        const string code = """
            class GlobalClass { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.Namespace.ShouldBe(string.Empty);
    }

    [Fact]
    public void AnalyzeSyntaxTrees_StaticUsing_IsNotInUsingDirectives()
    {
        const string code = """
            using static System.Math;
            namespace MyApp;
            class MyType { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldBeEmpty();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_AliasUsing_IsNotInUsingDirectives()
    {
        const string code = """
            using MyAlias = System.Collections.Generic;
            namespace MyApp;
            class MyType { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldBeEmpty();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_NamespaceScopedUsing_ExtractedForTypesInNamespace()
    {
        const string code = """
            namespace MyApp.Services
            {
                using MyApp.Models;
                class ServiceA { }
            }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldContain("MyApp.Models");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_FileScopedNamespaceWithBothFileAndNamespaceLevelUsings_AllUsingsIncluded()
    {
        const string code = """
            using MyApp.Models;

            namespace MyApp.Services;
            using MyApp.Logging;
            class ServiceA { }
            """;

        var types = ScanCode(code);

        var type = types.ShouldHaveSingleItem();
        type.UsingDirectives.ShouldContain("MyApp.Models");
        type.UsingDirectives.ShouldContain("MyApp.Logging");
    }

    // ── ReferencedTypeNames (semantic walker) tests ────────────────────────────
    [Fact]
    public void AnalyzeSyntaxTrees_ClassWithNoMembers_ReferencedTypeNamesIsEmpty()
    {
        const string code = """
            namespace MyApp;
            class Empty { }
            """;

        var types = ScanCode(code);

        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldBeEmpty();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_FieldDeclaration_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ServiceA
            {
                private ModelA field;
            }
            """;

        var types = ScanCode(code);

        // Without the model type in the same compilation the semantic model
        // cannot resolve "ModelA" — it falls back to the syntactic name.
        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldContain("ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MethodParameter_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ServiceA
            {
                public void Process(ModelA model) { }
            }
            """;

        var types = ScanCode(code);

        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldContain("ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MethodReturnType_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ServiceA
            {
                public ModelA Get() => null;
            }
            """;

        var types = ScanCode(code);

        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldContain("ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_BaseClass_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ServiceA : BaseService { }
            """;

        var types = ScanCode(code);

        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldContain("BaseService");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_QualifiedTypeName_CollectedAsQualifiedString()
    {
        const string code = """
            namespace MyApp.Services;
            class ServiceA
            {
                private MyApp.Models.ModelA field;
            }
            """;

        var types = ScanCode(code);

        // With no ModelA in the compilation the semantic model can't resolve it,
        // so the qualified syntax string is recorded as the fallback.
        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldContain("MyApp.Models.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_GenericFieldType_OuterAndArgumentTypesCollected()
    {
        const string code = """
            using System.Collections.Generic;
            namespace MyApp.Services;
            class ServiceA
            {
                private List<ModelA> list;
            }
            """;

        var types = ScanCode(code);

        var referencedNames = types.ShouldHaveSingleItem().ReferencedTypeNames;
        referencedNames.ShouldContain("ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_CrossFileSemanticResolution_ProducesFullyQualifiedName()
    {
        // When both types are compiled together the semantic model can resolve
        // the simple name "ModelA" to its fully-qualified "MyApp.Models.ModelA".
        const string modelCode = """
            namespace MyApp.Models;
            class ModelA { }
            """;

        const string serviceCode = """
            using MyApp.Models;
            namespace MyApp.Services;
            class ServiceA
            {
                private ModelA field;
            }
            """;

        var trees = new[]
        {
            (CSharpSyntaxTree.ParseText(modelCode), "TestAssembly"),
            (CSharpSyntaxTree.ParseText(serviceCode), "TestAssembly"),
        };
        var types = CSharpFileScanner.AnalyzeSyntaxTrees(trees);

        var serviceA = types.Single(t => t.Name == "ServiceA");
        serviceA.ReferencedTypeNames.ShouldContain("MyApp.Models.ModelA");
    }

    [Fact]
    public void ScanDirectory_OnlyFindsCSFiles_IgnoresOtherExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Valid.cs"), "namespace MyApp; class MyType { }");
            File.WriteAllText(Path.Combine(tempDir, "Other.txt"), "namespace OtherApp; class OtherType { }");

            var types = CSharpFileScanner.ScanDirectory(tempDir);

            types.ShouldHaveSingleItem().Name.ShouldBe("MyType");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ScanDirectory_ReadsAssemblyNameFromCsprojFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var projectDir = Path.Combine(tempDir, "MyProject");
        Directory.CreateDirectory(projectDir);
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "Code.cs"), "namespace MyApp; class MyType { }");
            File.WriteAllText(
                Path.Combine(projectDir, "MyProject.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");

            var types = CSharpFileScanner.ScanDirectory(tempDir);

            types.ShouldHaveSingleItem().AssemblyName.ShouldBe("MyProject");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static IReadOnlyList<TypeDeclarationInfo> ScanCode(string code, string assemblyName = "TestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return CSharpFileScanner.AnalyzeSyntaxTrees([(tree, assemblyName)]);
    }
}
