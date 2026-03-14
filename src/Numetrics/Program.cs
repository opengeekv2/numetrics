using Numetrics.Analysis;

namespace Numetrics;

internal static class Program
{
    internal static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Error: directory not found: {path}");
            return 1;
        }

        var (types, globalUsings) = CSharpFileScanner.ScanDirectory(path);

        if (types.Count == 0)
        {
            Console.WriteLine("No C# types found in the specified path.");
            return 0;
        }

        var namespaceMetrics = MetricsCalculator.ComputeNamespaceMetrics(types, globalUsings);
        var assemblyMetrics = MetricsCalculator.ComputeAssemblyMetrics(types, globalUsings);

        Console.WriteLine("=== Namespace Metrics ===");
        PrintMetricsTable(namespaceMetrics);

        Console.WriteLine();
        Console.WriteLine("=== Assembly Metrics ===");
        PrintMetricsTable(assemblyMetrics);

        return 0;
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

            if (m.Cycles.Count > 0)
            {
                foreach (var cycle in m.Cycles)
                {
                    Console.WriteLine($"  [cycle] {string.Join(" -> ", cycle)}");
                }
            }
        }
    }
}
