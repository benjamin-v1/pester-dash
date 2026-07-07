namespace PesterDash.Core.Models;

/// <summary>PSScriptAnalyzer findings for the project scope.</summary>
public sealed class AnalyserResult
{
    public required IReadOnlyList<AnalyserFinding> Findings { get; init; }

    public int ErrorCount => Findings.Count(f => f.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));

    public int WarningCount => Findings.Count(f => f.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));

    public int InformationCount => Findings.Count(f =>
        f.Severity.Equals("Information", StringComparison.OrdinalIgnoreCase)
        || f.Severity.Equals("Info", StringComparison.OrdinalIgnoreCase));

    public string? WarningMessage { get; init; }

    public static AnalyserResult Empty(string? warning = null) =>
        new() { Findings = [], WarningMessage = warning };
}

/// <summary>A single PSScriptAnalyzer diagnostic.</summary>
public sealed class AnalyserFinding
{
    public required string RuleName { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public required string ScriptPath { get; init; }

    public int Line { get; init; }

    public int Column { get; init; }
}
