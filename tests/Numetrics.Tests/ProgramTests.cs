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
}
