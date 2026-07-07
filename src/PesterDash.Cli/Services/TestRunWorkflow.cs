using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Cli.Services;

/// <summary>Coordinates test discovery, execution, parsing, and coverage.</summary>
internal static class TestRunWorkflow
{
    public static async Task<(int ExitCode, DashboardData? Data)> ExecuteAsync(
        IServiceProvider services,
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? filter,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var testRunner = services.GetRequiredService<ITestRunner>();
        var resultParser = services.GetRequiredService<IResultParser>();

        var runResult = await testRunner.RunAsync(
            context,
            discovery,
            filter,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (runResult.ProcessResult.WasCancelled)
        {
            return (130, null);
        }

        var processOutput = RunOutputHelper.FormatRunOutput(runResult.ProcessResult);
        var runLogPath = await RunOutputHelper.SaveRunLogAsync(
            context,
            processOutput,
            cancellationToken).ConfigureAwait(false);

        string? debugLogPath = null;
        if (context.Options.Debug)
        {
            var debugLogContent = RunOutputHelper.BuildDebugLog(context, discovery, runResult);
            debugLogPath = await RunOutputHelper.SaveDebugLogAsync(
                context,
                debugLogContent,
                cancellationToken).ConfigureAwait(false);
        }

        var runMetadata = new RunMetadata
        {
            ProcessOutput = processOutput,
            RunLogPath = runLogPath,
            DebugLogPath = debugLogPath,
            ProcessStderr = runResult.ProcessResult.StandardError,
        };

        if (!runResult.HasTestResults)
        {
            var exitCode = runResult.ProcessResult.ExitCode == 0 ? 1 : runResult.ProcessResult.ExitCode;
            return (exitCode, CreateDashboardData(
                context,
                runMetadata,
                new TestRunResult
                {
                    ResultsFile = runResult.TestResultsPath,
                    Total = 0,
                    Passed = 0,
                    Failed = 0,
                    Skipped = 0,
                    Duration = TimeSpan.Zero,
                    Tests = [],
                },
                coverage: null,
                analyser: null,
                filter));
        }

        var testRun = await resultParser.ParseAsync(runResult.TestResultsPath, cancellationToken).ConfigureAwait(false);

        CoverageReportResult? coverageReport = null;
        if (context.Options.Coverage && runResult.CoveragePath is not null)
        {
            var coverageRunner = services.GetRequiredService<ICoverageRunner>();
            coverageReport = await coverageRunner.ProcessAsync(
                context,
                runResult.CoveragePath,
                cancellationToken).ConfigureAwait(false);

            if (coverageReport is not null)
            {
                coverageReport = new CoverageReportResult
                {
                    Metrics = coverageReport.Metrics,
                    HtmlReportPath = coverageReport.HtmlReportPath,
                    CoverageFilePath = runResult.CoveragePath,
                    Warning = coverageReport.Warning,
                };
            }
        }

        AnalyserResult? analyserResult = null;
        if (context.Options.Analyser && discovery.SourceFiles.Count > 0)
        {
            var analyser = services.GetRequiredService<ScriptAnalyzerRunner>();
            analyserResult = await analyser.RunAsync(context, discovery.SourceFiles, cancellationToken).ConfigureAwait(false);
        }

        var data = CreateDashboardData(context, runMetadata, testRun, coverageReport, analyserResult, filter);

        var successExitCode = testRun.Failed > 0
            ? Math.Min(testRun.Failed, context.Options.MaxFailures)
            : 0;

        return (successExitCode, data);
    }

    private static DashboardData CreateDashboardData(
        ProjectContext context,
        RunMetadata metadata,
        TestRunResult testRun,
        CoverageReportResult? coverage,
        AnalyserResult? analyser,
        RunFilter? filter) =>
        new()
        {
            Context = context,
            TestRun = testRun,
            Coverage = coverage,
            Analyser = analyser,
            RunOutput = metadata.ProcessOutput,
            RunLogPath = metadata.RunLogPath,
            DebugLogPath = metadata.DebugLogPath,
            ProcessStderr = metadata.ProcessStderr,
            LastFilterDescription = filter?.IsActive == true ? filter.Describe() : "scope",
            LastFilter = filter?.IsActive == true ? filter : null,
        };

    private sealed class RunMetadata
    {
        public required string ProcessOutput { get; init; }

        public required string RunLogPath { get; init; }

        public string? DebugLogPath { get; init; }

        public string? ProcessStderr { get; init; }
    }
}
