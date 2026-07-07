namespace PesterDash.Core.Configuration;

/// <summary>
/// Application settings loaded from <c>pesterdash.json</c>.
/// </summary>
public sealed class PesterDashOptions
{
    public const string SectionName = "PesterDash";

    /// <summary>Whether to collect code coverage.</summary>
    public bool Coverage { get; set; } = true;

    /// <summary>Whether watch mode is enabled by default.</summary>
    public bool Watch { get; set; }

    /// <summary>Whether to run tests in parallel when supported by Pester.</summary>
    public bool Parallel { get; set; } = true;

    /// <summary>Maximum number of failures before stopping (0 = unlimited).</summary>
    public int MaxFailures { get; set; } = 100;

    /// <summary>Directory for test results and coverage artefacts.</summary>
    public string OutputDirectory { get; set; } = ".pester-dash/results";

    /// <summary>Whether to run PSScriptAnalyzer on scoped source files.</summary>
    public bool Analyser { get; set; } = true;

    /// <summary>Additional directory names to exclude from discovery.</summary>
    public IList<string> IgnoredDirectories { get; set; } = [];

    /// <summary>Write full Pester process output and run metadata to a debug log file.</summary>
    public bool Debug { get; set; }

    /// <summary>User-selected test and source files for runs.</summary>
    public RunScopeOptions RunScope { get; set; } = new();
}
