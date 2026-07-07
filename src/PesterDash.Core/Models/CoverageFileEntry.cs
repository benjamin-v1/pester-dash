namespace PesterDash.Core.Models;

/// <summary>Line and branch coverage for a single source file.</summary>
public sealed class CoverageFileEntry
{
    public required string FileName { get; init; }

    public int CoveredLines { get; init; }

    public int TotalLines { get; init; }

    public int CoveredBranches { get; init; }

    public int TotalBranches { get; init; }

    public double LinePercent => TotalLines == 0 ? 0 : CoveredLines * 100.0 / TotalLines;

    public double BranchPercent => TotalBranches == 0 ? 0 : CoveredBranches * 100.0 / TotalBranches;

    public bool HasBranchCoverage => TotalBranches > 0;

    public IReadOnlyList<CoverageLineEntry> Lines { get; init; } = [];

    public bool HasLineDetail => Lines.Count > 0;
}
