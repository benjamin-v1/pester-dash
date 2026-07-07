using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Services;

/// <summary>Manages artefact paths and directories.</summary>
public sealed class ArtifactService : IArtifactService
{
    public const string TestResultsFileName = "TestResults.xml";
    public const string CoverageFileName = "Coverage.xml";
    public const string CoverageDirectoryName = "Coverage";
    public const string RunScriptFileName = ".pesterdash-run.ps1";

    /// <inheritdoc />
    public string EnsureOutputDirectory(ProjectContext context)
    {
        Directory.CreateDirectory(context.OutputDirectory);
        return context.OutputDirectory;
    }

    /// <inheritdoc />
    public string GetTestResultsPath(ProjectContext context) =>
        Path.Combine(context.OutputDirectory, TestResultsFileName);

    /// <inheritdoc />
    public string GetCoveragePath(ProjectContext context) =>
        Path.Combine(context.OutputDirectory, CoverageFileName);

    /// <inheritdoc />
    public string GetCoverageReportDirectory(ProjectContext context) =>
        Path.Combine(context.OutputDirectory, CoverageDirectoryName);

    /// <inheritdoc />
    public string GetCoverageHtmlPath(ProjectContext context) =>
        Path.Combine(GetCoverageReportDirectory(context), "index.html");

    /// <inheritdoc />
    public string GetRunScriptPath(ProjectContext context) =>
        Path.Combine(context.OutputDirectory, RunScriptFileName);
}
