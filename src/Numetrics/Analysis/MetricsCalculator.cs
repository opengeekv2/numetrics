namespace Numetrics.Analysis;

internal static class MetricsCalculator
{
    internal static IReadOnlyList<PackageMetrics> ComputeNamespaceMetrics(
        IReadOnlyList<TypeDeclarationInfo> types)
    {
        return ComputeMetrics(types, t => t.Namespace);
    }

    internal static IReadOnlyList<PackageMetrics> ComputeAssemblyMetrics(
        IReadOnlyList<TypeDeclarationInfo> types)
    {
        return ComputeMetrics(types, t => t.AssemblyName);
    }

    private static IReadOnlyList<PackageMetrics> ComputeMetrics(
        IReadOnlyList<TypeDeclarationInfo> types,
        Func<TypeDeclarationInfo, string> groupKeySelector)
    {
        // Build a qualified-name → package-key map:
        //   key = "Namespace.TypeName"  (the fully qualified type name)
        //   value = the package key (namespace or assembly name)
        //
        // This allows the dependency resolver to do exact, unambiguous lookups
        // instead of guessing based on simple (potentially colliding) type names.
        var qualifiedTypeToKey = BuildQualifiedTypeMap(types, groupKeySelector);

        var grouped = types.GroupBy(groupKeySelector).ToList();

        // Build efferent dependency map: packageKey → set of dependency keys
        var efferentDeps = new Dictionary<string, HashSet<string>>();
        foreach (var group in grouped)
        {
            var key = group.Key;
            var deps = new HashSet<string>();

            foreach (var type in group)
            {
                ResolveAndAddDeps(type.ReferencedTypeNames, key, qualifiedTypeToKey, deps);
            }

            efferentDeps[key] = deps;
        }

        // Build afferent coupling map: packageKey → set of keys that depend on it
        var afferentDeps = new Dictionary<string, HashSet<string>>();
        foreach (var key in grouped.Select(g => g.Key))
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

    /// <summary>
    /// Builds a map from fully-qualified type name ("Namespace.TypeName") to the
    /// package key produced by <paramref name="groupKeySelector"/>.
    /// Types in the global namespace are keyed by their simple name only.
    /// </summary>
    private static Dictionary<string, string> BuildQualifiedTypeMap(
        IReadOnlyList<TypeDeclarationInfo> types,
        Func<TypeDeclarationInfo, string> groupKeySelector)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var qualifiedName = string.IsNullOrEmpty(type.Namespace)
                ? type.Name
                : $"{type.Namespace}.{type.Name}";
            map[qualifiedName] = groupKeySelector(type);
        }

        return map;
    }

    /// <summary>
    /// For each fully-qualified type reference emitted by the semantic walker,
    /// looks it up in the project type registry and adds the owning package key
    /// to <paramref name="deps"/> if it differs from <paramref name="currentKey"/>.
    /// References that do not match any project type (i.e. external types) are
    /// silently ignored.
    /// </summary>
    private static void ResolveAndAddDeps(
        IReadOnlySet<string> referencedTypeNames,
        string currentKey,
        Dictionary<string, string> qualifiedTypeToKey,
        HashSet<string> deps)
    {
        foreach (var typeName in referencedTypeNames)
        {
            if (qualifiedTypeToKey.TryGetValue(typeName, out var depKey) && depKey != currentKey)
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
