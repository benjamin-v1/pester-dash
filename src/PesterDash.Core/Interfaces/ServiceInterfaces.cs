using PesterDash.Core.Configuration;
using PesterDash.Core.Models;

namespace PesterDash.Core.Interfaces;

/// <summary>Discovers test and source files in a PowerShell project.</summary>
public interface IProjectDiscovery
{
    /// <summary>Discovers test and source files under <paramref name="projectRoot"/>.</summary>
    DiscoveryResult Discover(string projectRoot, PesterDashOptions options);
}

/// <summary>Runs Pester tests via an external <c>pwsh</c> process.</summary>
public interface ITestRunner
{
    /// <summary>Runs discovered Pester tests and writes artefacts.</summary>
    Task<PesterRunResult> RunAsync(
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? filter = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Generates and parses code coverage artefacts.</summary>
public interface ICoverageRunner
{
    /// <summary>Parses coverage XML and generates an HTML report when possible.</summary>
    Task<CoverageReportResult> ProcessAsync(
        ProjectContext context,
        string coverageXmlPath,
        CancellationToken cancellationToken = default);
}

/// <summary>Parses code coverage XML reports.</summary>
public interface ICoverageParser
{
    /// <summary>Parses a JaCoCo or Cobertura coverage file.</summary>
    Task<CoverageResult> ParseAsync(string coverageFile, CancellationToken cancellationToken = default);
}

/// <summary>Parses NUnit XML test results.</summary>
public interface IResultParser
{
    /// <summary>Parses NUnit XML from <paramref name="resultsFile"/>.</summary>
    Task<TestRunResult> ParseAsync(string resultsFile, CancellationToken cancellationToken = default);
}

/// <summary>Manages artefact directories and output files.</summary>
public interface IArtifactService
{
    /// <summary>Ensures the output directory exists and returns its path.</summary>
    string EnsureOutputDirectory(ProjectContext context);

    string GetTestResultsPath(ProjectContext context);

    string GetCoveragePath(ProjectContext context);

    string GetCoverageReportDirectory(ProjectContext context);

    string GetCoverageHtmlPath(ProjectContext context);

    string GetRunScriptPath(ProjectContext context);
}

/// <summary>Renders the interactive results dashboard.</summary>
public interface IDashboard
{
    /// <summary>Shows the dashboard and returns the exit code when the user quits.</summary>
    Task<int> ShowAsync(
        DashboardData data,
        IDashboardSession session,
        CancellationToken cancellationToken = default);
}

/// <summary>Run, scope, and watch actions available from the dashboard.</summary>
public interface IDashboardSession : IDisposable
{
    DiscoveryResult Discovery { get; }

    bool IsWatching { get; }

    Task<DashboardData?> RunAllAsync(CancellationToken cancellationToken = default);

    Task<DashboardData?> RunScopeAsync(CancellationToken cancellationToken = default);

    Task<DashboardData?> RunSelectionAsync(RunFilter filter, CancellationToken cancellationToken = default);

    Task<DashboardData?> RunAnalyserAsync(CancellationToken cancellationToken = default);

    Task<DashboardData?> EditScopeAsync(CancellationToken cancellationToken = default);

    Task<bool> ToggleWatchAsync(CancellationToken cancellationToken = default);

    bool TryConsumeWatchTrigger();
}

/// <summary>Executes PowerShell processes.</summary>
public interface IPowerShellRunner
{
    /// <summary>Locates <c>pwsh</c> and verifies Pester is available.</summary>
    Task EnsurePrerequisitesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a PowerShell script file.</summary>
    Task<PowerShellRunResult> RunScriptFileAsync(
        string scriptPath,
        string workingDirectory,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Watches the project for file changes.</summary>
public interface IFileWatcherService
{
}
