namespace PesterDash.Core.Models;

/// <summary>Code coverage metrics parsed from a coverage report.</summary>
public sealed class CoverageResult
{
    public required string ReportFile { get; init; }

    public int CoveredLines { get; init; }

    public int TotalLines { get; init; }

    public int CoveredBranches { get; init; }

    public int TotalBranches { get; init; }

    public double LinePercent => TotalLines == 0 ? 0 : CoveredLines * 100.0 / TotalLines;

    public double BranchPercent => TotalBranches == 0 ? 0 : CoveredBranches * 100.0 / TotalBranches;

    public bool HasBranchCoverage => TotalBranches > 0;

    public IReadOnlyList<CoverageFileEntry> Files { get; init; } = [];
}
