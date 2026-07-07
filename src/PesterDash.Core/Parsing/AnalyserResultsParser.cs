using System.Text.Json;
using PesterDash.Core.Models;

namespace PesterDash.Core.Parsing;

/// <summary>Parses PSScriptAnalyzer JSON output.</summary>
public static class AnalyserResultsParser
{
    public static IReadOnlyList<AnalyserFinding> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(ReadFinding).Where(f => f is not null).Cast<AnalyserFinding>().ToList();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            var single = ReadFinding(root);
            return single is null ? [] : [single];
        }

        return [];
    }

    private static AnalyserFinding? ReadFinding(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var ruleName = GetString(element, "RuleName") ?? GetString(element, "ruleName") ?? "Unknown";
        var severity = GetSeverity(element);
        var message = GetString(element, "Message") ?? GetString(element, "message") ?? string.Empty;
        var scriptPath = GetString(element, "ScriptPath")
            ?? GetString(element, "scriptPath")
            ?? GetString(element, "ScriptName")
            ?? GetString(element, "scriptName")
            ?? string.Empty;
        var line = GetInt(element, "Line")
            ?? GetInt(element, "line")
            ?? GetExtentInt(element, "StartLineNumber")
            ?? 0;
        var column = GetInt(element, "Column")
            ?? GetInt(element, "column")
            ?? GetExtentInt(element, "StartColumnNumber")
            ?? 0;

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return null;
        }

        return new AnalyserFinding
        {
            RuleName = ruleName,
            Severity = severity,
            Message = message,
            ScriptPath = scriptPath,
            Line = line,
            Column = column,
        };
    }

    private static int? GetExtentInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty("Extent", out var extent) || extent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetInt(extent, property);
    }

    private static string GetSeverity(JsonElement element)
    {
        if (!element.TryGetProperty("Severity", out var value)
            && !element.TryGetProperty("severity", out value))
        {
            return "Warning";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "Warning",
            JsonValueKind.Number when value.TryGetInt32(out var severity) => severity switch
            {
                0 => "Error",
                1 => "Warning",
                2 => "Information",
                _ => severity.ToString(),
            },
            _ => "Warning",
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? GetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }
}
