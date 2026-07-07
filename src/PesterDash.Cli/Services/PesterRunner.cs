using System.Text;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Services;

/// <summary>Generates and runs a temporary Pester configuration script.</summary>
public sealed class PesterRunner : ITestRunner
{
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly IArtifactService _artifactService;

    public PesterRunner(IPowerShellRunner powerShellRunner, IArtifactService artifactService)
    {
        _powerShellRunner = powerShellRunner;
        _artifactService = artifactService;
    }

    /// <inheritdoc />
    public async Task<PesterRunResult> RunAsync(
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? filter = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        await _powerShellRunner.EnsurePrerequisitesAsync(cancellationToken).ConfigureAwait(false);

        _artifactService.EnsureOutputDirectory(context);

        var testResultsPath = _artifactService.GetTestResultsPath(context);
        var coveragePath = _artifactService.GetCoveragePath(context);
        var runScriptPath = _artifactService.GetRunScriptPath(context);

        var script = BuildRunScript(context, discovery, filter, testResultsPath, coveragePath);
        await File.WriteAllTextAsync(runScriptPath, script, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        var processResult = await _powerShellRunner.RunScriptFileAsync(
            runScriptPath,
            context.ProjectRoot,
            outputProgress,
            cancellationToken).ConfigureAwait(false);

        return new PesterRunResult
        {
            ProcessResult = processResult,
            TestResultsPath = testResultsPath,
            CoveragePath = context.Options.Coverage ? coveragePath : null,
            RunScriptPath = runScriptPath,
        };
    }

    private static string BuildRunScript(
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? filter,
        string testResultsPath,
        string coveragePath)
    {
        var testFiles = filter?.HasFileFilter == true ? filter.TestFiles : discovery.TestFiles;

        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("Import-Module Pester");
        builder.AppendLine("$config = New-PesterConfiguration");
        builder.AppendLine("$config.Run.Path = @(");
        AppendQuotedPaths(builder, testFiles);
        builder.AppendLine(")");
        builder.AppendLine("$config.Run.PassThru = $true");
        builder.AppendLine("$config.Output.Verbosity = 'Detailed'");
        builder.AppendLine("$config.TestResult.Enabled = $true");
        builder.AppendLine("$config.TestResult.OutputFormat = 'NUnitXml'");
        builder.AppendLine($"$config.TestResult.OutputPath = '{EscapePowerShellSingleQuoted(testResultsPath)}'");

        if (filter?.HasNameFilter == true)
        {
            builder.AppendLine("$config.Filter.FullName = @(");
            AppendQuotedPaths(builder, filter.FullNameFilters);
            builder.AppendLine(")");
        }

        if (context.Options.Coverage && discovery.SourceFiles.Count > 0)
        {
            builder.AppendLine("$config.CodeCoverage.Enabled = $true");
            builder.AppendLine("$config.CodeCoverage.Path = @(");
            AppendQuotedPaths(builder, discovery.SourceFiles);
            builder.AppendLine(")");
            builder.AppendLine($"$config.CodeCoverage.OutputPath = '{EscapePowerShellSingleQuoted(coveragePath)}'");
        }

        builder.AppendLine("$result = Invoke-Pester -Configuration $config");
        builder.AppendLine("if ($result.FailedCount -gt 0) { exit 1 }");
        builder.AppendLine("exit 0");
        return builder.ToString();
    }

    private static void AppendQuotedPaths(StringBuilder builder, IEnumerable<string> paths)
    {
        var lines = paths
            .Select(path => $"    '{EscapePowerShellSingleQuoted(path)}'")
            .ToList();

        builder.AppendLine(string.Join($",{Environment.NewLine}", lines));
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
