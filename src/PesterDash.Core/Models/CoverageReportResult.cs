namespace PesterDash.Core.Models;

/// <summary>Coverage parsing and HTML report generation result.</summary>
public sealed class CoverageReportResult
{
    public CoverageResult? Metrics { get; init; }

    public string? HtmlReportPath { get; init; }

    public string? CoverageFilePath { get; init; }

    public string? Warning { get; init; }
}
