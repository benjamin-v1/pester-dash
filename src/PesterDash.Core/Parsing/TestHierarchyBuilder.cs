using System.Xml.Linq;
using PesterDash.Core.Models;

namespace PesterDash.Core.Parsing;

/// <summary>Builds a navigable test hierarchy from NUnit XML.</summary>
public static class TestHierarchyBuilder
{
    /// <summary>Updates leaf test nodes in an existing tree from a keyed result map.</summary>
    public static TestTreeNode UpdateFromTests(
        TestTreeNode existing,
        IReadOnlyDictionary<string, TestCaseResult> updates)
    {
        if (existing.Kind == TestTreeNodeKind.Test && existing.Test is not null)
        {
            var key = TestRunResultMerger.GetKey(existing.Test);
            if (updates.TryGetValue(key, out var updated))
            {
                return new TestTreeNode
                {
                    Name = existing.Name,
                    Kind = TestTreeNodeKind.Test,
                    Test = updated,
                };
            }

            return existing;
        }

        var children = existing.Children
            .Select(child => UpdateFromTests(child, updates))
            .ToList();

        return new TestTreeNode
        {
            Name = existing.Name,
            Kind = existing.Kind,
            SourcePath = existing.SourcePath,
            Children = children,
        };
    }

    public static TestTreeNode Build(XDocument document)
    {
        var root = document.Root;
        if (root is null)
        {
            return new TestTreeNode { Name = "Tests", Kind = TestTreeNodeKind.Root };
        }

        var children = new List<TestTreeNode>();

        foreach (var suite in FindChildSuites(root))
        {
            var node = ParseSuite(suite, depth: 0);
            if (IsWrapperSuite(node))
            {
                children.AddRange(node.Children);
            }
            else
            {
                children.Add(node);
            }
        }

        return new TestTreeNode
        {
            Name = "Tests",
            Kind = TestTreeNodeKind.Root,
            Children = children,
        };
    }

    private static TestTreeNode ParseSuite(XElement suite, int depth)
    {
        var rawName = (string?)suite.Attribute("name")
            ?? (string?)suite.Attribute("fullname")
            ?? "Unknown";

        var kind = ClassifySuite(rawName, depth);
        var name = kind == TestTreeNodeKind.File
            ? Path.GetFileName(rawName)
            : rawName;

        var children = new List<TestTreeNode>();

        foreach (var child in FindChildElements(suite))
        {
            if (IsTestCaseElement(child.Name.LocalName))
            {
                children.Add(ParseTestCase(child, name, kind));
            }
            else if (IsSuiteElement(child.Name.LocalName))
            {
                children.Add(ParseSuite(child, depth + 1));
            }
        }

        return new TestTreeNode
        {
            Name = name,
            Kind = kind,
            SourcePath = kind == TestTreeNodeKind.File ? rawName : null,
            Children = children,
        };
    }

    private static TestTreeNode ParseTestCase(XElement element, string parentName, TestTreeNodeKind parentKind)
    {
        var test = NUnitResultParser.ReadTestCase(element);
        var displayName = GetTestDisplayName(test.Name, parentName);

        return new TestTreeNode
        {
            Name = displayName,
            Kind = TestTreeNodeKind.Test,
            Test = test,
        };
    }

    private static TestTreeNodeKind ClassifySuite(string name, int depth)
    {
        if (IsFileName(name))
        {
            return TestTreeNodeKind.File;
        }

        if (name.Equals("Pester", StringComparison.OrdinalIgnoreCase))
        {
            return TestTreeNodeKind.Root;
        }

        return depth <= 2
            ? TestTreeNodeKind.Describe
            : TestTreeNodeKind.Context;
    }

    private static bool IsWrapperSuite(TestTreeNode node) =>
        node.Kind == TestTreeNodeKind.Root
        || node.Name.Equals("Pester", StringComparison.OrdinalIgnoreCase);

    private static bool IsFileName(string name) =>
        name.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
        || name.Contains(Path.DirectorySeparatorChar)
        || name.Contains(Path.AltDirectorySeparatorChar);

    private static string GetTestDisplayName(string fullName, string parentName)
    {
        if (fullName.StartsWith(parentName + ".", StringComparison.OrdinalIgnoreCase))
        {
            return fullName[(parentName.Length + 1)..];
        }

        var parts = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[^1] : fullName;
    }

    private static IEnumerable<XElement> FindChildSuites(XElement element)
    {
        var results = element.Element("results");
        if (results is not null)
        {
            foreach (var child in results.Elements())
            {
                if (IsSuiteElement(child.Name.LocalName))
                {
                    yield return child;
                }
            }

            yield break;
        }

        foreach (var child in element.Elements())
        {
            if (IsSuiteElement(child.Name.LocalName))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<XElement> FindChildElements(XElement suite)
    {
        var results = suite.Element("results");
        if (results is not null)
        {
            foreach (var child in results.Elements())
            {
                yield return child;
            }

            yield break;
        }

        foreach (var child in suite.Elements())
        {
            yield return child;
        }
    }

    private static bool IsSuiteElement(string localName) =>
        localName.Equals("test-suite", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("testsuite", StringComparison.OrdinalIgnoreCase);

    private static bool IsTestCaseElement(string localName) =>
        localName.Equals("test-case", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("testcase", StringComparison.OrdinalIgnoreCase);
}
