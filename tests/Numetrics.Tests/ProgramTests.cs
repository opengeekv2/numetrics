namespace Numetrics.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_ReturnsZero()
    {
        int result = Program.Main([]);

        result.ShouldBe(0);
    }

    [Fact]
    public Task Main_WritesOutput()
    {
        TextWriter originalOut = Console.Out;
        try
        {
            StringWriter writer = new StringWriter();
            Console.SetOut(writer);

            Program.Main([]);

            return Verify(writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
