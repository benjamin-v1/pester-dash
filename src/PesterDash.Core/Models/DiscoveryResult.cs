namespace PesterDash.Core.Models;

/// <summary>
/// Files discovered in a PowerShell project.
/// </summary>
public sealed class DiscoveryResult
{
    public required string ProjectRoot { get; init; }

    public required IReadOnlyList<string> TestFiles { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public int TotalFilesScanned { get; init; }

    public bool HasTests => TestFiles.Count > 0;
}
