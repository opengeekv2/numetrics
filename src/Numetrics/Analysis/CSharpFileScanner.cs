using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Numetrics.Analysis;

internal static class CSharpFileScanner
{
    internal static IReadOnlyList<TypeDeclarationInfo> AnalyzeSyntaxTrees(
        IEnumerable<(SyntaxTree Tree, string AssemblyName)> trees)
    {
        var (types, _) = AnalyzeSyntaxTreesWithGlobalUsings(trees);
        return types;
    }

    internal static (IReadOnlyList<TypeDeclarationInfo> Types, IReadOnlySet<string> GlobalUsings)
        AnalyzeSyntaxTreesWithGlobalUsings(IEnumerable<(SyntaxTree Tree, string AssemblyName)> trees)
    {
        var allTypes = new List<TypeDeclarationInfo>();
        var globalUsings = new HashSet<string>();

        foreach (var (tree, assemblyName) in trees)
        {
            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Collect global usings from this file
            foreach (var usingDirective in root.Usings)
            {
                if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                {
                    var nameText = GetUsingName(usingDirective);
                    if (nameText != null)
                    {
                        globalUsings.Add(nameText);
                    }
                }
            }

            // Collect file-level (non-global) using directives
            var fileUsings = root.Usings
                .Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                .Select(GetUsingName)
                .OfType<string>()
                .ToHashSet();

            // Process types in top-level namespace declarations
            foreach (var memberDecl in root.Members)
            {
                switch (memberDecl)
                {
                    case FileScopedNamespaceDeclarationSyntax fileScopedNs:
                        var nsName = fileScopedNs.Name.ToString();
                        var nsUsings = fileUsings
                            .Concat(fileScopedNs.Usings.Select(GetUsingName).OfType<string>())
                            .ToHashSet();
                        ExtractTypesFromMembers(fileScopedNs.Members, nsName, assemblyName, nsUsings, allTypes);
                        break;

                    case NamespaceDeclarationSyntax blockNs:
                        var blockNsName = blockNs.Name.ToString();
                        var blockNsUsings = fileUsings
                            .Concat(blockNs.Usings.Select(GetUsingName).OfType<string>())
                            .ToHashSet();
                        ExtractTypesFromMembers(blockNs.Members, blockNsName, assemblyName, blockNsUsings, allTypes);
                        break;

                    default:
                        // Types declared outside any namespace (global namespace)
                        if (memberDecl is TypeDeclarationSyntax globalType)
                        {
                            allTypes.Add(CreateTypeInfo(globalType, string.Empty, assemblyName, fileUsings));
                        }

                        break;
                }
            }
        }

        return (allTypes, globalUsings);
    }

    internal static (IReadOnlyList<TypeDeclarationInfo> Types, IReadOnlySet<string> GlobalUsings)
        ScanDirectory(string directoryPath)
    {
        var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

        // Determine assembly names from .csproj files
        var assemblyMap = BuildAssemblyMap(directoryPath, csFiles);

        var trees = csFiles.Select(file =>
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var assembly = assemblyMap.TryGetValue(file, out var asmName) ? asmName : Path.GetFileName(directoryPath);
            return (tree, assembly);
        });

        return AnalyzeSyntaxTreesWithGlobalUsings(trees);
    }

    private static Dictionary<string, string> BuildAssemblyMap(string basePath, string[] csFiles)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var csprojFiles = Directory.GetFiles(basePath, "*.csproj", SearchOption.AllDirectories);
        foreach (var csproj in csprojFiles)
        {
            var projectDir = Path.GetDirectoryName(csproj) ?? basePath;
            var assemblyName = Path.GetFileNameWithoutExtension(csproj);

            foreach (var csFile in csFiles)
            {
                if (csFile.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
                {
                    result[csFile] = assemblyName;
                }
            }
        }

        return result;
    }

    private static void ExtractTypesFromMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        string namespaceName,
        string assemblyName,
        HashSet<string> usingDirectives,
        List<TypeDeclarationInfo> result)
    {
        foreach (var member in members)
        {
            if (member is TypeDeclarationSyntax typeDecl)
            {
                result.Add(CreateTypeInfo(typeDecl, namespaceName, assemblyName, usingDirectives));
            }
        }
    }

    private static TypeDeclarationInfo CreateTypeInfo(
        TypeDeclarationSyntax typeDecl,
        string namespaceName,
        string assemblyName,
        HashSet<string> usingDirectives)
    {
        var isAbstract = typeDecl is InterfaceDeclarationSyntax ||
                         typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));

        return new TypeDeclarationInfo(
            typeDecl.Identifier.Text,
            namespaceName,
            assemblyName,
            isAbstract,
            usingDirectives);
    }

    private static string? GetUsingName(UsingDirectiveSyntax usingDirective)
    {
        if (usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
        {
            return null;
        }

        if (usingDirective.Alias != null)
        {
            return null;
        }

        var name = usingDirective.NamespaceOrType?.ToString();
        if (name != null && name.StartsWith("global::", StringComparison.Ordinal))
        {
            return name["global::".Length..];
        }

        return name;
    }
}
