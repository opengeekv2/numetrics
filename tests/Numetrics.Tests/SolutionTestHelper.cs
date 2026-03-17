namespace Numetrics.Tests;

/// <summary>
/// Shared helpers for creating temporary solution and project structures used
/// across integration test classes.
/// </summary>
internal static class SolutionTestHelper
{
    /// <summary>
    /// Writes a minimal <c>.sln</c> file into <paramref name="directory"/> that
    /// references a single project at the given relative path, and returns the
    /// absolute path to the written <c>.sln</c> file.
    /// </summary>
    internal static string WriteSln(string directory, string projectName, string relativeProjectPath)
    {
        var slnPath = Path.Combine(directory, $"{projectName}.sln");
        var content = $$"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{projectName}}", "{{relativeProjectPath}}", "{00000000-0000-0000-0000-000000000001}"
            EndProject

            """;
        File.WriteAllText(slnPath, content);
        return slnPath;
    }
}
