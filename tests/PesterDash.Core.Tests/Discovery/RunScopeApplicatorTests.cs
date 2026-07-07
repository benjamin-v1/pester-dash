using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Models;

namespace PesterDash.Core.Tests.Discovery;

public class RunScopeApplicatorTests
{
    [Fact]
    public void Apply_UsesSmartDefaults_WhenScopeNotConfigured()
    {
        var discovery = CreateDiscovery(
            testFiles:
            [
                @"C:\proj\tests\unit\run-tests.ps1",
                @"C:\proj\tests\unit\Gateway.Unit.Tests.ps1",
            ],
            sourceFiles:
            [
                @"C:\proj\scripts\Gateway.psm1",
            ]);

        var result = RunScopeApplicator.Apply(discovery, new PesterDashOptions());

        Assert.Single(result.TestFiles);
        Assert.Contains("Gateway.Unit.Tests.ps1", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.SourceFiles);
    }

    [Fact]
    public void Apply_UsesConfiguredScope_WhenPresent()
    {
        var discovery = CreateDiscovery(
            testFiles:
            [
                @"C:\proj\tests\unit\run-tests.ps1",
                @"C:\proj\tests\unit\Gateway.Unit.Tests.ps1",
            ],
            sourceFiles:
            [
                @"C:\proj\scripts\Gateway.psm1",
                @"C:\proj\scripts\Helper.ps1",
            ]);

        var options = new PesterDashOptions
        {
            RunScope = new RunScopeOptions
            {
                TestFiles = [@"tests\unit\run-tests.ps1"],
                SourceFiles = [@"scripts\Helper.ps1"],
            },
        };

        var result = RunScopeApplicator.Apply(discovery, options);

        Assert.Single(result.TestFiles);
        Assert.Contains("run-tests.ps1", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.SourceFiles);
        Assert.Contains("Helper.ps1", result.SourceFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateScopeFromSelection_StoresRelativePaths()
    {
        var scope = RunScopeApplicator.CreateScopeFromSelection(
            @"C:\proj",
            [@"C:\proj\tests\Alpha.Tests.ps1"],
            [@"C:\proj\src\Alpha.ps1"]);

        Assert.Equal([@"tests\Alpha.Tests.ps1"], scope.TestFiles);
        Assert.Equal([@"src\Alpha.ps1"], scope.SourceFiles);
    }

    private static DiscoveryResult CreateDiscovery(
        IReadOnlyList<string> testFiles,
        IReadOnlyList<string> sourceFiles) =>
        new()
        {
            ProjectRoot = @"C:\proj",
            TestFiles = testFiles,
            SourceFiles = sourceFiles,
            TotalFilesScanned = testFiles.Count + sourceFiles.Count,
        };
}
