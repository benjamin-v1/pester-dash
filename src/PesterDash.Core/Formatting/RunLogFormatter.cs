using System.Text.RegularExpressions;

namespace PesterDash.Core.Formatting;

/// <summary>Helpers for presenting Pester process output in the UI.</summary>
public static class RunLogFormatter
{
    private static readonly Regex AnsiEscapeRegex = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    public static string StripAnsi(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : AnsiEscapeRegex.Replace(text, string.Empty);

    public static string? GetStderrSummary(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return null;
        }

        foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleaned = StripAnsi(line).Trim();
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned.Length <= 140 ? cleaned : cleaned[..137] + "...";
            }
        }

        return null;
    }
}
