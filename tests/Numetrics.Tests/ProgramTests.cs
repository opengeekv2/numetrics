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
}
