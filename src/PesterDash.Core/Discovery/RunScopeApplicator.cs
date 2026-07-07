using PesterDash.Core.Configuration;
using PesterDash.Core.Models;

namespace PesterDash.Core.Discovery;

/// <summary>Applies persisted or default run scope to a discovery result.</summary>
public static class RunScopeApplicator
{
    /// <summary>
    /// Returns a discovery result limited to the configured scope, or smart defaults when unset.
    /// </summary>
    public static DiscoveryResult Apply(DiscoveryResult discovery, PesterDashOptions options)
    {
        if (options.RunScope.IsConfigured)
        {
            return ApplyConfiguredScope(discovery, options.RunScope);
        }

        return ApplyDefaultScope(discovery);
    }

    /// <summary>Builds relative-path scope entries from absolute file paths.</summary>
    public static RunScopeOptions CreateScopeFromSelection(
        string projectRoot,
        IEnumerable<string> selectedTestFiles,
        IEnumerable<string> selectedSourceFiles) =>
        new()
        {
            TestFiles = selectedTestFiles
                .Select(path => RunScopePath.ToRelative(projectRoot, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceFiles = selectedSourceFiles
                .Select(path => RunScopePath.ToRelative(projectRoot, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

    /// <summary>Resolves configured relative paths to discovered files.</summary>
    public static (IReadOnlyList<string> TestFiles, IReadOnlyList<string> SourceFiles) ResolveSelection(
        DiscoveryResult discovery,
        RunScopeOptions scope)
    {
        var testFiles = MatchConfiguredFiles(discovery.TestFiles, scope.TestFiles, discovery.ProjectRoot);
        var sourceFiles = MatchConfiguredFiles(discovery.SourceFiles, scope.SourceFiles, discovery.ProjectRoot);
        return (testFiles, sourceFiles);
    }

    private static DiscoveryResult ApplyConfiguredScope(DiscoveryResult discovery, RunScopeOptions scope)
    {
        var (testFiles, sourceFiles) = ResolveSelection(discovery, scope);

        return new DiscoveryResult
        {
            ProjectRoot = discovery.ProjectRoot,
            TestFiles = testFiles,
            SourceFiles = sourceFiles,
            TotalFilesScanned = discovery.TotalFilesScanned,
        };
    }

    private static DiscoveryResult ApplyDefaultScope(DiscoveryResult discovery) =>
        new()
        {
            ProjectRoot = discovery.ProjectRoot,
            TestFiles = RunScopeDefaults.SelectDefaultTestFiles(discovery.TestFiles),
            SourceFiles = RunScopeDefaults.SelectDefaultSourceFiles(discovery.SourceFiles),
            TotalFilesScanned = discovery.TotalFilesScanned,
        };

    private static List<string> MatchConfiguredFiles(
        IReadOnlyList<string> discoveredFiles,
        IList<string> configuredRelativePaths,
        string projectRoot)
    {
        var configured = new HashSet<string>(
            configuredRelativePaths.Select(RunScopePath.Normalize),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        return discoveredFiles
            .Where(file => configured.Contains(RunScopePath.ToRelative(projectRoot, file)))
            .ToList();
    }
}
