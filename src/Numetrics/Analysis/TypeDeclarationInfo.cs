namespace Numetrics.Analysis;

internal record TypeDeclarationInfo(
    string Name,
    string Namespace,
    string AssemblyName,
    bool IsAbstract,
    IReadOnlySet<string> UsingDirectives);
