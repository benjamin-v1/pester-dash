namespace PesterDash.Core.Models;

/// <summary>Result of executing a PowerShell process.</summary>
public sealed class PowerShellRunResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public bool WasCancelled { get; init; }

    public bool Succeeded => ExitCode == 0 && !WasCancelled;
}
