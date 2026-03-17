namespace Numetrics.Tests;

public class ProgramTests
{
    [Fact]
    public async Task Main_ReturnsZero()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var result = await Program.Main([slnPath]);

            result.ShouldBe(0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNoProjects_WritesNoTypesFound()
    {
        // A solution that has no projects at all produces no types.
        var slnPath = CreateTempEmptySolution();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            output.ShouldContain("No C# types found");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNonExistentPath_ReturnsOne()
    {
        var result = await Program.Main(["/nonexistent/path/that/does/not/exist.sln"]);

        result.ShouldBe(1);
    }

    [Fact]
    public async Task Main_WithTypesFound_PrintsNamespaceAndAssemblyHeaders()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            output.ShouldContain("Namespace Metrics");
            output.ShouldContain("Assembly Metrics");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithCycle_PrintsCycleOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Each class holds a field whose type is declared in the other namespace,
            // creating a real structural dependency cycle detected by the semantic model.
            File.WriteAllText(
                Path.Combine(tempDir, "A.cs"),
                "namespace MyApp.A; class TypeA { MyApp.B.TypeB Field; }");
            File.WriteAllText(
                Path.Combine(tempDir, "B.cs"),
                "namespace MyApp.B; class TypeB { MyApp.A.TypeA Field; }");
            File.WriteAllText(
                Path.Combine(tempDir, "Test.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "Test", "Test.csproj");

            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            output.ShouldContain("[cycle]");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNonExistentPath_WritesErrorMessageToStderr()
    {
        var originalError = Console.Error;
        try
        {
            var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            await Program.Main(["/nonexistent/path/that/does/not/exist.sln"]);

            errorWriter.ToString().ShouldContain("Error");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Main_WithNonExistentPath_ErrorMessageContainsPath()
    {
        const string invalidPath = "/nonexistent/path/that/does/not/exist.sln";
        var originalError = Console.Error;
        try
        {
            var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            await Program.Main([invalidPath]);

            errorWriter.ToString().ShouldContain(invalidPath);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Main_WithTypesFound_HasBlankLineBetweenSections()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // A blank line separates the namespace table from the assembly section header.
            output.ShouldContain(Environment.NewLine + Environment.NewLine + "=== Assembly Metrics ===");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithTypesFound_PrintsAssemblyMetricsTableContent()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // The assembly table must appear AFTER the "=== Assembly Metrics ===" header.
            var assemblySection = output.Substring(output.IndexOf("Assembly Metrics", StringComparison.Ordinal));
            assemblySection.ShouldContain("MyProject");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithMultiplePackages_PrintsInAlphabeticalOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            // "Zoo" should come before "Apple" if sorted descending but after if ascending
            File.WriteAllText(Path.Combine(tempDir, "Z.cs"), "namespace Zoo; class ZClass { }");
            File.WriteAllText(Path.Combine(tempDir, "A.cs"), "namespace Apple; class AClass { }");
            File.WriteAllText(
                Path.Combine(tempDir, "T.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "T", "T.csproj");

            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            var appleIndex = output.IndexOf("Apple", StringComparison.Ordinal);
            var zooIndex = output.IndexOf("Zoo", StringComparison.Ordinal);
            appleIndex.ShouldBeLessThan(zooIndex);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithTypesFound_PrintsColumnHeaderLabels()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // All seven column labels must appear in order. The regex is case-sensitive,
            // which distinguishes single-letter columns ("A", "I", "D") from letters that
            // incidentally appear in other output words.
            output.ShouldMatch(@"Package\s+NC\s+Ca\s+Ce\s+A\s+I\s+D");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithTypesFound_PrintsHeaderSeparatorLine()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // Separator is a line of 80 dashes
            output.ShouldContain(new string('-', 80));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithTypesFound_PrintsMetricValuesInRow()
    {
        var slnPath = CreateTempSolutionWithSingleType();
        try
        {
            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // Concrete type with no couplings: abstractness=0.00, instability=0.00
            output.ShouldContain("0.00");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(slnPath) !, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithCycle_PrintsCycleWithArrowSeparator()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "A.cs"),
                "namespace MyApp.A; class TypeA { MyApp.B.TypeB Field; }");
            File.WriteAllText(
                Path.Combine(tempDir, "B.cs"),
                "namespace MyApp.B; class TypeB { MyApp.A.TypeA Field; }");
            File.WriteAllText(
                Path.Combine(tempDir, "Test.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
            var slnPath = SolutionTestHelper.WriteSln(tempDir, "Test", "Test.csproj");

            var output = await CaptureStdoutAsync(() => Program.Main([slnPath]));

            // Cycle must use " -> " as node separator, not an empty string
            output.ShouldContain(" -> ");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNoArgs_InDirectoryWithNoSolution_ReturnsOne()
    {
        // When called with no arguments, Main falls back to FindSolutionFile in
        // the current directory.  In a directory with no solution file it must
        // return 1 (and NOT throw an IndexOutOfRangeException from accessing
        // args[0] when args is empty).
        var originalDir = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var result = await Program.Main([]);

            result.ShouldBe(1);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNoArgs_InDirectoryWithNoSolution_WritesErrorMessageToStderr()
    {
        // Verifies that the "no solution found" error message is actually written
        // to stderr (kills statement-removal and string-replacement mutations of
        // the Console.Error.WriteLine call).
        var originalDir = Directory.GetCurrentDirectory();
        var originalError = Console.Error;
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            await Program.Main([]);

            errorWriter.ToString().ShouldContain("solution");
        }
        finally
        {
            Console.SetError(originalError);
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNoArgs_InDirectoryWithSingleSlnFile_FindsSolution()
    {
        // FindSolutionFile must return the path of the single .sln candidate so
        // that execution continues past the null-check.  The directory also
        // contains a non-solution file (Dummy.txt) so that the "*.sln" → "" string
        // mutation causes GetFiles("") to return >1 file and invalidate the result.
        var originalDir = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            File.WriteAllText(
                Path.Combine(tempDir, "Empty.sln"),
                $"Microsoft Visual Studio Solution File, Format Version 12.00{Environment.NewLine}");
            File.WriteAllText(Path.Combine(tempDir, "Dummy.txt"), string.Empty);

            var output = await CaptureStdoutAsync(() => Program.Main([]));

            // The solution was found and loaded (no projects → "No C# types found").
            output.ShouldContain("No C# types found");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Main_WithNoArgs_InDirectoryWithSingleSlnxFile_FindsSolution()
    {
        // FindSolutionFile must include .slnx files via Concat (not Except).
        // The directory also contains a non-solution file (Dummy.txt) so that the
        // "*.slnx" → "" string mutation causes GetFiles("") to return >1 file.
        var originalDir = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "Empty.slnx"), "<Solution />");
            File.WriteAllText(Path.Combine(tempDir, "Dummy.txt"), string.Empty);

            var output = await CaptureStdoutAsync(() => Program.Main([]));

            // The solution was found and loaded (no projects → "No C# types found").
            output.ShouldContain("No C# types found");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a temp directory containing a single-project solution with one
    /// concrete type, then returns the path to the <c>.sln</c> file.
    /// </summary>
    private static string CreateTempSolutionWithSingleType()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "Code.cs"), "namespace MyProject; class MyType { }");
        File.WriteAllText(
            Path.Combine(tempDir, "MyProject.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        return SolutionTestHelper.WriteSln(tempDir, "MyProject", "MyProject.csproj");
    }

    /// <summary>
    /// Creates a temp directory containing a solution file with no projects,
    /// then returns the path to the <c>.sln</c> file.
    /// </summary>
    private static string CreateTempEmptySolution()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var slnPath = Path.Combine(tempDir, "Empty.sln");
        File.WriteAllText(slnPath, $"Microsoft Visual Studio Solution File, Format Version 12.00{Environment.NewLine}");
        return slnPath;
    }

    private static async Task<string> CaptureStdoutAsync(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
