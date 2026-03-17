using Numetrics.Analysis;

namespace Numetrics.Tests.Analysis;

public class CycleDetectorTests
{
    [Fact]
    public void DetectCycles_NoDependencies_ReturnsNoCycles()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string>(),
            ["B"] = new HashSet<string>(),
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldBeEmpty();
    }

    [Fact]
    public void DetectCycles_LinearChain_ReturnsNoCycles()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "C" },
            ["C"] = new HashSet<string>(),
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldBeEmpty();
    }

    [Fact]
    public void DetectCycles_SimpleCycle_ReturnsCycle()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldNotBeEmpty();
        var allNodes = cycles.SelectMany(c => c).ToHashSet();
        allNodes.ShouldContain("A");
        allNodes.ShouldContain("B");
    }

    [Fact]
    public void DetectCycles_ThreeNodeCycle_ReturnsCycle()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "C" },
            ["C"] = new HashSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldNotBeEmpty();
        var allNodes = cycles.SelectMany(c => c).ToHashSet();
        allNodes.ShouldContain("A");
        allNodes.ShouldContain("B");
        allNodes.ShouldContain("C");
    }

    [Fact]
    public void DetectCycles_SelfLoop_ReturnsCycle()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldNotBeEmpty();
    }

    [Fact]
    public void DetectCycles_MixedGraph_OnlyCyclicNodesReported()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "C" },
            ["C"] = new HashSet<string> { "B" },
            ["D"] = new HashSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldNotBeEmpty();
        var allNodes = cycles.SelectMany(c => c).ToHashSet();
        allNodes.ShouldContain("B");
        allNodes.ShouldContain("C");
    }

    [Fact]
    public void DetectCycles_TwoNodeCycle_ReportsExactlyOneCycle()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.Count.ShouldBe(1);
    }

    [Fact]
    public void DetectCycles_TwoSeparateCycles_ReportsBothCycles()
    {
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B" },
            ["B"] = new HashSet<string> { "A" },
            ["C"] = new HashSet<string> { "D" },
            ["D"] = new HashSet<string> { "C" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.Count.ShouldBe(2);
    }

    [Fact]
    public void DetectCycles_DiamondGraph_ReturnsNoCycles()
    {
        // A -> B, A -> C, B -> D, C -> D  — no cycles
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "B", "C" },
            ["B"] = new HashSet<string> { "D" },
            ["C"] = new HashSet<string> { "D" },
            ["D"] = new HashSet<string>(),
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldBeEmpty();
    }

    [Fact]
    public void DetectCycles_TwoCyclesWithNodeNamesAmbiguousWithoutSeparator_BothReported()
    {
        // Cycle 1: "A" <-> "BC"  (sorted+joined = "A->BC")
        // Cycle 2: "AB" <-> "C" (sorted+joined = "AB->C")
        // Without the "->" separator both keys collapse to "ABC", wrongly deduplicating them.
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new HashSet<string> { "BC" },
            ["BC"] = new HashSet<string> { "A" },
            ["AB"] = new HashSet<string> { "C" },
            ["C"] = new HashSet<string> { "AB" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.Count.ShouldBe(2);
    }

    [Fact]
    public void DetectCycles_DeadEndBranchBeforeCycleEdge_CycleDoesNotIncludeDeadEndNode()
    {
        // A → B (dead end, "B" < "C" so SortedSet iterates it first)
        // A → C → A (the actual cycle: [A, C])
        // Without the path.RemoveAt backtracking step, the dead-end node "B" stays
        // in the path when "C" is visited and leaks into the reported cycle.
        var dependencies = new Dictionary<string, IReadOnlySet<string>>
        {
            ["A"] = new SortedSet<string> { "B", "C" }, // deterministic order: B before C
            ["B"] = new SortedSet<string>(),
            ["C"] = new SortedSet<string> { "A" },
        };

        var cycles = CycleDetector.DetectCycles(dependencies);

        cycles.ShouldHaveSingleItem();
        cycles[0].ShouldContain("A");
        cycles[0].ShouldContain("C");
        cycles[0].ShouldNotContain("B");
    }
}
