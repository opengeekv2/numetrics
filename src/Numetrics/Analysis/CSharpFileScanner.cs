using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Numetrics.Analysis;

internal static class CSharpFileScanner
{
    /// <summary>
    /// Loads every project in the solution at <paramref name="solutionFilePath"/>
    /// using the Roslyn MSBuild workspace, obtains each project's fully-evaluated
    /// <see cref="Compilation"/> (which includes all NuGet and framework references),
    /// and extracts type declaration information from every source file.
    /// </summary>
    internal static async Task<IReadOnlyList<TypeDeclarationInfo>> LoadSolutionAsync(string solutionFilePath)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath).ConfigureAwait(false);

        var allTypes = new List<TypeDeclarationInfo>();
        foreach (var project in solution.Projects)
        {
            // Only C# projects yield a CSharpCompilation; skip others (e.g. F#).
            if (await project.GetCompilationAsync().ConfigureAwait(false) is not CSharpCompilation compilation)
            {
                continue;
            }

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                AnalyzeTree(syntaxTree, project.AssemblyName, semanticModel, allTypes);
            }
        }

        return allTypes;
    }

    /// <summary>
    /// Builds a <see cref="CSharpCompilation"/> from the provided raw syntax trees
    /// and analyzes each tree.  This path is used by unit tests that supply
    /// in-memory source code without going through the MSBuild workspace.
    /// </summary>
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
            AnalyzeTree(tree, assemblyName, semanticModel, allTypes);
        }

        return allTypes;
    }

    // Walks the top-level members of a single syntax tree and appends any
    // TypeDeclarationInfo objects found to <paramref name="result"/>.
    private static void AnalyzeTree(
        SyntaxTree syntaxTree,
        string assemblyName,
        SemanticModel semanticModel,
        List<TypeDeclarationInfo> result)
    {
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();

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
                        result);
                    break;

                case NamespaceDeclarationSyntax blockNs:
                    ExtractTypesFromMembers(
                        blockNs.Members,
                        blockNs.Name.ToString(),
                        assemblyName,
                        semanticModel,
                        result);
                    break;

                default:
                    if (memberDecl is TypeDeclarationSyntax globalType)
                    {
                        result.Add(CreateTypeInfo(globalType, string.Empty, assemblyName, semanticModel));
                    }

                    break;
            }
        }
    }

    private static IEnumerable<MetadataReference> GetFrameworkReferences()
    {
        // Only the base runtime assembly is required for in-memory compilations
        // used by AnalyzeSyntaxTrees.  All project-internal types are resolved
        // directly from the compilation's own syntax trees; external types are
        // not project types and are filtered out by the calculator.
        return new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
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

        public IReadOnlySet<string> Names => names;

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitVariableDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitEventDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            CollectTypeName(node.ReturnType);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            CollectTypeName(node.ReturnType);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            CollectTypeName(node.ReturnType);
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            CollectTypeName(node.ReturnType);
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            if (node.Type != null)
            {
                CollectTypeName(node.Type);
            }

            base.VisitParameter(node);
        }

        public override void VisitBaseList(BaseListSyntax node)
        {
            foreach (var type in node.Types)
            {
                CollectTypeName(type.Type);
            }

            base.VisitBaseList(node);
        }

        public override void VisitTypeConstraint(TypeConstraintSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitTypeConstraint(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitCastExpression(node);
        }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitTypeOfExpression(node);
        }

        public override void VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitDefaultExpression(node);
        }

        public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitSizeOfExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.AsExpression) && node.Right is TypeSyntax asType)
            {
                CollectTypeName(asType);
            }

            base.VisitBinaryExpression(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            // AttributeSyntax.Name is a NameSyntax which is also a TypeSyntax.
            CollectTypeName(node.Name);
            base.VisitAttribute(node);
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitDeclarationPattern(node);
        }

        public override void VisitDeclarationExpression(DeclarationExpressionSyntax node)
        {
            CollectTypeName(node.Type);
            base.VisitDeclarationExpression(node);
        }

        public override void VisitTypePattern(TypePatternSyntax node)
        {
            CollectTypeName(node.Type);
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
            var symbol = semanticModel.GetTypeInfo(type).Type;
            if (symbol == null || symbol.TypeKind == TypeKind.Error)
            {
                return;
            }

            AddSymbolRecursively(symbol);
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
                    AddQualifiedName(definition);

                    foreach (var arg in namedType.TypeArguments)
                    {
                        AddSymbolRecursively(arg);
                    }

                    break;

                case IArrayTypeSymbol arrayType:
                    AddSymbolRecursively(arrayType.ElementType);
                    break;

                case IPointerTypeSymbol pointerType:
                    AddSymbolRecursively(pointerType.PointedAtType);
                    break;
            }
        }

        private void AddQualifiedName(INamedTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            var qualifiedName = ns.IsGlobalNamespace
                ? symbol.Name
                : $"{ns.ToDisplayString()}.{symbol.Name}";
            names.Add(qualifiedName);
        }
    }
}
