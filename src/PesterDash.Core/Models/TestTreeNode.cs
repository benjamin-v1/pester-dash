namespace PesterDash.Core.Models;

/// <summary>Kind of node in the test result hierarchy.</summary>
public enum TestTreeNodeKind
{
    Root,
    File,
    Describe,
    Context,
    Test,
}

/// <summary>Hierarchical view of test results for drill-down navigation.</summary>
public sealed class TestTreeNode
{
    public required string Name { get; init; }

    public TestTreeNodeKind Kind { get; init; }

    public TestCaseResult? Test { get; init; }

    public IReadOnlyList<TestTreeNode> Children { get; init; } = [];

    /// <summary>Full path when <see cref="Kind"/> is <see cref="TestTreeNodeKind.File"/>.</summary>
    public string? SourcePath { get; init; }

    public bool IsLeaf => Test is not null || Children.Count == 0;

    public int Total => Passed + Failed + Skipped + Inconclusive;

    public int Passed => Test?.Outcome == TestOutcome.Passed
        ? 1
        : Children.Sum(child => child.Passed);

    public int Failed => Test?.Outcome == TestOutcome.Failed
        ? 1
        : Children.Sum(child => child.Failed);

    public int Skipped => Test?.Outcome is TestOutcome.Skipped or TestOutcome.Inconclusive
        ? 1
        : Children.Sum(child => child.Skipped);

    public int Inconclusive => Test?.Outcome == TestOutcome.Inconclusive
        ? 1
        : Children.Sum(child => child.Inconclusive);

    public bool HasFailures => Failed > 0 || Children.Any(child => child.HasFailures);

    public TimeSpan Duration => Test?.Duration
        ?? TimeSpan.FromMilliseconds(Children.Sum(child => child.Duration.TotalMilliseconds));
}
