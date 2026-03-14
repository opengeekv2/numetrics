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

    private static IReadOnlyList<TypeDeclarationInfo> ScanCode(string code, string assemblyName = "TestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return CSharpFileScanner.AnalyzeSyntaxTrees([(tree, assemblyName)]);
    }
}
