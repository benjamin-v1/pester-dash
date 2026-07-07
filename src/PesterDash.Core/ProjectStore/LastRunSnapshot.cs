using System.Text.Json;
using System.Text.Json.Serialization;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Core.ProjectStore;

/// <summary>Persisted dashboard state for instant load on <c>open</c>.</summary>
public sealed class LastRunSnapshot
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DateTimeOffset SavedAt { get; init; }

    public string? LastFilterDescription { get; init; }

    public RunFilter? LastFilter { get; init; }

    public SnapshotTestRun? TestRun { get; init; }

    public SnapshotCoverage? Coverage { get; init; }

    public SnapshotAnalyser? Analyser { get; init; }

    public string? RunLogPath { get; init; }

    public string? DebugLogPath { get; init; }

    public bool HasResults => TestRun is { Total: > 0 };

    public static LastRunSnapshot FromDashboard(DashboardData data, RunFilter? lastFilter)
    {
        var run = data.TestRun;
        var coverage = data.Coverage?.Metrics;

        return new LastRunSnapshot
        {
            SavedAt = DateTimeOffset.UtcNow,
            LastFilterDescription = lastFilter?.Describe(),
            LastFilter = lastFilter?.IsActive == true ? lastFilter : null,
            RunLogPath = data.RunLogPath,
            DebugLogPath = data.DebugLogPath,
            TestRun = run.Total == 0
                ? null
                : new SnapshotTestRun
                {
                    ResultsFile = run.ResultsFile,
                    Total = run.Total,
                    Passed = run.Passed,
                    Failed = run.Failed,
                    Skipped = run.Skipped,
                    DurationSeconds = run.Duration.TotalSeconds,
                    Tests = run.Tests.Select(test => new SnapshotTest
                    {
                        Name = test.Name,
                        FullName = test.FullName,
                        Outcome = test.Outcome.ToString(),
                        DurationMs = test.Duration.TotalMilliseconds,
                        ErrorMessage = test.ErrorMessage,
                        StackTrace = test.StackTrace,
                    }).ToList(),
                },
            Coverage = coverage is null
                ? null
                : new SnapshotCoverage
                {
                    LinePercent = coverage.LinePercent,
                    CoveredLines = coverage.CoveredLines,
                    TotalLines = coverage.TotalLines,
                    BranchPercent = coverage.HasBranchCoverage ? coverage.BranchPercent : null,
                    HtmlReportPath = data.Coverage?.HtmlReportPath,
                    CoverageXmlPath = data.Coverage?.CoverageFilePath,
                },
            Analyser = data.Analyser is null
                ? null
                : new SnapshotAnalyser
                {
                    WarningMessage = data.Analyser.WarningMessage,
                    Findings = data.Analyser.Findings.Select(f => new SnapshotAnalyserFinding
                    {
                        RuleName = f.RuleName,
                        Severity = f.Severity,
                        Message = f.Message,
                        ScriptPath = f.ScriptPath,
                        Line = f.Line,
                        Column = f.Column,
                    }).ToList(),
                },
        };
    }

    public async Task<DashboardData?> ToDashboardAsync(
        ProjectContext context,
        IResultParser resultParser,
        ICoverageParser? coverageParser,
        CancellationToken cancellationToken = default)
    {
        if (TestRun is null || TestRun.Total == 0)
        {
            return new DashboardData
            {
                Context = context,
                TestRun = new TestRunResult
                {
                    ResultsFile = TestRun?.ResultsFile ?? string.Empty,
                    Total = 0,
                    Passed = 0,
                    Failed = 0,
                    Skipped = 0,
                    Duration = TimeSpan.Zero,
                    Tests = [],
                },
                StatusMessage = "No previous run. Press F5 to run all tests, S to configure scope.",
                RunLogPath = RunLogPath,
                DebugLogPath = DebugLogPath,
                Analyser = Analyser?.ToModel(),
            };
        }

        TestRunResult testRun;
        if (!string.IsNullOrWhiteSpace(TestRun.ResultsFile) && File.Exists(TestRun.ResultsFile))
        {
            testRun = await resultParser.ParseAsync(TestRun.ResultsFile, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            testRun = TestRun.ToModel();
        }

        CoverageReportResult? coverage = null;
        if (Coverage?.CoverageXmlPath is { } coveragePath
            && coverageParser is not null
            && File.Exists(coveragePath))
        {
            var metrics = await coverageParser.ParseAsync(coveragePath, cancellationToken).ConfigureAwait(false);
            coverage = new CoverageReportResult
            {
                Metrics = metrics,
                HtmlReportPath = Coverage.HtmlReportPath,
                CoverageFilePath = coveragePath,
            };
        }

        return new DashboardData
        {
            Context = context,
            TestRun = testRun,
            Coverage = coverage,
            Analyser = Analyser?.ToModel(),
            RunLogPath = RunLogPath,
            DebugLogPath = DebugLogPath,
            LastFilterDescription = LastFilterDescription,
            LastFilter = LastFilter,
        };
    }

    public static async Task SaveAsync(string projectRoot, LastRunSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ProjectStorePaths.EnsureStore(projectRoot);
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        await File.WriteAllTextAsync(ProjectStorePaths.GetLastRunPath(projectRoot), json, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<LastRunSnapshot?> LoadAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var path = ProjectStorePaths.GetLastRunPath(projectRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<LastRunSnapshot>(json, SerializerOptions);
    }

    public sealed class SnapshotTestRun
    {
        public string? ResultsFile { get; init; }

        public int Total { get; init; }

        public int Passed { get; init; }

        public int Failed { get; init; }

        public int Skipped { get; init; }

        public double DurationSeconds { get; init; }

        public IList<SnapshotTest> Tests { get; init; } = [];

        public TestRunResult ToModel() =>
            new()
            {
                ResultsFile = ResultsFile ?? string.Empty,
                Total = Total,
                Passed = Passed,
                Failed = Failed,
                Skipped = Skipped,
                Duration = TimeSpan.FromSeconds(DurationSeconds),
                Tests = Tests.Select(test => test.ToModel()).ToList(),
            };
    }

    public sealed class SnapshotTest
    {
        public required string Name { get; init; }

        public string? FullName { get; init; }

        public required string Outcome { get; init; }

        public double DurationMs { get; init; }

        public string? ErrorMessage { get; init; }

        public string? StackTrace { get; init; }

        public TestCaseResult ToModel() =>
            new()
            {
                Name = Name,
                FullName = FullName,
                Outcome = Enum.TryParse<TestOutcome>(Outcome, true, out var outcome) ? outcome : TestOutcome.Inconclusive,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                ErrorMessage = ErrorMessage,
                StackTrace = StackTrace,
            };
    }

    public sealed class SnapshotCoverage
    {
        public double LinePercent { get; init; }

        public int CoveredLines { get; init; }

        public int TotalLines { get; init; }

        public double? BranchPercent { get; init; }

        public string? HtmlReportPath { get; init; }

        public string? CoverageXmlPath { get; init; }
    }

    public sealed class SnapshotAnalyser
    {
        public string? WarningMessage { get; init; }

        public IList<SnapshotAnalyserFinding> Findings { get; init; } = [];

        public AnalyserResult ToModel() =>
            new()
            {
                WarningMessage = WarningMessage,
                Findings = Findings.Select(f => f.ToModel()).ToList(),
            };
    }

    public sealed class SnapshotAnalyserFinding
    {
        public required string RuleName { get; init; }

        public required string Severity { get; init; }

        public required string Message { get; init; }

        public required string ScriptPath { get; init; }

        public int Line { get; init; }

        public int Column { get; init; }

        public AnalyserFinding ToModel() =>
            new()
            {
                RuleName = RuleName,
                Severity = Severity,
                Message = Message,
                ScriptPath = ScriptPath,
                Line = Line,
                Column = Column,
            };
    }
}
