namespace Numetrics.Analysis;

internal interface ITypeDeclarationInfo
{
    string Name { get; }

    string Namespace { get; }

    string AssemblyName { get; }

    bool IsAbstract { get; }

    IReadOnlySet<string> UsingDirectives { get; }
}
