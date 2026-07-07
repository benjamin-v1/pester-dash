namespace PesterDash.Core.Configuration;

/// <summary>
/// User-selected test and source files for a run, persisted in <c>pesterdash.json</c>.
/// Paths are relative to the project root.
/// </summary>
public sealed class RunScopeOptions
{
    /// <summary>Test files to include in Pester runs.</summary>
    public IList<string> TestFiles { get; set; } = [];

    /// <summary>Source files to include in code coverage.</summary>
    public IList<string> SourceFiles { get; set; } = [];

    /// <summary>Whether the user has saved a run scope at least once.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsConfigured => TestFiles.Count > 0;
}
