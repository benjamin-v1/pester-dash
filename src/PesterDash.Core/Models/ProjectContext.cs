using PesterDash.Core.Configuration;

namespace PesterDash.Core.Models;

/// <summary>
/// Resolved project root and configuration for a command invocation.
/// </summary>
public sealed class ProjectContext
{
    public required string ProjectRoot { get; init; }

    public required PesterDashOptions Options { get; init; }

    public string OutputDirectory =>
        Path.GetFullPath(Path.Combine(ProjectRoot, Options.OutputDirectory));
}
