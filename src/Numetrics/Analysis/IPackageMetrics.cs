namespace Numetrics.Analysis;

internal interface IPackageMetrics
{
    string Name { get; }

    int TypeCount { get; }

    int AbstractTypeCount { get; }

    int AfferentCouplings { get; }

    int EfferentCouplings { get; }

    double Abstractness { get; }

    double Instability { get; }

    double Distance { get; }

    IReadOnlyList<IReadOnlyList<string>> Cycles { get; }
}
