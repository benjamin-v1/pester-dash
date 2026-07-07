using PesterDash.Core.Models;

namespace PesterDash.Cli.Views;

internal enum DashboardPrimaryView
{
    Tests,
    Coverage,
    Analyser,
}

internal sealed class ScopeSummary
{
    public required string Title { get; init; }

    public int Passed { get; init; }

    public int Failed { get; init; }

    public int Skipped { get; init; }

    public int Total { get; init; }

    public double PassRate { get; init; }

    public TimeSpan Duration { get; init; }

    public static ScopeSummary FromRun(TestRunResult run) =>
        new()
        {
            Title = "All tests",
            Passed = run.Passed,
            Failed = run.Failed,
            Skipped = run.Skipped,
            Total = run.Total,
            PassRate = run.PassRate,
            Duration = run.Duration,
        };

    public static ScopeSummary FromNode(TestTreeNode node)
    {
        var total = node.Total;
        var passed = node.Passed;
        return new()
        {
            Title = node.Name,
            Passed = passed,
            Failed = node.Failed,
            Skipped = node.Skipped,
            Total = total,
            PassRate = total == 0 ? 0 : passed * 100.0 / total,
            Duration = node.Duration,
        };
    }
}
