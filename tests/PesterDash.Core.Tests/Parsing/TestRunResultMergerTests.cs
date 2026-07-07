using System.Xml.Linq;
using PesterDash.Core.Models;
using PesterDash.Core.Parsing;

namespace PesterDash.Core.Tests.Parsing;

public class TestRunResultMergerTests
{
    [Fact]
    public void Merge_UpdatesMatchingTestsAndPreservesOthers()
    {
        var existing = CreateRun(
            ("Get-Greeting.returns a greeting", TestOutcome.Passed, 0.11, null),
            ("Get-Greeting.is pending", TestOutcome.Skipped, 0.01, null),
            ("Other.describe test", TestOutcome.Passed, 0.05, null));

        var incoming = CreateRun(
            ("Get-Greeting.returns a greeting", TestOutcome.Failed, 0.20, "boom"));

        var merged = TestRunResultMerger.Merge(existing, incoming);

        Assert.Equal(3, merged.Total);
        Assert.Equal(1, merged.Passed);
        Assert.Equal(1, merged.Failed);
        Assert.Equal(1, merged.Skipped);

        var updated = merged.Tests.Single(test => test.Name == "Get-Greeting.returns a greeting");
        Assert.Equal(TestOutcome.Failed, updated.Outcome);
        Assert.Equal("boom", updated.ErrorMessage);

        var untouched = merged.Tests.Single(test => test.Name == "Other.describe test");
        Assert.Equal(TestOutcome.Passed, untouched.Outcome);
    }

    [Fact]
    public void Merge_RebuildsCountsAndDurationFromMergedTests()
    {
        var existing = CreateRun(
            ("A.one", TestOutcome.Passed, 0.10, null),
            ("A.two", TestOutcome.Passed, 0.20, null));

        var incoming = CreateRun(
            ("A.one", TestOutcome.Passed, 0.30, null));

        var merged = TestRunResultMerger.Merge(existing, incoming);

        Assert.Equal(2, merged.Total);
        Assert.Equal(2, merged.Passed);
        Assert.Equal(TimeSpan.FromMilliseconds(500), merged.Duration);
    }

    [Fact]
    public async Task Merge_UpdatesTreeNodesForChangedTests()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestResults.xml");
        var existing = await new NUnitResultParser().ParseAsync(fixture);
        var incoming = CreateRun(
            ("Get-Greeting.returns a greeting", TestOutcome.Failed, 0.42, "failed again"));

        var merged = TestRunResultMerger.Merge(existing, incoming);

        Assert.NotNull(merged.Tree);
        var testNode = FindTestNode(merged.Tree!, "returns a greeting");
        Assert.NotNull(testNode);
        Assert.Equal(TestOutcome.Failed, testNode!.Test!.Outcome);
        Assert.Equal(1, merged.Tree!.Children[0].Failed);
    }

    private static TestRunResult CreateRun(
        params (string Name, TestOutcome Outcome, double Seconds, string? Error)[] tests)
    {
        var cases = tests
            .Select(test => new TestCaseResult
            {
                Name = test.Name,
                FullName = test.Name,
                Outcome = test.Outcome,
                Duration = TimeSpan.FromSeconds(test.Seconds),
                ErrorMessage = test.Error,
            })
            .ToList();

        return new TestRunResult
        {
            ResultsFile = "results.xml",
            Tests = cases,
            Total = cases.Count,
            Passed = cases.Count(test => test.Outcome == TestOutcome.Passed),
            Failed = cases.Count(test => test.Outcome == TestOutcome.Failed),
            Skipped = cases.Count(test => test.Outcome is TestOutcome.Skipped or TestOutcome.Inconclusive),
            Duration = TimeSpan.FromSeconds(cases.Sum(test => test.Duration.TotalSeconds)),
        };
    }

    private static TestTreeNode? FindTestNode(TestTreeNode node, string displayName)
    {
        if (node.Kind == TestTreeNodeKind.Test && node.Name == displayName)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindTestNode(child, displayName);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
