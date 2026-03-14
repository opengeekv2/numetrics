namespace Numetrics.Analysis;

internal record PackageMetrics(
    string Name,
    int TypeCount,
    int AbstractTypeCount,
    int AfferentCouplings,
    int EfferentCouplings,
    double Abstractness,
    double Instability,
    double Distance,
    IReadOnlyList<IReadOnlyList<string>> Cycles);
