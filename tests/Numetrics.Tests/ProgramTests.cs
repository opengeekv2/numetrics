namespace Numetrics.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_ReturnsZero()
    {
        var result = Program.Main([]);

        result.ShouldBe(0);
    }

    [Fact]
    public void Main_WithEmptyDirectory_WritesNoTypesFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var originalOut = Console.Out;
            try
            {
                var writer = new StringWriter();
                Console.SetOut(writer);

                Program.Main([tempDir]);

                writer.ToString().ShouldContain("No C# types found");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void Main_WithInvalidPath_ReturnsOne()
    {
        var result = Program.Main(["/nonexistent/path/that/does/not/exist"]);

        result.ShouldBe(1);
    }

    [Fact]
    public void Main_WithTypesFound_PrintsNamespaceAndAssemblyHeaders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "Test.cs"),
                "namespace MyApp; class MyType { }");
            File.WriteAllText(
                Path.Combine(tempDir, "Test.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");

            var originalOut = Console.Out;
            try
            {
                var writer = new StringWriter();
                Console.SetOut(writer);

                Program.Main([tempDir]);

                var output = writer.ToString();
                output.ShouldContain("Namespace Metrics");
                output.ShouldContain("Assembly Metrics");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithCycle_PrintsCycleOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "A.cs"),
                "using MyApp.B; namespace MyApp.A; class TypeA { }");
            File.WriteAllText(
                Path.Combine(tempDir, "B.cs"),
                "using MyApp.A; namespace MyApp.B; class TypeB { }");
            File.WriteAllText(
                Path.Combine(tempDir, "Test.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");

            var originalOut = Console.Out;
            try
            {
                var writer = new StringWriter();
                Console.SetOut(writer);

                Program.Main([tempDir]);

                writer.ToString().ShouldContain("[cycle]");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithInvalidPath_WritesErrorMessageToStderr()
    {
        var originalError = Console.Error;
        try
        {
            var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            Program.Main(["/nonexistent/path/that/does/not/exist"]);

            errorWriter.ToString().ShouldContain("Error");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Main_WithInvalidPath_ErrorMessageContainsPath()
    {
        const string invalidPath = "/nonexistent/path/that/does/not/exist";
        var originalError = Console.Error;
        try
        {
            var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            Program.Main([invalidPath]);

            errorWriter.ToString().ShouldContain(invalidPath);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Main_WithTypesFound_HasBlankLineBetweenSections()
    {
        var tempDir = CreateTempDirWithSingleType();
        try
        {
            var output = CaptureStdout(() => Program.Main([tempDir]));

            // A blank line separates the namespace table from the assembly section header.
            output.ShouldContain(Environment.NewLine + Environment.NewLine + "=== Assembly Metrics ===");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithTypesFound_PrintsAssemblyMetricsTableContent()
    {
        var tempDir = CreateTempDirWithSingleType();
        try
        {
            var output = CaptureStdout(() => Program.Main([tempDir]));

            // The assembly table must appear AFTER the "=== Assembly Metrics ===" header.
            var assemblySection = output.Substring(output.IndexOf("Assembly Metrics", StringComparison.Ordinal));
            assemblySection.ShouldContain("MyProject");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithMultiplePackages_PrintsInAlphabeticalOrder()
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

            var output = CaptureStdout(() => Program.Main([tempDir]));

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
    public void Main_WithTypesFound_PrintsColumnHeaderLabels()
    {
        var tempDir = CreateTempDirWithSingleType();
        try
        {
            var output = CaptureStdout(() => Program.Main([tempDir]));

            // All seven column labels must appear in order. The regex is case-sensitive,
            // which distinguishes single-letter columns ("A", "I", "D") from letters that
            // incidentally appear in other output words.
            output.ShouldMatch(@"Package\s+NC\s+Ca\s+Ce\s+A\s+I\s+D");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithTypesFound_PrintsHeaderSeparatorLine()
    {
        var tempDir = CreateTempDirWithSingleType();
        try
        {
            var output = CaptureStdout(() => Program.Main([tempDir]));

            // Separator is a line of 80 dashes
            output.ShouldContain(new string('-', 80));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithTypesFound_PrintsMetricValuesInRow()
    {
        var tempDir = CreateTempDirWithSingleType();
        try
        {
            var output = CaptureStdout(() => Program.Main([tempDir]));

            // Concrete type with no couplings: abstractness=0.00, instability=0.00
            output.ShouldContain("0.00");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Main_WithCycle_PrintsCycleWithArrowSeparator()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "A.cs"),
                "using MyApp.B; namespace MyApp.A; class TypeA { }");
            File.WriteAllText(
                Path.Combine(tempDir, "B.cs"),
                "using MyApp.A; namespace MyApp.B; class TypeB { }");
            File.WriteAllText(
                Path.Combine(tempDir, "Test.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");

            var output = CaptureStdout(() => Program.Main([tempDir]));

            // Cycle must use " -> " as node separator, not an empty string
            output.ShouldContain(" -> ");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirWithSingleType()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "Code.cs"), "namespace MyProject; class MyType { }");
        File.WriteAllText(
            Path.Combine(tempDir, "MyProject.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        return tempDir;
    }

    private static string CaptureStdout(Action action)
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
