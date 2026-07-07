using System.Globalization;
using System.Xml.Linq;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Core.Parsing;

/// <summary>Parses NUnit XML test results produced by Pester.</summary>
public sealed class NUnitResultParser : IResultParser
{
    /// <inheritdoc />
    public async Task<TestRunResult> ParseAsync(string resultsFile, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resultsFile))
        {
            throw new FileNotFoundException($"Test results file not found: {resultsFile}", resultsFile);
        }

        await using var stream = File.OpenRead(resultsFile);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        var root = document.Root;
        var tests = document
            .Descendants()
            .Where(element => IsTestCaseElement(element.Name.LocalName))
            .Select(ReadTestCase)
            .ToList();

        var passed = tests.Count(test => test.Outcome == TestOutcome.Passed);
        var failed = tests.Count(test => test.Outcome == TestOutcome.Failed);
        var skipped = tests.Count(test => test.Outcome is TestOutcome.Skipped or TestOutcome.Inconclusive);

        var total = tests.Count > 0
            ? tests.Count
            : ReadIntAttribute(root, "total") ?? 0;

        if (tests.Count == 0)
        {
            passed = ReadIntAttribute(root, "passed") ?? 0;
            failed = ReadIntAttribute(root, "failed") ?? 0;
            skipped = ReadIntAttribute(root, "skipped") ?? ReadIntAttribute(root, "ignored") ?? 0;
        }

        var durationSeconds = ReadDoubleAttribute(root, "duration")
            ?? ReadDoubleAttribute(root, "time")
            ?? tests.Sum(test => test.Duration.TotalSeconds);

        return new TestRunResult
        {
            ResultsFile = resultsFile,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            Tests = tests,
            Tree = TestHierarchyBuilder.Build(document),
        };
    }

    internal static TestCaseResult ReadTestCase(XElement element)
    {
        var failure = element.Element("failure");
        var message = failure?.Element("message")?.Value.Trim()
            ?? element.Element("message")?.Value.Trim();
        var stackTrace = failure?.Element("stack-trace")?.Value.Trim()
            ?? failure?.Element("stacktrace")?.Value.Trim()
            ?? element.Element("stack-trace")?.Value.Trim();

        return new TestCaseResult
        {
            Name = (string?)element.Attribute("name")
                ?? (string?)element.Attribute("fullname")
                ?? "(unknown)",
            FullName = (string?)element.Attribute("fullname"),
            Outcome = ReadOutcome(element),
            Duration = ReadDuration(element),
            ErrorMessage = string.IsNullOrWhiteSpace(message) ? null : message,
            StackTrace = string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace,
        };
    }

    private static bool IsTestCaseElement(string localName) =>
        localName.Equals("test-case", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("testcase", StringComparison.OrdinalIgnoreCase);

    private static TestOutcome ReadOutcome(XElement element)
    {
        var result = (string?)element.Attribute("result");
        if (!string.IsNullOrWhiteSpace(result))
        {
            return result.ToLowerInvariant() switch
            {
                "passed" or "success" => TestOutcome.Passed,
                "failed" or "failure" => TestOutcome.Failed,
                "skipped" or "ignored" => TestOutcome.Skipped,
                "inconclusive" => TestOutcome.Inconclusive,
                _ => TestOutcome.Inconclusive,
            };
        }

        var success = (string?)element.Attribute("success");
        if (bool.TryParse(success, out var succeeded))
        {
            return succeeded ? TestOutcome.Passed : TestOutcome.Failed;
        }

        var executed = (string?)element.Attribute("executed");
        if (bool.TryParse(executed, out var wasExecuted) && !wasExecuted)
        {
            return TestOutcome.Skipped;
        }

        return TestOutcome.Inconclusive;
    }

    private static TimeSpan ReadDuration(XElement element)
    {
        var seconds = ReadDoubleAttribute(element, "time")
            ?? ReadDoubleAttribute(element, "duration");

        return seconds.HasValue
            ? TimeSpan.FromSeconds(seconds.Value)
            : TimeSpan.Zero;
    }

    private static int? ReadIntAttribute(XElement? element, string name)
    {
        var value = (string?)element?.Attribute(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? ReadDoubleAttribute(XElement? element, string name)
    {
        var value = (string?)element?.Attribute(name);
        return string.IsNullOrEmpty(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }
}
