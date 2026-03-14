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
}
