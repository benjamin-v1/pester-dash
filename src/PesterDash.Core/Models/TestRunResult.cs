namespace PesterDash.Core.Models;

/// <summary>Aggregated results from a test run.</summary>
public sealed class TestRunResult
{
    public required string ResultsFile { get; init; }

    public int Total { get; init; }

    public int Passed { get; init; }

    public int Failed { get; init; }

    public int Skipped { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<TestCaseResult> Tests { get; init; } = [];

    public TestTreeNode? Tree { get; init; }

    public double PassRate => Total == 0 ? 0 : Passed * 100.0 / Total;

    public IReadOnlyList<TestCaseResult> SlowestTests =>
        Tests.OrderByDescending(t => t.Duration).Take(10).ToList();

    public IReadOnlyList<TestCaseResult> FailedTests =>
        Tests.Where(t => t.Outcome == TestOutcome.Failed).ToList();
}
