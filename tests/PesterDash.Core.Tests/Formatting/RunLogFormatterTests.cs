using PesterDash.Core.Formatting;

namespace PesterDash.Core.Tests.Formatting;

public class RunLogFormatterTests
{
    [Fact]
    public void StripAnsi_RemovesColorCodes()
    {
        var input = "\u001b[31;1mWrite-Error:\u001b[0m Failed to import module";

        var result = RunLogFormatter.StripAnsi(input);

        Assert.Equal("Write-Error: Failed to import module", result);
    }

    [Fact]
    public void GetStderrSummary_ReturnsFirstMeaningfulLine()
    {
        var stderr = "\u001b[31;1mWrite-Error:\u001b[0m Failed to import module at temp path.\n";

        var summary = RunLogFormatter.GetStderrSummary(stderr);

        Assert.Equal("Write-Error: Failed to import module at temp path.", summary);
    }
}
