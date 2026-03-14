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
                ResolveAndAddDeps(
                    type.ReferencedTypeNames,
                    type.Namespace,
                    type.UsingDirectives,
                    key,
                    qualifiedTypeToKey,
                    deps);
            }

            efferentDeps[key] = deps;
        }
        // Build afferent coupling map: packageKey → set of keys that depend on it
        var afferentDeps = new Dictionary<string, HashSet<string>>();
        foreach (var key in grouped.Select(g => g.Key))
            afferentDeps[key] = new HashSet<string>();

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
    /// For each type reference name collected by the syntax walker, tries to
    /// resolve it to a project package key using the same rules the C# compiler
    /// applies: the type's own namespace is checked first (free access to
    /// siblings), followed by every explicit using directive in scope.
    /// Already-qualified names (containing a dot) are matched directly.
    /// Only references that resolve to a <em>different</em> package are added.
    /// </summary>
    private static void ResolveAndAddDeps(
        IReadOnlySet<string> referencedTypeNames,
        string currentNamespace,
        IReadOnlySet<string> usingDirectives,
        string currentKey,
        Dictionary<string, string> qualifiedTypeToKey,
        HashSet<string> deps)
    {
        foreach (var typeName in referencedTypeNames)
        {
            if (typeName.Contains('.'))
            {
                // Already qualified by the developer (e.g. "MyApp.Models.ModelA").
                // Look it up directly in the registry.
                if (qualifiedTypeToKey.TryGetValue(typeName, out var directKey) &&
                    directKey != currentKey)
                {
                    deps.Add(directKey);
                }
            }
            else
            {
                // Simple name — qualify using the current namespace first, then
                // each using directive.  This mirrors C# name resolution and
                // avoids counting a dependency for every package that happens to
                // contain a type with the same name.
                if (!string.IsNullOrEmpty(currentNamespace))
                    TryAddDep($"{currentNamespace}.{typeName}", currentKey, qualifiedTypeToKey, deps);

                foreach (var ns in usingDirectives)
                    TryAddDep($"{ns}.{typeName}", currentKey, qualifiedTypeToKey, deps);
            }
        }
    }

    private static void TryAddDep(
        string qualifiedName,
        string currentKey,
        Dictionary<string, string> qualifiedTypeToKey,
        HashSet<string> deps)
    {
        if (qualifiedTypeToKey.TryGetValue(qualifiedName, out var depKey) && depKey != currentKey)
            deps.Add(depKey);
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

