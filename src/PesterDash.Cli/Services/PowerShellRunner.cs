using System.Diagnostics;
using System.Text;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Services;

/// <summary>Executes PowerShell via <c>pwsh</c> process APIs.</summary>
public sealed class PowerShellRunner : IPowerShellRunner
{
    private string? _pwshPath;

    /// <inheritdoc />
    public async Task EnsurePrerequisitesAsync(CancellationToken cancellationToken = default)
    {
        _pwshPath = FindPwshExecutable()
            ?? throw new InvalidOperationException(
                "PowerShell 7+ (pwsh) was not found on PATH. Install from https://github.com/PowerShell/PowerShell");

        var versionCheck = await RunAsync(
            _pwshPath,
            "-NoProfile -NonInteractive -Command \"if (-not (Get-Module -ListAvailable Pester)) { exit 2 }; Import-Module Pester; if ((Get-Module Pester).Version.Major -lt 5) { exit 3 }\"",
            Environment.CurrentDirectory,
            outputProgress: null,
            cancellationToken).ConfigureAwait(false);

        if (versionCheck.ExitCode == 2)
        {
            throw new InvalidOperationException(
                "Pester is not installed. Run: Install-Module Pester -Scope CurrentUser -Force");
        }

        if (versionCheck.ExitCode == 3)
        {
            throw new InvalidOperationException(
                "Pester 5 or later is required. Run: Install-Module Pester -Scope CurrentUser -Force");
        }

        if (versionCheck.WasCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (versionCheck.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to verify Pester installation: {versionCheck.StandardError.Trim()}");
        }
    }

    /// <inheritdoc />
    public Task<PowerShellRunResult> RunScriptFileAsync(
        string scriptPath,
        string workingDirectory,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        _pwshPath ??= FindPwshExecutable()
            ?? throw new InvalidOperationException("PowerShell 7+ (pwsh) was not found on PATH.");

        var arguments = $"-NoProfile -NonInteractive -File \"{scriptPath}\"";
        return RunAsync(_pwshPath, arguments, workingDirectory, outputProgress, cancellationToken);
    }

    private static string? FindPwshExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("PATHEXT")?.Split(';') ?? [".EXE", ".CMD", ".BAT"]
            : [string.Empty];

        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), "pwsh" + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static async Task<PowerShellRunResult> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var cancelled = false;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stdout.AppendLine(e.Data);
            outputProgress?.Report(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stderr.AppendLine(e.Data);
            outputProgress?.Report(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {executable}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using var registration = cancellationToken.Register(() =>
        {
            cancelled = true;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have already exited.
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        return new PowerShellRunResult
        {
            ExitCode = process.HasExited ? process.ExitCode : -1,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            WasCancelled = cancelled,
        };
    }
}
