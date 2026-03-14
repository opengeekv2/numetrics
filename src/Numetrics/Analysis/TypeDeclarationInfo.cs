namespace Numetrics.Analysis;

internal sealed record TypeDeclarationInfo(
    string Name,
    string Namespace,
    string AssemblyName,
    bool IsAbstract,
    IReadOnlySet<string> ReferencedTypeNames,
    IReadOnlySet<string> UsingDirectives)
    : ITypeDeclarationInfo;
