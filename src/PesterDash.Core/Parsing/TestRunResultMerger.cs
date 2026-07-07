using PesterDash.Core.Models;

namespace PesterDash.Core.Parsing;

/// <summary>Merges partial test runs into an existing result set.</summary>
public static class TestRunResultMerger
{
    /// <summary>
    /// Replaces matching tests from <paramref name="incoming"/> into <paramref name="existing"/>
    /// and rebuilds aggregate counts and the navigable tree.
    /// </summary>
    public static TestRunResult Merge(TestRunResult existing, TestRunResult incoming)
    {
        var updates = incoming.Tests.ToDictionary(GetKey, test => test, StringComparer.OrdinalIgnoreCase);
        var mergedTests = new List<TestCaseResult>(existing.Tests.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var test in existing.Tests)
        {
            var key = GetKey(test);
            mergedTests.Add(updates.TryGetValue(key, out var updated) ? updated : test);
            seen.Add(key);
        }

        foreach (var test in incoming.Tests)
        {
            var key = GetKey(test);
            if (seen.Add(key))
            {
                mergedTests.Add(test);
            }
        }

        var tree = existing.Tree is not null
            ? TestHierarchyBuilder.UpdateFromTests(existing.Tree, updates)
            : incoming.Tree;

        return new TestRunResult
        {
            ResultsFile = incoming.ResultsFile,
            Tests = mergedTests,
            Total = mergedTests.Count,
            Passed = mergedTests.Count(test => test.Outcome == TestOutcome.Passed),
            Failed = mergedTests.Count(test => test.Outcome == TestOutcome.Failed),
            Skipped = mergedTests.Count(test => test.Outcome is TestOutcome.Skipped or TestOutcome.Inconclusive),
            Duration = TimeSpan.FromMilliseconds(mergedTests.Sum(test => test.Duration.TotalMilliseconds)),
            Tree = tree,
        };
    }

    internal static string GetKey(TestCaseResult test) =>
        string.IsNullOrWhiteSpace(test.FullName) ? test.Name : test.FullName!;
}
