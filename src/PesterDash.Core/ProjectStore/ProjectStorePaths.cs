namespace PesterDash.Core.ProjectStore;

/// <summary>Paths under the project-local <c>.pester-dash</c> directory.</summary>
public static class ProjectStorePaths
{
    public const string StoreDirectoryName = ".pester-dash";
    public const string ResultsDirectoryName = "results";
    public const string LogsDirectoryName = "logs";
    public const string AnalyserDirectoryName = "analyser";
    public const string ConfigFileName = "config.json";
    public const string LastRunFileName = "last-run.json";
    public const string LegacyConfigFileName = "pesterdash.json";

    public static string GetStoreRoot(string projectRoot) =>
        Path.Combine(Path.GetFullPath(projectRoot), StoreDirectoryName);

    public static string GetConfigPath(string projectRoot) =>
        Path.Combine(GetStoreRoot(projectRoot), ConfigFileName);

    public static string GetLastRunPath(string projectRoot) =>
        Path.Combine(GetStoreRoot(projectRoot), LastRunFileName);

    public static string GetResultsDirectory(string projectRoot) =>
        Path.Combine(GetStoreRoot(projectRoot), ResultsDirectoryName);

    public static string GetLogsDirectory(string projectRoot) =>
        Path.Combine(GetStoreRoot(projectRoot), LogsDirectoryName);

    public static string GetAnalyserDirectory(string projectRoot) =>
        Path.Combine(GetStoreRoot(projectRoot), AnalyserDirectoryName);

    public static string GetLegacyConfigPath(string projectRoot) =>
        Path.Combine(Path.GetFullPath(projectRoot), LegacyConfigFileName);

    public static void EnsureStore(string projectRoot)
    {
        Directory.CreateDirectory(GetStoreRoot(projectRoot));
        Directory.CreateDirectory(GetResultsDirectory(projectRoot));
        Directory.CreateDirectory(GetLogsDirectory(projectRoot));
        Directory.CreateDirectory(GetAnalyserDirectory(projectRoot));
    }
}
