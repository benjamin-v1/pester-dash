namespace PesterDash.Core.Models;

/// <summary>A single executed test case.</summary>
public sealed class TestCaseResult
{
    public required string Name { get; init; }

    public string? FullName { get; init; }

    public required TestOutcome Outcome { get; init; }

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public string? StackTrace { get; init; }
}
