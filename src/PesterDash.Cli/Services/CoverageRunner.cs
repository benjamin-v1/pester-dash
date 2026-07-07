using System.Diagnostics;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Services;

/// <summary>Parses coverage XML and generates HTML reports via ReportGenerator.</summary>
public sealed class CoverageRunner : ICoverageRunner
{
    private readonly ICoverageParser _coverageParser;
    private readonly IArtifactService _artifactService;

    public CoverageRunner(ICoverageParser coverageParser, IArtifactService artifactService)
    {
        _coverageParser = coverageParser;
        _artifactService = artifactService;
    }

    /// <inheritdoc />
    public async Task<CoverageReportResult> ProcessAsync(
        ProjectContext context,
        string coverageXmlPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(coverageXmlPath))
        {
            return new CoverageReportResult
            {
                Warning = $"Coverage file not found: {coverageXmlPath}",
            };
        }

        CoverageResult metrics;

        try
        {
            metrics = await _coverageParser.ParseAsync(coverageXmlPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new CoverageReportResult
            {
                Warning = $"Failed to parse coverage XML: {ex.Message}",
            };
        }

        var reportDirectory = _artifactService.GetCoverageReportDirectory(context);
        Directory.CreateDirectory(reportDirectory);

        var htmlPath = _artifactService.GetCoverageHtmlPath(context);
        var reportGeneratorWarning = await TryGenerateHtmlReportAsync(
            coverageXmlPath,
            reportDirectory,
            cancellationToken).ConfigureAwait(false);

        return new CoverageReportResult
        {
            Metrics = metrics,
            HtmlReportPath = File.Exists(htmlPath) ? htmlPath : null,
            Warning = reportGeneratorWarning,
        };
    }

    private static async Task<string?> TryGenerateHtmlReportAsync(
        string coverageXmlPath,
        string reportDirectory,
        CancellationToken cancellationToken)
    {
        var toolDirectory = AppContext.BaseDirectory;
        var manifestPath = Path.Combine(toolDirectory, ".config", "dotnet-tools.json");

        if (File.Exists(manifestPath))
        {
            var restore = await RunDotNetAsync(
                ["tool", "restore"],
                cancellationToken,
                toolDirectory).ConfigureAwait(false);

            if (restore.ExitCode != 0)
            {
                return "Failed to restore ReportGenerator dotnet tool from manifest.";
            }

            var result = await RunDotNetAsync(
                [
                    "tool", "run", "reportgenerator", "--",
                    $"-reports:{coverageXmlPath}",
                    $"-targetdir:{reportDirectory}",
                    "-reporttypes:Html",
                ],
                cancellationToken,
                toolDirectory).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                return FormatReportGeneratorFailure(result);
            }

            return null;
        }

        var globalResult = await RunDotNetAsync(
            [
                "reportgenerator",
                $"-reports:{coverageXmlPath}",
                $"-targetdir:{reportDirectory}",
                "-reporttypes:Html",
            ],
            cancellationToken).ConfigureAwait(false);

        return globalResult.ExitCode == 0
            ? null
            : FormatReportGeneratorFailure(globalResult);
    }

    private static string FormatReportGeneratorFailure(ProcessResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;

        return $"ReportGenerator failed. Install with: dotnet tool install -g dotnet-reportgenerator-globaltool{Environment.NewLine}{details}".Trim();
    }

    private static async Task<ProcessResult> RunDotNetAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet process.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
