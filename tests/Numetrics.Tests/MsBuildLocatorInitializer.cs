using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Numetrics.Tests;

/// <summary>
/// Ensures <see cref="MSBuildLocator.RegisterDefaults"/> is called before any
/// test code runs.  The <see cref="ModuleInitializerAttribute"/> guarantees this
/// runs at assembly-load time — before the JIT can pre-load MSBuild assemblies
/// through a reference to <c>Microsoft.CodeAnalysis.Workspaces.MSBuild</c>.
/// </summary>
internal static class MsBuildLocatorInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }
}
