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
    public void AnalyzeSyntaxTrees_UsingDirectives_ExtractedAsUsings()
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
    public void AnalyzeSyntaxTrees_GlobalUsingDirective_IsExtracted()
    {
        const string code = """
            global using MyApp.Models;

            namespace MyApp.Services;
            class ServiceA { }
            """;

        var trees = new[] { (CSharpSyntaxTree.ParseText(code), "TestAssembly") };
        var (types, globalUsings) = CSharpFileScanner.AnalyzeSyntaxTreesWithGlobalUsings(trees);

        globalUsings.ShouldContain("MyApp.Models");
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
    public void AnalyzeSyntaxTrees_StaticUsing_IsNotExtracted()
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
    public void AnalyzeSyntaxTrees_AliasUsing_IsNotExtracted()
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
    public void AnalyzeSyntaxTrees_GlobalUsing_IsNotInTypeUsingDirectives()
    {
        const string code = """
            global using MyApp.Models;

            namespace MyApp.Services;
            class ServiceA { }
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
        // File-level using AND a using inside the file-scoped namespace declaration.
        // Both must end up in the type's UsingDirectives (Concat, not Except).
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

    [Fact]
    public void ScanDirectory_OnlyFindsCSFiles_IgnoresOtherExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Valid.cs"), "namespace MyApp; class MyType { }");
            File.WriteAllText(Path.Combine(tempDir, "Other.txt"), "namespace OtherApp; class OtherType { }");

            var (types, _) = CSharpFileScanner.ScanDirectory(tempDir);

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

            var (types, _) = CSharpFileScanner.ScanDirectory(tempDir);

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
