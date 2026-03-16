using Numetrics.Analysis;

namespace Numetrics.Tests.Analysis;

public class MetricsCalculatorTests
{
    [Fact]
    public void ComputeNamespaceMetrics_SingleConcreteType_ReturnsCorrectMetrics()
    {
        var types = new[] { MakeType("MyType", "MyApp", "MyApp") };

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
        // ServiceA has a field of type ModelB — the semantic model produces the
        // fully-qualified name "MyApp.Models.ModelB" in ReferencedTypeNames.
        var types = new[]
        {
            MakeType("ServiceA", "MyApp.Services", "MyApp", refs: new[] { "MyApp.Models.ModelB" }),
            MakeType("ModelB",   "MyApp.Models",   "MyApp"),
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
            MakeType("IService",         "MyApp", "MyApp", isAbstract: true),
            MakeType("ConcreteService",  "MyApp", "MyApp"),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.TypeCount.ShouldBe(2);
        ns.AbstractTypeCount.ShouldBe(1);
        ns.Abstractness.ShouldBe(0.5);
    }

    [Fact]
    public void ComputeNamespaceMetrics_ExternalTypeInRefs_IsIgnored()
    {
        // Fully-qualified external type names that are not present in the project
        // registry must not contribute to efferent coupling.
        var types = new[]
        {
            MakeType(
                "MyService",
                "MyApp",
                "MyApp",
                refs: new[] { "System.Collections.Generic.IEnumerable", "System.Linq.IQueryable" }),
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
            MakeType("IService",    "MyApp", "MyApp", isAbstract: true),
            MakeType("IRepository", "MyApp", "MyApp", isAbstract: true),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.Abstractness.ShouldBe(1.0);
        ns.Distance.ShouldBe(0.0);
    }

    [Fact]
    public void ComputeAssemblyMetrics_TwoAssembliesWithDependency_ComputesCouplings()
    {
        var types = new[]
        {
            MakeType("ServiceA", "MyApp.Services", "MyApp.Services", refs: new[] { "MyApp.Models.ModelB" }),
            MakeType("ModelB",   "MyApp.Models",   "MyApp.Models"),
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
        // Both types live in the same assembly – cross-namespace ref stays internal.
        var types = new[]
        {
            MakeType("TypeA", "MyApp.NS1", "MyApp", refs: new[] { "MyApp.NS2.TypeB" }),
            MakeType("TypeB", "MyApp.NS2", "MyApp"),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.AfferentCouplings.ShouldBe(0);
        ns.EfferentCouplings.ShouldBe(0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_InstabilityIsZero_WhenNoCouplings()
    {
        var types = new[] { MakeType("MyType", "MyApp", "MyApp") };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var ns = metrics.ShouldHaveSingleItem();
        ns.Instability.ShouldBe(0.0);
    }

    [Fact]
    public void ComputeNamespaceMetrics_WithCycle_IncludesCycleInPackageMetrics()
    {
        var types = new[]
        {
            MakeType("ServiceA", "MyApp.A", "MyApp", refs: new[] { "MyApp.B.ServiceB" }),
            MakeType("ServiceB", "MyApp.B", "MyApp", refs: new[] { "MyApp.A.ServiceA" }),
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
            MakeType("IService",        "MyApp", "MyApp", isAbstract: true),
            MakeType("ConcreteService", "MyApp", "MyApp"),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var asm = metrics.ShouldHaveSingleItem();
        asm.AbstractTypeCount.ShouldBe(1);
        asm.Abstractness.ShouldBe(0.5);
    }

    [Fact]
    public void ComputeAssemblyMetrics_NamespaceDiffersFromAssemblyName_DependencyResolvedToAssembly()
    {
        var types = new[]
        {
            MakeType("ServiceA", "NS.Services", "AssemblyA", refs: new[] { "NS.Models.ModelB" }),
            MakeType("ModelB",   "NS.Models",   "AssemblyB"),
        };

        var metrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        var assemblyA = metrics.Single(m => m.Name == "AssemblyA");
        var assemblyB = metrics.Single(m => m.Name == "AssemblyB");
        assemblyA.EfferentCouplings.ShouldBe(1);
        assemblyB.AfferentCouplings.ShouldBe(1);
    }

    [Fact]
    public void ComputeNamespaceMetrics_TwoPackagesDependOnSamePackage_AfferentCouplingIsTwo()
    {
        var types = new[]
        {
            MakeType("TypeA", "NS.A", "Asm"),
            MakeType("TypeB", "NS.B", "Asm", refs: new[] { "NS.A.TypeA" }),
            MakeType("TypeC", "NS.C", "Asm", refs: new[] { "NS.A.TypeA" }),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        metrics.Single(m => m.Name == "NS.A").AfferentCouplings.ShouldBe(2);
    }

    [Fact]
    public void ComputeNamespaceMetrics_EqualAfferentAndEfferentCouplings_InstabilityIsHalf()
    {
        var types = new[]
        {
            MakeType("TypeA", "NS.A", "Asm", refs: new[] { "NS.B.TypeB" }),
            MakeType("TypeB", "NS.B", "Asm", refs: new[] { "NS.A.TypeA" }),
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
        var types = new[]
        {
            MakeType("Hub",   "NS.Hub", "Asm", refs: new[] { "NS.A.TypeA", "NS.B.TypeB" }),
            MakeType("TypeA", "NS.A",   "Asm", refs: new[] { "NS.Hub.Hub" }),
            MakeType("TypeB", "NS.B",   "Asm"),
        };

        var metrics = MetricsCalculator.ComputeNamespaceMetrics(types);

        var hub = metrics.Single(m => m.Name == "NS.Hub");
        hub.EfferentCouplings.ShouldBe(2);
        hub.AfferentCouplings.ShouldBe(1);
        hub.Instability.ShouldBe(2.0 / 3.0, tolerance: 1e-10);
    }

    private static TypeDeclarationInfo MakeType(
        string name,
        string ns,
        string assembly,
        bool isAbstract = false,
        IEnumerable<string>? refs = null)
    {
        return new TypeDeclarationInfo(
            name,
            ns,
            assembly,
            isAbstract,
            new HashSet<string>(refs ?? Enumerable.Empty<string>()));
    }
}
