using PesterDash.Core.Models;
using PesterDash.Core.Parsing;

namespace PesterDash.Core.Tests.Parsing;

public class CoverageParserTests
{
    private readonly CoverageParser _sut = new();

    [Fact]
    public async Task ParseAsync_ReadsJaCoCoCounters()
    {
        var result = await _sut.ParseAsync(GetFixturePath("Coverage.jacoco.xml"));

        Assert.Equal(3, result.CoveredLines);
        Assert.Equal(4, result.TotalLines);
        Assert.Equal(75, result.LinePercent);
        Assert.Equal(3, result.CoveredBranches);
        Assert.Equal(4, result.TotalBranches);
        Assert.Equal(75, result.BranchPercent);
    }

    [Fact]
    public async Task ParseAsync_ReadsCoberturaAttributes()
    {
        var result = await _sut.ParseAsync(GetFixturePath("Coverage.cobertura.xml"));

        Assert.Equal(3, result.CoveredLines);
        Assert.Equal(4, result.TotalLines);
        Assert.Equal(1, result.CoveredBranches);
        Assert.Equal(2, result.TotalBranches);
    }

    [Fact]
    public async Task ParseAsync_ReadsJaCoCoSourceFiles()
    {
        var result = await _sut.ParseAsync(GetFixturePath("Coverage.jacoco.files.xml"));

        Assert.Equal(2, result.Files.Count);
        Assert.Equal("Greeting.ps1", result.Files[0].FileName);
        Assert.Equal(100, result.Files[0].LinePercent);
        Assert.Equal(3, result.Files[0].Lines.Count);
        Assert.Equal(CoverageLineStatus.Covered, result.Files[0].Lines[0].Status);
        Assert.Equal(CoverageLineStatus.Uncovered, result.Files[0].Lines[2].Status);
        Assert.Equal("Helper.ps1", result.Files[1].FileName);
        Assert.Equal(50, result.Files[1].LinePercent);
    }

    [Fact]
    public async Task ParseAsync_ReadsJaCoCoLineDetail()
    {
        var result = await _sut.ParseAsync(GetFixturePath("Coverage.jacoco.files.xml"));
        var greeting = result.Files.First(file => file.FileName == "Greeting.ps1");

        Assert.True(greeting.HasLineDetail);
        Assert.Contains(greeting.Lines, line => line.LineNumber == 2 && line.Status == CoverageLineStatus.Covered);
        Assert.Contains(greeting.Lines, line => line.LineNumber == 3 && line.Status == CoverageLineStatus.Uncovered);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
