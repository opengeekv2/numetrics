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

    // ── ReferencedTypeNames (semantic model) tests ────────────────────────────
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
    public void AnalyzeSyntaxTrees_UnresolvableType_IsIgnored()
    {
        // "MissingType" is not present in the compilation. The semantic model
        // returns an error type, which must be silently discarded.
        const string code = """
            namespace MyApp.Services;
            class ServiceA
            {
                private MissingType field;
            }
            """;

        var types = ScanCode(code);

        types.ShouldHaveSingleItem().ReferencedTypeNames.ShouldBeEmpty();
    }

    [Fact]
    public void AnalyzeSyntaxTrees_FieldDeclaration_TypeNameCollected()
    {
        // Both ModelA and ServiceA are compiled together so the semantic model
        // can resolve "ModelA" to its fully-qualified name.
        const string code = """
            namespace MyApp.Services;
            class ModelA { }
            class ServiceA
            {
                private ModelA field;
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Services.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MethodParameter_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ModelA { }
            class ServiceA
            {
                public void Process(ModelA model) { }
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Services.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_MethodReturnType_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class ModelA { }
            class ServiceA
            {
                public ModelA Get() => null;
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Services.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_BaseClass_TypeNameCollected()
    {
        const string code = """
            namespace MyApp.Services;
            class BaseService { }
            class ServiceA : BaseService { }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Services.BaseService");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_QualifiedTypeName_CollectedAsFullyQualifiedName()
    {
        // ModelA is in a separate namespace, referenced with a qualified name.
        // The semantic model resolves the qualified syntax to the FQN.
        const string modelCode = """
            namespace MyApp.Models;
            class ModelA { }
            """;

        const string serviceCode = """
            namespace MyApp.Services;
            class ServiceA
            {
                private MyApp.Models.ModelA field;
            }
            """;

        var trees = new[]
        {
            (CSharpSyntaxTree.ParseText(modelCode), "TestAssembly"),
            (CSharpSyntaxTree.ParseText(serviceCode), "TestAssembly"),
        };
        var types = CSharpFileScanner.AnalyzeSyntaxTrees(trees);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Models.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_GenericFieldType_TypeArgumentCollected()
    {
        // ModelA is the type argument of List<>. The walker recursively resolves
        // the type argument, producing its fully-qualified name.
        const string modelCode = """
            namespace MyApp.Models;
            class ModelA { }
            """;

        const string serviceCode = """
            using System.Collections.Generic;
            using MyApp.Models;
            namespace MyApp.Services;
            class ServiceA
            {
                private List<ModelA> list;
            }
            """;

        var trees = new[]
        {
            (CSharpSyntaxTree.ParseText(modelCode), "TestAssembly"),
            (CSharpSyntaxTree.ParseText(serviceCode), "TestAssembly"),
        };
        var types = CSharpFileScanner.AnalyzeSyntaxTrees(trees);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.Models.ModelA");
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
    public async Task LoadSolutionAsync_OnlyFindsCSFiles_IgnoresOtherExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Valid.cs"), "namespace MyApp; class MyType { }");
            File.WriteAllText(Path.Combine(tempDir, "Other.txt"), "namespace OtherApp; class OtherType { }");
            File.WriteAllText(
                Path.Combine(tempDir, "MyApp.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "MyApp", "MyApp.csproj");

            var types = await CSharpFileScanner.LoadSolutionAsync(slnPath);

            types.ShouldHaveSingleItem().Name.ShouldBe("MyType");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSolutionAsync_ReadsAssemblyNameFromProject()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Code.cs"), "namespace MyApp; class MyType { }");
            File.WriteAllText(
                Path.Combine(tempDir, "MyProject.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "MyProject", "MyProject.csproj");

            var types = await CSharpFileScanner.LoadSolutionAsync(slnPath);

            types.ShouldHaveSingleItem().AssemblyName.ShouldBe("MyProject");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSolutionAsync_CrossFileFieldReference_TypeNameResolved()
    {
        // Both files are in the same project so the workspace compilation resolves
        // "MyApp.Models.ModelA" from ServiceA.cs to its fully-qualified name.
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "ModelA.cs"),
                "namespace MyApp.Models; class ModelA { }");
            File.WriteAllText(
                Path.Combine(tempDir, "ServiceA.cs"),
                "namespace MyApp.Services; class ServiceA { private MyApp.Models.ModelA field; }");
            File.WriteAllText(
                Path.Combine(tempDir, "MyApp.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "MyApp", "MyApp.csproj");

            var types = await CSharpFileScanner.LoadSolutionAsync(slnPath);

            types.Single(t => t.Name == "ServiceA")
                 .ReferencedTypeNames.ShouldContain("MyApp.Models.ModelA");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeSyntaxTrees_GlobalNamespaceTypeReference_StoredAsSimpleName()
    {
        // ModelA has no namespace (global namespace).  The collected reference name
        // must be "ModelA", not ".ModelA" (which would happen if the namespace
        // display string were always prepended, even for the global namespace).
        const string code = """
            class ModelA { }
            class ServiceA
            {
                private ModelA field;
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_CastExpressionInVariableInitializer_TypeCollected()
    {
        // The declared field type is object, but the initializer casts to ModelA.
        // The walker must traverse into the variable initializer via
        // base.VisitVariableDeclaration to reach the cast expression and
        // trigger VisitCastExpression.
        const string code = """
            namespace MyApp;
            class ModelA { }
            class ServiceA
            {
                private object field = (ModelA)null;
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.ModelA");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_AttributeOnMethodParameter_TypeCollected()
    {
        // The parameter carries an attribute whose class is in the same namespace.
        // The walker must traverse into the parameter's attribute list via
        // base.VisitParameter to discover MyParamAttr.
        const string code = """
            namespace MyApp;
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            class MyParamAttr : System.Attribute { }
            class ServiceA
            {
                public void Process([MyParamAttr] int x) { }
            }
            """;

        var types = ScanCode(code);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.MyParamAttr");
    }

    [Fact]
    public void AnalyzeSyntaxTrees_CastExpressionInPrimaryConstructorBaseArg_TypeCollected()
    {
        // ServiceA's primary constructor passes "(ModelA)null" to its base.
        // The walker must traverse into the base-list constructor argument via
        // base.VisitBaseList to reach the cast expression and trigger
        // VisitCastExpression.
        const string code = """
            namespace MyApp;
            class ModelA { }
            class Base(object arg) { }
            class ServiceA() : Base((ModelA)null) { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var types = CSharpFileScanner.AnalyzeSyntaxTrees([(tree, "TestAssembly")]);

        types.Single(t => t.Name == "ServiceA")
             .ReferencedTypeNames.ShouldContain("MyApp.ModelA");
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static IReadOnlyList<TypeDeclarationInfo> ScanCode(string code, string assemblyName = "TestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return CSharpFileScanner.AnalyzeSyntaxTrees([(tree, assemblyName)]);
    }
}
