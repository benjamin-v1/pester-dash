using PesterDash.Core.Models;
using PesterDash.Core.Parsing;

namespace PesterDash.Core.Tests.Parsing;

public class NUnitResultParserTests
{
    private readonly NUnitResultParser _sut = new();

    [Fact]
    public async Task ParseAsync_ReadsSummaryAndTestCases()
    {
        var fixture = GetFixturePath("TestResults.xml");

        var result = await _sut.ParseAsync(fixture);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, result.Tests.Count);
    }

    [Fact]
    public async Task ParseAsync_ReadsSkippedTests()
    {
        var fixture = GetFixturePath("TestResults.xml");

        var result = await _sut.ParseAsync(fixture);
        var skipped = result.Tests.Single(test => test.Outcome == TestOutcome.Skipped);

        Assert.Equal("Get-Greeting.is pending", skipped.Name);
    }

    [Fact]
    public async Task ParseAsync_ReadsFailureDetails()
    {
        var fixture = GetFixturePath("TestResults.Failed.xml");

        var result = await _sut.ParseAsync(fixture);
        var failed = result.FailedTests.Single();

        Assert.Equal("Sample.Should fail", failed.Name);
        Assert.Equal(TestOutcome.Failed, failed.Outcome);
        Assert.Contains("Expected 1", failed.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("line 10", failed.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_ThrowsWhenFileMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.ParseAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml")));
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
