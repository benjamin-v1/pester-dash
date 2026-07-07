namespace PesterDash.Core.Models;

/// <summary>Coverage status for a single source line.</summary>
public enum CoverageLineStatus
{
    NotExecutable,
    Covered,
    Uncovered,
    Partial,
}

/// <summary>Coverage data for one line in a source file.</summary>
public sealed class CoverageLineEntry
{
    public int LineNumber { get; init; }

    public CoverageLineStatus Status { get; init; }

    public int HitCount { get; init; }
}
