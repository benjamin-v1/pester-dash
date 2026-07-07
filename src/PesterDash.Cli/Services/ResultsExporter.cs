using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Services;

internal static class ResultsExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static (string JsonPath, string CsvPath) Export(DashboardData data)
    {
        var outputDir = data.Context.OutputDirectory;
        Directory.CreateDirectory(outputDir);

        var jsonPath = Path.Combine(outputDir, "pesterdash-results.json");
        var csvPath = Path.Combine(outputDir, "pesterdash-results.csv");

        var payload = BuildPayload(data);
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(csvPath, BuildCsv(data), Encoding.UTF8);

        return (jsonPath, csvPath);
    }

    private static ExportPayload BuildPayload(DashboardData data)
    {
        var run = data.TestRun;
        var coverage = data.Coverage?.Metrics;

        return new ExportPayload
        {
            Project = data.Context.ProjectRoot,
            ExportedAt = DateTimeOffset.Now,
            Summary = new ExportSummary
            {
                Total = run.Total,
                Passed = run.Passed,
                Failed = run.Failed,
                Skipped = run.Skipped,
                PassRate = run.PassRate,
                DurationSeconds = run.Duration.TotalSeconds,
            },
            Coverage = coverage is null
                ? null
                : new ExportCoverageSummary
                {
                    LinePercent = coverage.LinePercent,
                    CoveredLines = coverage.CoveredLines,
                    TotalLines = coverage.TotalLines,
                    BranchPercent = coverage.HasBranchCoverage ? coverage.BranchPercent : null,
                },
            Tests = run.Tests.Select(test => new ExportTest
            {
                Name = test.Name,
                FullName = test.FullName,
                Outcome = test.Outcome.ToString(),
                DurationMs = test.Duration.TotalMilliseconds,
                ErrorMessage = test.ErrorMessage,
                StackTrace = test.StackTrace,
            }).ToList(),
        };
    }

    private static string BuildCsv(DashboardData data)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Name,Outcome,DurationMs,Error");

        foreach (var test in data.TestRun.Tests)
        {
            builder.Append(CsvEscape(test.Name));
            builder.Append(',');
            builder.Append(CsvEscape(test.Outcome.ToString()));
            builder.Append(',');
            builder.Append(test.Duration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(CsvEscape(test.ErrorMessage));
        }

        return builder.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private sealed class ExportPayload
    {
        public required string Project { get; init; }

        public DateTimeOffset ExportedAt { get; init; }

        public required ExportSummary Summary { get; init; }

        public ExportCoverageSummary? Coverage { get; init; }

        public required IReadOnlyList<ExportTest> Tests { get; init; }
    }

    private sealed class ExportSummary
    {
        public int Total { get; init; }

        public int Passed { get; init; }

        public int Failed { get; init; }

        public int Skipped { get; init; }

        public double PassRate { get; init; }

        public double DurationSeconds { get; init; }
    }

    private sealed class ExportCoverageSummary
    {
        public double LinePercent { get; init; }

        public int CoveredLines { get; init; }

        public int TotalLines { get; init; }

        public double? BranchPercent { get; init; }
    }

    private sealed class ExportTest
    {
        public required string Name { get; init; }

        public string? FullName { get; init; }

        public required string Outcome { get; init; }

        public double DurationMs { get; init; }

        public string? ErrorMessage { get; init; }

        public string? StackTrace { get; init; }
    }
}
