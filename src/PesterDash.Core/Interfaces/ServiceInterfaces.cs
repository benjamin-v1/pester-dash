namespace PesterDash.Core.Interfaces;

/// <summary>Discovers test and source files in a PowerShell project.</summary>
public interface IProjectDiscovery
{
}

/// <summary>Runs Pester tests via an external <c>pwsh</c> process.</summary>
public interface ITestRunner
{
}

/// <summary>Generates and parses code coverage artefacts.</summary>
public interface ICoverageRunner
{
}

/// <summary>Parses NUnit XML test results.</summary>
public interface IResultParser
{
}

/// <summary>Manages artefact directories and output files.</summary>
public interface IArtifactService
{
}

/// <summary>Renders the interactive results dashboard.</summary>
public interface IDashboard
{
}

/// <summary>Executes PowerShell processes.</summary>
public interface IPowerShellRunner
{
}

/// <summary>Watches the project for file changes.</summary>
public interface IFileWatcherService
{
}
