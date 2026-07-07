using PesterDash.Core.Models;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class CoverageOutput
{
    public static void Write(CoverageReportResult report)
    {
        if (report.Metrics is null)
        {
            if (!string.IsNullOrWhiteSpace(report.Warning))
            {
                AnsiConsole.MarkupLine($"[yellow]Coverage warning:[/] {Markup.Escape(report.Warning)}");
            }

            return;
        }

        var metrics = report.Metrics;
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Coverage[/]")
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Line", $"{metrics.LinePercent:F0}% ({metrics.CoveredLines}/{metrics.TotalLines})");
        table.AddRow(
            "Branch",
            metrics.HasBranchCoverage
                ? $"{metrics.BranchPercent:F0}% ({metrics.CoveredBranches}/{metrics.TotalBranches})"
                : "n/a");

        AnsiConsole.Write(table);

        if (report.HtmlReportPath is not null)
        {
            AnsiConsole.MarkupLine($"[grey]HTML report:[/] {Markup.Escape(report.HtmlReportPath)}");
        }

        if (!string.IsNullOrWhiteSpace(report.Warning))
        {
            AnsiConsole.MarkupLine($"[yellow]Coverage warning:[/] {Markup.Escape(report.Warning)}");
        }
    }
}
