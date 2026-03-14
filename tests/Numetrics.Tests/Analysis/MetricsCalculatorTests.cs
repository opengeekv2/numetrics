using Numetrics.Analysis;

namespace Numetrics.Tests.Analysis;

public class MetricsCalculatorTests
{
    [Fact]
    public void ComputeNamespaceMetrics_SingleConcreteType_ReturnsCorrectMetrics()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("MyType", "MyApp", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.Name.ShouldBe("MyApp");
        ns.TypeCount.ShouldBe(1);
        ns.AbstractTypeCount.ShouldBe(0);
        ns.AfferentCouplings.ShouldBe(0);
        ns.EfferentCouplings.ShouldBe(0);
        ns.Abstractness.ShouldBe(0.0);
        ns.Instability.ShouldBe(0.0);
        ns.Distance.ShouldBe(1.0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_TwoNamespacesWithDependency_ComputesCouplings()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.Services", "MyApp", false, new HashSet<string> { "MyApp.Models" }),
            new TypeDeclarationInfo("ModelB", "MyApp.Models", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var servicesMetrics = metrics.Single(m => m.Name == "MyApp.Services");
        servicesMetrics.AfferentCouplings.ShouldBe(0);
        servicesMetrics.EfferentCouplings.ShouldBe(1);
        servicesMetrics.Instability.ShouldBe(1.0);
        servicesMetrics.Distance.ShouldBe(0.0);

        var modelsMetrics = metrics.Single(m => m.Name == "MyApp.Models");
        modelsMetrics.AfferentCouplings.ShouldBe(1);
        modelsMetrics.EfferentCouplings.ShouldBe(0);
        modelsMetrics.Instability.ShouldBe(0.0);
        modelsMetrics.Distance.ShouldBe(1.0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_AbstractTypes_CalculatesAbstractness()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("IService", "MyApp", "MyApp", true, new HashSet<string>()),
            new TypeDeclarationInfo("ConcreteService", "MyApp", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.TypeCount.ShouldBe(2);
        ns.AbstractTypeCount.ShouldBe(1);
        ns.Abstractness.ShouldBe(0.5);
    }

    [Fact]
    public void ComputeNamespaceMetrics_IgnoresNonProjectNamespaceDependencies()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("MyService", "MyApp", "MyApp", false, new HashSet<string> { "System.Linq", "System.Collections.Generic" }),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.EfferentCouplings.ShouldBe(0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_WhenAllAbstract_AbstractnessIsOne()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("IService", "MyApp", "MyApp", true, new HashSet<string>()),
            new TypeDeclarationInfo("IRepository", "MyApp", "MyApp", true, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.Abstractness.ShouldBe(1.0);
        ns.Distance.ShouldBe(0.0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_WithGlobalUsings_CountsAsEfferentDependencies()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.Services", "MyApp", false, new HashSet<string>()),
            new TypeDeclarationInfo("ModelB", "MyApp.Models", "MyApp", false, new HashSet<string>()),
        };
        var globalUsings = new HashSet<string> { "MyApp.Models" };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types, globalUsings);

        var servicesMetrics = metrics.Single(m => m.Name == "MyApp.Services");
        servicesMetrics.EfferentCouplings.ShouldBe(1);

        var modelsMetrics = metrics.Single(m => m.Name == "MyApp.Models");
        modelsMetrics.AfferentCouplings.ShouldBe(1);
    }

    [Fact]
    public void ComputeNamespaceMetrics_GlobalUsings_DoNotSelfReference()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.Services", "MyApp", false, new HashSet<string>()),
        };
        var globalUsings = new HashSet<string> { "MyApp.Services" };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types, globalUsings);

        var ns = metrics.ShouldHaveSingleItem();
        ns.EfferentCouplings.ShouldBe(0);
    }

    [Fact]
    public void ComputeAssemblyMetrics_TwoAssembliesWithDependency_ComputesCouplings()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.Services", "MyApp.Services", false, new HashSet<string> { "MyApp.Models" }),
            new TypeDeclarationInfo("ModelB", "MyApp.Models", "MyApp.Models", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var servicesMetrics = metrics.Single(m => m.Name == "MyApp.Services");
        servicesMetrics.AfferentCouplings.ShouldBe(0);
        servicesMetrics.EfferentCouplings.ShouldBe(1);
        servicesMetrics.Instability.ShouldBe(1.0);

        var modelsMetrics = metrics.Single(m => m.Name == "MyApp.Models");
        modelsMetrics.AfferentCouplings.ShouldBe(1);
        modelsMetrics.EfferentCouplings.ShouldBe(0);
        modelsMetrics.Instability.ShouldBe(0.0);
    }

    [Fact]
    public void ComputeAssemblyMetrics_SingleAssembly_NoCouplings()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("TypeA", "MyApp.NS1", "MyApp", false, new HashSet<string> { "MyApp.NS2" }),
            new TypeDeclarationInfo("TypeB", "MyApp.NS2", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.AfferentCouplings.ShouldBe(0);
        ns.EfferentCouplings.ShouldBe(0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_InstabilityIsZero_WhenNoCouplings()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("MyType", "MyApp", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.Instability.ShouldBe(0.0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_WithCycle_IncludesCycleInPackageMetrics()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.A", "MyApp", false, new HashSet<string> { "MyApp.B" }),
            new TypeDeclarationInfo("ServiceB", "MyApp.B", "MyApp", false, new HashSet<string> { "MyApp.A" }),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        metrics.Single(m => m.Name == "MyApp.A").Cycles.ShouldNotBeEmpty();
        metrics.Single(m => m.Name == "MyApp.B").Cycles.ShouldNotBeEmpty();
    }

    [Fact]
    public void ComputeAssemblyMetrics_WithAbstractTypes_ComputesAbstractness()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("IService", "MyApp", "MyApp", true, new HashSet<string>()),
            new TypeDeclarationInfo("ConcreteService", "MyApp", "MyApp", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var asm = metrics.ShouldHaveSingleItem();
        asm.AbstractTypeCount.ShouldBe(1);
        asm.Abstractness.ShouldBe(0.5);
    }

    [Fact]
    public void ComputeAssemblyMetrics_WithGlobalUsings_CountsAsEfferentDependencies()
    {
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "MyApp.Services", "MyApp.Services", false, new HashSet<string>()),
            new TypeDeclarationInfo("ModelB", "MyApp.Models", "MyApp.Models", false, new HashSet<string>()),
        };
        var globalUsings = new HashSet<string> { "MyApp.Models" };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types, globalUsings);

        var servicesMetrics = metrics.Single(m => m.Name == "MyApp.Services");
        servicesMetrics.EfferentCouplings.ShouldBe(1);

        var modelsMetrics = metrics.Single(m => m.Name == "MyApp.Models");
        modelsMetrics.AfferentCouplings.ShouldBe(1);
    }

    [Fact]
    public void ComputeAssemblyMetrics_NamespaceDiffersFromAssemblyName_DependencyResolvedToAssembly()
    {
        // When namespace names differ from assembly names, the custom usingDirectiveToKey
        // must be used (not overwritten) to correctly map namespace usings to assembly names.
        var types = new[]
        {
            new TypeDeclarationInfo("ServiceA", "NS.Services", "AssemblyA", false, new HashSet<string> { "NS.Models" }),
            new TypeDeclarationInfo("ModelB", "NS.Models", "AssemblyB", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var servicesMetrics = metrics.Single(m => m.Name == "AssemblyA");
        servicesMetrics.EfferentCouplings.ShouldBe(1);

        var modelsMetrics = metrics.Single(m => m.Name == "AssemblyB");
        modelsMetrics.AfferentCouplings.ShouldBe(1);
    }

    [Fact]
    public void ComputeNamespaceMetrics_TwoPackagesDependOnSamePackage_AfferentCouplingIsTwo()
    {
        // Both NS.B and NS.C depend on NS.A → NS.A must have Ca=2.
        var types = new[]
        {
            new TypeDeclarationInfo("TypeA", "NS.A", "Asm", false, new HashSet<string>()),
            new TypeDeclarationInfo("TypeB", "NS.B", "Asm", false, new HashSet<string> { "NS.A" }),
            new TypeDeclarationInfo("TypeC", "NS.C", "Asm", false, new HashSet<string> { "NS.A" }),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var nsA = metrics.Single(m => m.Name == "NS.A");
        nsA.AfferentCouplings.ShouldBe(2);
    }

    [Fact]
    public void ComputeNamespaceMetrics_EqualAfferentAndEfferentCouplings_InstabilityIsHalf()
    {
        // Ce=1, Ca=1 → Instability = Ce / (Ce + Ca) = 1/2 = 0.5
        // If Ce+Ca is computed as Ce-Ca the denominator is 0 → Infinity instead of 0.5.
        var types = new[]
        {
            new TypeDeclarationInfo("TypeA", "NS.A", "Asm", false, new HashSet<string> { "NS.B" }),
            new TypeDeclarationInfo("TypeB", "NS.B", "Asm", false, new HashSet<string> { "NS.A" }),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var nsA = metrics.Single(m => m.Name == "NS.A");
        nsA.EfferentCouplings.ShouldBe(1);
        nsA.AfferentCouplings.ShouldBe(1);
        nsA.Instability.ShouldBe(0.5);
    }

    [Fact]
    public void ComputeNamespaceMetrics_HighEfferentLowAfferentCouplings_InstabilityIsCorrect()
    {
        // Ce=2, Ca=1 → Instability = 2 / (2+1) = 2/3 ≈ 0.667
        // If the formula uses Ce*(Ce+Ca) the result becomes 2/6 = 1/3, which is wrong.
        var types = new[]
        {
            new TypeDeclarationInfo("Hub", "NS.Hub", "Asm", false, new HashSet<string> { "NS.A", "NS.B" }),
            new TypeDeclarationInfo("TypeA", "NS.A", "Asm", false, new HashSet<string> { "NS.Hub" }),
            new TypeDeclarationInfo("TypeB", "NS.B", "Asm", false, new HashSet<string>()),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var hub = metrics.Single(m => m.Name == "NS.Hub");
        hub.EfferentCouplings.ShouldBe(2);
        hub.AfferentCouplings.ShouldBe(1);
        hub.Instability.ShouldBe(2.0 / 3.0, tolerance: 1e-10);
    }
}
