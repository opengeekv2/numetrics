namespace Numetrics.Analysis;

internal static class CycleDetector
{
    internal static IReadOnlyList<IReadOnlyList<string>> DetectCycles(
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();
        var cycles = new List<IReadOnlyList<string>>();
        var reportedCycles = new HashSet<string>();

        foreach (var node in dependencies.Keys)
        {
            if (!visited.Contains(node))
            {
                Dfs(node, dependencies, visited, recursionStack, path, cycles, reportedCycles);
            }
        }

        return cycles;
    }

    private static void Dfs(
        string node,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<IReadOnlyList<string>> cycles,
        HashSet<string> reportedCycles)
    {
        visited.Add(node);
        recursionStack.Add(node);
        path.Add(node);

        if (dependencies.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    Dfs(neighbor, dependencies, visited, recursionStack, path, cycles, reportedCycles);
                }
                else if (recursionStack.Contains(neighbor))
                {
                    var cycleStart = path.IndexOf(neighbor);
                    var cycle = path.Skip(cycleStart).ToList();

                    var cycleKey = string.Join("->", cycle.OrderBy(n => n));
                    if (reportedCycles.Add(cycleKey))
                    {
                        cycles.Add(cycle);
                    }
                }
            }
        }

        recursionStack.Remove(node);
        path.RemoveAt(path.Count - 1);
    }
}
