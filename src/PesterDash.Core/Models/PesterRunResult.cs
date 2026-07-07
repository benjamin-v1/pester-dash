namespace PesterDash.Core.Models;

/// <summary>Result of a Pester test run.</summary>
public sealed class PesterRunResult
{
    public required PowerShellRunResult ProcessResult { get; init; }

    public required string TestResultsPath { get; init; }

    public string? CoveragePath { get; init; }

    public required string RunScriptPath { get; init; }

    public bool HasTestResults => File.Exists(TestResultsPath);
}
