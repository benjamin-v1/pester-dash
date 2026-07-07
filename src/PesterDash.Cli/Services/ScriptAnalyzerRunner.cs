using System.Text;
using System.Text.Json;
using PesterDash.Core.Formatting;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Core.Parsing;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Cli.Services;

/// <summary>Runs PSScriptAnalyzer on scoped source files.</summary>
public sealed class ScriptAnalyzerRunner
{
    private readonly IPowerShellRunner _powerShellRunner;

    public ScriptAnalyzerRunner(IPowerShellRunner powerShellRunner)
    {
        _powerShellRunner = powerShellRunner;
    }

    public async Task<AnalyserResult> RunAsync(
        ProjectContext context,
        IReadOnlyList<string> sourceFiles,
        CancellationToken cancellationToken = default)
    {
        var validPaths = sourceFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPaths.Count == 0)
        {
            return AnalyserResult.Empty("No source files in scope.");
        }

        ProjectStorePaths.EnsureStore(context.ProjectRoot);
        var analyserDir = ProjectStorePaths.GetAnalyserDirectory(context.ProjectRoot);
        Directory.CreateDirectory(analyserDir);

        var pathsJsonPath = Path.Combine(analyserDir, "paths.json");
        var outputPath = Path.Combine(analyserDir, "results.json");
        var scriptPath = Path.Combine(analyserDir, "run.ps1");

        var pathsJson = JsonSerializer.Serialize(validPaths);
        await File.WriteAllTextAsync(pathsJsonPath, pathsJson, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        var script = BuildScript(pathsJsonPath, outputPath);
        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        var result = await _powerShellRunner.RunScriptFileAsync(
            scriptPath,
            context.ProjectRoot,
            outputProgress: null,
            cancellationToken).ConfigureAwait(false);

        if (result.WasCancelled)
        {
            return AnalyserResult.Empty("Analyser run cancelled.");
        }

        if (!File.Exists(outputPath))
        {
            var message = BuildFailureMessage(result);
            return AnalyserResult.Empty(message);
        }

        try
        {
            var json = await File.ReadAllTextAsync(outputPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AnalyserResult { Findings = [] };
            }

            var findings = AnalyserResultsParser.Parse(json);
            return new AnalyserResult { Findings = findings };
        }
        catch (Exception ex)
        {
            return AnalyserResult.Empty($"Failed to parse analyser output: {ex.Message}");
        }
    }

    private static string BuildFailureMessage(PowerShellRunResult result)
    {
        var stderr = result.StandardError?.Trim();
        var stdout = result.StandardOutput?.Trim();
        var combined = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;

        if (!string.IsNullOrWhiteSpace(combined))
        {
            return RunLogFormatter.GetStderrSummary(combined)
                ?? combined.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                ?? combined;
        }

        return "PSScriptAnalyzer did not produce results. Install with: Install-Module PSScriptAnalyzer -Scope CurrentUser";
    }

    private static string BuildScript(string pathsJsonPath, string outputPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {");
        builder.AppendLine("  Write-Error 'PSScriptAnalyzer module is not installed.'");
        builder.AppendLine("  exit 2");
        builder.AppendLine("}");
        builder.AppendLine("Import-Module PSScriptAnalyzer");
        builder.AppendLine($"$rawPaths = Get-Content -Raw -LiteralPath '{EscapePowerShellSingleQuoted(pathsJsonPath)}' | ConvertFrom-Json");
        builder.AppendLine("$pathList = foreach ($item in @($rawPaths)) { [string]$item }");
        builder.AppendLine("$results = foreach ($path in $pathList) {");
        builder.AppendLine("  Invoke-ScriptAnalyzer -Path $path -Recurse:$false -Severity @('Error','Warning','Information')");
        builder.AppendLine("}");
        builder.AppendLine("$results = @($results | Where-Object { $_ })");
        builder.AppendLine("if ($results.Count -eq 0) {");
        builder.AppendLine($"  '[]' | Set-Content -LiteralPath '{EscapePowerShellSingleQuoted(outputPath)}' -Encoding UTF8");
        builder.AppendLine("} else {");
        builder.AppendLine($"  $results | ConvertTo-Json -Depth 6 -AsArray | Set-Content -LiteralPath '{EscapePowerShellSingleQuoted(outputPath)}' -Encoding UTF8");
        builder.AppendLine("}");
        builder.AppendLine("exit 0");
        return builder.ToString();
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
