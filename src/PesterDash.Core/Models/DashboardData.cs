namespace PesterDash.Core.Models;

/// <summary>Data displayed by the interactive dashboard.</summary>
public sealed class DashboardData
{
    public required ProjectContext Context { get; init; }

    public required TestRunResult TestRun { get; init; }

    public CoverageReportResult? Coverage { get; init; }

    public AnalyserResult? Analyser { get; init; }

    /// <summary>Captured process output when Pester did not produce test results.</summary>
    public string? RunOutput { get; init; }

    /// <summary>Path to the saved stdout/stderr log.</summary>
    public string? RunLogPath { get; init; }

    /// <summary>Path to the debug log when <c>--debug</c> is enabled.</summary>
    public string? DebugLogPath { get; init; }

    /// <summary>Raw stderr from the Pester process.</summary>
    public string? ProcessStderr { get; init; }

    /// <summary>Whether the Pester process wrote to stderr.</summary>
    public bool HasProcessStderr => !string.IsNullOrWhiteSpace(ProcessStderr);

    /// <summary>Empty-state or status text shown in the dashboard header.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Description of the last executed filter (scope, file, test, etc.).</summary>
    public string? LastFilterDescription { get; init; }

    /// <summary>Last run filter for targeted reruns.</summary>
    public RunFilter? LastFilter { get; init; }

    /// <summary>Whether file watching is active.</summary>
    public bool IsWatching { get; init; }
}
