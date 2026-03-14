namespace Numetrics.Analysis;

internal static class MetricsCalculator
{
    internal static IReadOnlyList<PackageMetrics> ComputeNamespaceMetrics(
        IReadOnlyList<TypeDeclarationInfo> types,
        IReadOnlySet<string>? globalUsingDirectives = null)
    {
        var projectNamespaces = types.Select(t => t.Namespace).ToHashSet();
        return ComputeMetrics(
            types,
            t => t.Namespace,
            projectNamespaces,
            globalUsingDirectives);
    }

    internal static IReadOnlyList<PackageMetrics> ComputeAssemblyMetrics(
        IReadOnlyList<TypeDeclarationInfo> types,
        IReadOnlySet<string>? globalUsingDirectives = null)
    {
        // Build a mapping from namespace → assembly for all project types
        var namespaceToAssembly = types
            .GroupBy(t => t.Namespace)
            .ToDictionary(g => g.Key, g => g.First().AssemblyName);

        var projectAssemblies = types.Select(t => t.AssemblyName).ToHashSet();

        // Project global usings onto assembly names
        IReadOnlySet<string>? globalAssemblyUsings = null;
        if (globalUsingDirectives != null)
        {
            globalAssemblyUsings = globalUsingDirectives
                .Where(namespaceToAssembly.ContainsKey)
                .Select(ns => namespaceToAssembly[ns])
                .ToHashSet();
        }

        return ComputeMetrics(
            types,
            t => t.AssemblyName,
            projectAssemblies,
            globalAssemblyUsings,
            usingDirective =>
                namespaceToAssembly.TryGetValue(usingDirective, out var assembly) ? assembly : null);
    }

    private static IReadOnlyList<PackageMetrics> ComputeMetrics(
        IReadOnlyList<TypeDeclarationInfo> types,
        Func<TypeDeclarationInfo, string> groupKeySelector,
        IReadOnlySet<string> projectKeys,
        IReadOnlySet<string>? globalUsingDirectives,
        Func<string, string?>? usingDirectiveToKey = null)
    {
        usingDirectiveToKey ??= directive => projectKeys.Contains(directive) ? directive : null;

        var grouped = types.GroupBy(groupKeySelector).ToList();

        // Build efferent dependency map: key → set of dependency keys
        var efferentDeps = new Dictionary<string, HashSet<string>>();
        foreach (var group in grouped)
        {
            var key = group.Key;
            var deps = new HashSet<string>();

            foreach (var type in group)
            {
                AddDepsFromUsings(type.UsingDirectives, key, usingDirectiveToKey, deps);
            }

            if (globalUsingDirectives != null)
            {
                AddDepsFromUsings(globalUsingDirectives, key, usingDirectiveToKey, deps);
            }

            efferentDeps[key] = deps;
        }

        // Build afferent coupling map: key → set of keys that depend on it
        var afferentDeps = new Dictionary<string, HashSet<string>>();
        foreach (var key in projectKeys)
        {
            afferentDeps[key] = new HashSet<string>();
        }

        foreach (var entry in efferentDeps)
        {
            foreach (var dep in entry.Value)
            {
                if (!afferentDeps.TryGetValue(dep, out var afferent))
                {
                    afferent = new HashSet<string>();
                    afferentDeps[dep] = afferent;
                }

                afferent.Add(entry.Key);
            }
        }

        // Detect cycles across all packages
        var allDependencies = efferentDeps.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlySet<string>)kv.Value);
        var cycles = CycleDetector.DetectCycles(allDependencies);
        var cyclesByNode = BuildCyclesByNode(cycles);

        // Compute metrics for each package
        var result = new List<PackageMetrics>();
        foreach (var group in grouped)
        {
            var key = group.Key;
            var typeCount = group.Count();
            var abstractCount = group.Count(t => t.IsAbstract);

            var ce = efferentDeps.TryGetValue(key, out var effs) ? effs.Count : 0;
            var ca = afferentDeps.TryGetValue(key, out var affs) ? affs.Count : 0;

            var abstractness = typeCount > 0 ? (double)abstractCount / typeCount : 0.0;
            var instability = ((ce + ca) > 0) ? ((double)ce / (ce + ca)) : 0.0;
            var distance = Math.Abs(abstractness + instability - 1.0);

            var nodeCycles = cyclesByNode.TryGetValue(key, out var nc) ? nc : new List<IReadOnlyList<string>>();

            result.Add(new PackageMetrics(
                key,
                typeCount,
                abstractCount,
                ca,
                ce,
                abstractness,
                instability,
                distance,
                nodeCycles));
        }

        return result;
    }

    private static void AddDepsFromUsings(
        IReadOnlySet<string> usings,
        string currentKey,
        Func<string, string?> usingDirectiveToKey,
        HashSet<string> deps)
    {
        foreach (var directive in usings)
        {
            var depKey = usingDirectiveToKey(directive);
            if (depKey != null && depKey != currentKey)
            {
                deps.Add(depKey);
            }
        }
    }

    private static Dictionary<string, IReadOnlyList<IReadOnlyList<string>>> BuildCyclesByNode(
        IReadOnlyList<IReadOnlyList<string>> cycles)
    {
        var result = new Dictionary<string, List<IReadOnlyList<string>>>();
        foreach (var cycle in cycles)
        {
            foreach (var node in cycle)
            {
                if (!result.TryGetValue(node, out var nodeCycles))
                {
                    nodeCycles = new List<IReadOnlyList<string>>();
                    result[node] = nodeCycles;
                }

                nodeCycles.Add(cycle);
            }
        }

        return result.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<IReadOnlyList<string>>)kv.Value);
    }
}
