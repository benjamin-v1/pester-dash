using System.Text;
using PesterDash.Core.Formatting;
using PesterDash.Core.Models;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Cli.Services;

internal static class RunOutputHelper
{
    public const string RunLogFileName = "pester-output.log";
    public const string DebugLogFileName = "pester-debug.log";
    public static string FormatRunOutput(PowerShellRunResult processResult)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            builder.AppendLine(processResult.StandardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("--- stderr ---");
            }

            builder.AppendLine(processResult.StandardError.TrimEnd());
        }

        return builder.Length == 0
            ? $"Pester exited with code {processResult.ExitCode} but produced no output."
            : builder.ToString();
    }

    public static string StripAnsi(string? text) => RunLogFormatter.StripAnsi(text);

    public static string? GetStderrSummary(string? stderr) => RunLogFormatter.GetStderrSummary(stderr);

    public static async Task<string> SaveRunLogAsync(
        ProjectContext context,
        string output,
        CancellationToken cancellationToken = default)
    {
        var logsDir = ProjectStorePaths.GetLogsDirectory(context.ProjectRoot);
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, RunLogFileName);
        await File.WriteAllTextAsync(logPath, output, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return logPath;
    }

    public static string BuildDebugLog(
        ProjectContext context,
        DiscoveryResult discovery,
        PesterRunResult runResult)
    {
        var builder = new StringBuilder();
        var process = runResult.ProcessResult;

        builder.AppendLine("=== PesterDash Debug Log ===");
        builder.AppendLine($"Timestamp (UTC): {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Project root:    {context.ProjectRoot}");
        builder.AppendLine($"Working dir:     {context.ProjectRoot}");
        builder.AppendLine($"Output dir:      {context.OutputDirectory}");
        builder.AppendLine($"Exit code:       {process.ExitCode}");
        builder.AppendLine($"Cancelled:       {process.WasCancelled}");
        builder.AppendLine($"Run script:      {runResult.RunScriptPath}");
        builder.AppendLine($"Test results:    {runResult.TestResultsPath}");
        builder.AppendLine($"Results exist:   {runResult.HasTestResults}");
        builder.AppendLine($"Coverage path:   {runResult.CoveragePath ?? "(none)"}");
        builder.AppendLine();

        builder.AppendLine("--- Test files ---");
        foreach (var testFile in discovery.TestFiles)
        {
            builder.AppendLine(testFile);
        }

        builder.AppendLine();
        builder.AppendLine("--- Source files (coverage) ---");
        foreach (var sourceFile in discovery.SourceFiles)
        {
            builder.AppendLine(sourceFile);
        }

        if (File.Exists(runResult.RunScriptPath))
        {
            builder.AppendLine();
            builder.AppendLine("--- Generated Pester script ---");
            builder.AppendLine(File.ReadAllText(runResult.RunScriptPath));
        }

        builder.AppendLine();
        builder.AppendLine("--- stdout ---");
        builder.AppendLine(string.IsNullOrWhiteSpace(process.StandardOutput)
            ? "(empty)"
            : process.StandardOutput.TrimEnd());

        builder.AppendLine();
        builder.AppendLine("--- stderr ---");
        builder.AppendLine(string.IsNullOrWhiteSpace(process.StandardError)
            ? "(empty)"
            : process.StandardError.TrimEnd());

        return builder.ToString();
    }

    public static async Task<string> SaveDebugLogAsync(
        ProjectContext context,
        string content,
        CancellationToken cancellationToken = default)
    {
        var logsDir = ProjectStorePaths.GetLogsDirectory(context.ProjectRoot);
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, DebugLogFileName);
        await File.WriteAllTextAsync(logPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return logPath;
    }
}
