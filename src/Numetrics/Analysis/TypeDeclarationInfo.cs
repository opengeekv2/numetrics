namespace Numetrics.Analysis;

internal sealed record TypeDeclarationInfo(
    string Name,
    string Namespace,
    string AssemblyName,
    bool IsAbstract,
    IReadOnlySet<string> UsingDirectives)
    : ITypeDeclarationInfo;
