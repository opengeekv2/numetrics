using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Numetrics.Analysis;

internal static class CSharpFileScanner
{
    internal static IReadOnlyList<TypeDeclarationInfo> AnalyzeSyntaxTrees(
        IEnumerable<(SyntaxTree Tree, string AssemblyName)> trees)
    {
        var treeList = trees.ToList();

        // First pass: collect global using directives so they can be added to every
        // type's resolution context (mirrors how the C# compiler applies them).
        var globalUsings = new HashSet<string>();
        foreach (var (tree, _) in treeList)
        {
            var root = (CompilationUnitSyntax)tree.GetRoot();
            foreach (var u in root.Usings)
            {
                if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                {
                    var name = GetUsingName(u);
                    if (name != null)
                    {
                        globalUsings.Add(name);
                    }
                }
            }
        }

        // Second pass: extract type declarations with their references and usings.
        var allTypes = new List<TypeDeclarationInfo>();
        foreach (var (tree, assemblyName) in treeList)
        {
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var fileUsings = root.Usings
                .Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                .Select(GetUsingName)
                .OfType<string>()
                .Concat(globalUsings)
                .ToHashSet();

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
                        if (memberDecl is TypeDeclarationSyntax globalType)
                        {
                            allTypes.Add(CreateTypeInfo(globalType, string.Empty, assemblyName, fileUsings));
                        }

                        break;
                }
            }
        }

        return allTypes;
    }

    internal static IReadOnlyList<TypeDeclarationInfo> ScanDirectory(string directoryPath)
    {
        var csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
        var assemblyMap = BuildAssemblyMap(directoryPath, csFiles);

        var trees = csFiles.Select(file =>
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var assembly = assemblyMap.TryGetValue(file, out var asmName) ? asmName : Path.GetFileName(directoryPath);
            return (tree, assembly);
        });

        return AnalyzeSyntaxTrees(trees);
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

        var walker = new TypeReferenceWalker();
        walker.Visit(typeDecl);

        return new TypeDeclarationInfo(
            typeDecl.Identifier.Text,
            namespaceName,
            assemblyName,
            isAbstract,
            walker.Names,
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

        return usingDirective.NamespaceOrType?.ToString();
    }

    /// <summary>
    /// Walks a type declaration syntax tree and collects every type name that appears
    /// in a type-reference position (fields, properties, method signatures, base lists,
    /// expressions, patterns, …). Only the syntactic names are recorded; name resolution
    /// against the project registry is deferred to <see cref="MetricsCalculator"/>.
    /// </summary>
    private sealed class TypeReferenceWalker : CSharpSyntaxWalker
    {
        private HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlySet<string> Names => this.names;

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitVariableDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitEventDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            this.CollectTypeName(node.ReturnType);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            this.CollectTypeName(node.ReturnType);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            this.CollectTypeName(node.ReturnType);
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            this.CollectTypeName(node.ReturnType);
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            if (node.Type != null)
            {
                this.CollectTypeName(node.Type);
            }

            base.VisitParameter(node);
        }

        public override void VisitBaseList(BaseListSyntax node)
        {
            foreach (var type in node.Types)
            {
                this.CollectTypeName(type.Type);
            }

            base.VisitBaseList(node);
        }

        public override void VisitTypeConstraint(TypeConstraintSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitTypeConstraint(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitCastExpression(node);
        }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitTypeOfExpression(node);
        }

        public override void VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitDefaultExpression(node);
        }

        public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitSizeOfExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.AsExpression) && node.Right is TypeSyntax asType)
            {
                this.CollectTypeName(asType);
            }

            base.VisitBinaryExpression(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            // AttributeSyntax.Name is a NameSyntax which is also a TypeSyntax.
            this.CollectTypeName(node.Name);
            base.VisitAttribute(node);
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitDeclarationPattern(node);
        }

        public override void VisitDeclarationExpression(DeclarationExpressionSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitDeclarationExpression(node);
        }

        public override void VisitTypePattern(TypePatternSyntax node)
        {
            this.CollectTypeName(node.Type);
            base.VisitTypePattern(node);
        }

        private void CollectTypeName(TypeSyntax? type)
        {
            if (type == null)
            {
                return;
            }

            switch (type)
            {
                case IdentifierNameSyntax id when id.Identifier.Text != "var":
                    this.names.Add(id.Identifier.Text);
                    break;

                case QualifiedNameSyntax qn:
                    this.names.Add(qn.ToString());
                    break;

                case GenericNameSyntax gn:
                    this.names.Add(gn.Identifier.Text);
                    foreach (var arg in gn.TypeArgumentList.Arguments)
                    {
                        this.CollectTypeName(arg);
                    }

                    break;

                case ArrayTypeSyntax arr:
                    this.CollectTypeName(arr.ElementType);
                    break;

                case NullableTypeSyntax nullable:
                    this.CollectTypeName(nullable.ElementType);
                    break;

                case TupleTypeSyntax tuple:
                    foreach (var element in tuple.Elements)
                    {
                        this.CollectTypeName(element.Type);
                    }

                    break;

                case PointerTypeSyntax pointer:
                    this.CollectTypeName(pointer.ElementType);
                    break;

                case RefTypeSyntax refType:
                    this.CollectTypeName(refType.Type);
                    break;
            }
        }
    }
}

