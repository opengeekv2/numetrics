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

        // Build one compilation from all trees so the semantic model can resolve
        // cross-file type references within the project.
        var compilation = CSharpCompilation.Create(
            "NumetricsAnalysis",
            syntaxTrees: treeList.Select(t => t.Tree),
            references: GetFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var allTypes = new List<TypeDeclarationInfo>();
        foreach (var (tree, assemblyName) in treeList)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            foreach (var memberDecl in root.Members)
            {
                switch (memberDecl)
                {
                    case FileScopedNamespaceDeclarationSyntax fileScopedNs:
                        ExtractTypesFromMembers(
                            fileScopedNs.Members,
                            fileScopedNs.Name.ToString(),
                            assemblyName,
                            semanticModel,
                            allTypes);
                        break;

                    case NamespaceDeclarationSyntax blockNs:
                        ExtractTypesFromMembers(
                            blockNs.Members,
                            blockNs.Name.ToString(),
                            assemblyName,
                            semanticModel,
                            allTypes);
                        break;

                    default:
                        if (memberDecl is TypeDeclarationSyntax globalType)
                        {
                            allTypes.Add(CreateTypeInfo(globalType, string.Empty, assemblyName, semanticModel));
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

    private static IEnumerable<MetadataReference> GetFrameworkReferences()
    {
        // Only the base runtime assembly is required.  All project-internal types
        // are resolved directly from the compilation's own syntax trees; external
        // types are not project dependencies and are filtered out by the calculator.
        // Limiting references to the essentials avoids loading the full platform
        // assembly set, which would otherwise add unnecessary overhead.
        return new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
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
        SemanticModel semanticModel,
        List<TypeDeclarationInfo> result)
    {
        foreach (var member in members)
        {
            if (member is TypeDeclarationSyntax typeDecl)
            {
                result.Add(CreateTypeInfo(typeDecl, namespaceName, assemblyName, semanticModel));
            }
        }
    }

    private static TypeDeclarationInfo CreateTypeInfo(
        TypeDeclarationSyntax typeDecl,
        string namespaceName,
        string assemblyName,
        SemanticModel semanticModel)
    {
        var isAbstract = typeDecl is InterfaceDeclarationSyntax ||
                         typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));

        var walker = new TypeReferenceWalker(semanticModel);
        walker.Visit(typeDecl);

        return new TypeDeclarationInfo(
            typeDecl.Identifier.Text,
            namespaceName,
            assemblyName,
            isAbstract,
            walker.Names);
    }

    /// <summary>
    /// Walks a type declaration and collects every referenced type name as a
    /// fully-qualified string (e.g. <c>"MyApp.Models.ModelA"</c>) using the
    /// Roslyn semantic model.  Types that the compiler cannot resolve (e.g.
    /// references to missing external packages) are silently ignored — they
    /// are not project types and therefore cannot contribute to coupling.
    /// </summary>
    private sealed class TypeReferenceWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel semanticModel;
        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        public TypeReferenceWalker(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
        }

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

            // Ask the semantic model for the resolved symbol.  This gives us a
            // fully-qualified name (e.g. "MyApp.Models.ModelA").  If the type
            // cannot be resolved it is external (e.g. a missing package reference)
            // and is silently ignored — it is not a project type and cannot
            // contribute to coupling.
            var symbol = this.semanticModel.GetTypeInfo(type).Type;
            if (symbol == null || symbol.TypeKind == TypeKind.Error)
            {
                return;
            }

            this.AddSymbolRecursively(symbol);
        }

        private void AddSymbolRecursively(ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case INamedTypeSymbol namedType:
                    // Use the open-generic definition so that e.g. List<ModelA> and
                    // List<ServiceB> both contribute a dependency on List<T> (which
                    // is external and will be filtered) plus their type arguments.
                    var definition = namedType.IsGenericType
                        ? namedType.OriginalDefinition
                        : namedType;
                    this.AddQualifiedName(definition);

                    foreach (var arg in namedType.TypeArguments)
                    {
                        this.AddSymbolRecursively(arg);
                    }

                    break;

                case IArrayTypeSymbol arrayType:
                    this.AddSymbolRecursively(arrayType.ElementType);
                    break;

                case IPointerTypeSymbol pointerType:
                    this.AddSymbolRecursively(pointerType.PointedAtType);
                    break;
            }
        }

        private void AddQualifiedName(INamedTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            var qualifiedName = ns.IsGlobalNamespace
                ? symbol.Name
                : $"{ns.ToDisplayString()}.{symbol.Name}";
            this.names.Add(qualifiedName);
        }
    }
}
