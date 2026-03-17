using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Numetrics.Analysis;

namespace Numetrics;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        // MSBuildLocator.RegisterDefaults() MUST be called before any MSBuild
        // types are loaded by the JIT.  Keeping it as the very first statement
        // and delegating all workspace work to a NoInlining method prevents the
        // JIT from pre-loading MSBuild assemblies before the locator can redirect
        // their resolution.
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        return await RunAsync(args).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> RunAsync(string[] args)
    {
        var solutionPath = args.Length > 0 ? args[0] : FindSolutionFile(Directory.GetCurrentDirectory());

        if (solutionPath == null)
        {
            Console.Error.WriteLine("Error: no .sln or .slnx solution file found in the current directory.");
            return 1;
        }

        if (!File.Exists(solutionPath))
        {
            Console.Error.WriteLine($"Error: solution file not found: {solutionPath}");
            return 1;
        }

        var types = await CSharpFileScanner.LoadSolutionAsync(solutionPath).ConfigureAwait(false);

        if (types.Count == 0)
        {
            Console.WriteLine("No C# types found in the specified solution.");
            return 0;
        }

        var namespaceMetrics = MetricsCalculator.ComputeNamespaceMetrics(types);
        var assemblyMetrics = MetricsCalculator.ComputeAssemblyMetrics(types);

        Console.WriteLine("=== Namespace Metrics ===");
        PrintMetricsTable(namespaceMetrics);

        Console.WriteLine();
        Console.WriteLine("=== Assembly Metrics ===");
        PrintMetricsTable(assemblyMetrics);

        return 0;
    }

    private static string? FindSolutionFile(string directory)
    {
        var candidates = Directory.GetFiles(directory, "*.sln")
            .Concat(Directory.GetFiles(directory, "*.slnx"))
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static void PrintMetricsTable(IReadOnlyList<PackageMetrics> metrics)
    {
        var sorted = metrics.OrderBy(m => m.Name).ToList();

        Console.WriteLine($"{"Package",-50} {"NC",4} {"Ca",4} {"Ce",4} {"A",6} {"I",6} {"D",6}");
        Console.WriteLine(new string('-', 80));

        foreach (var m in sorted)
        {
            Console.WriteLine(
                $"{m.Name,-50} {m.TypeCount,4} {m.AfferentCouplings,4} {m.EfferentCouplings,4} {m.Abstractness,6:F2} {m.Instability,6:F2} {m.Distance,6:F2}");

            foreach (var cycle in m.Cycles)
            {
                Console.WriteLine($"  [cycle] {string.Join(" -> ", cycle)}");
            }
        }
    }
}
