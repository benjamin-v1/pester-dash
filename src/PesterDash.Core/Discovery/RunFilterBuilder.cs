using PesterDash.Core.Models;

namespace PesterDash.Core.Discovery;

/// <summary>Builds <see cref="RunFilter"/> values from dashboard tree selection.</summary>
public static class RunFilterBuilder
{
    public static RunFilter FromTreePath(IReadOnlyList<TestTreeNode> path)
    {
        if (path.Count == 0)
        {
            return new RunFilter();
        }

        var node = path[^1];

        return node.Kind switch
        {
            TestTreeNodeKind.File when !string.IsNullOrWhiteSpace(node.SourcePath) =>
                new RunFilter { TestFiles = [node.SourcePath] },

            TestTreeNodeKind.Test when node.Test?.FullName is { Length: > 0 } fullName =>
                new RunFilter { FullNameFilters = [fullName] },

            TestTreeNodeKind.Describe or TestTreeNodeKind.Context =>
                new RunFilter { FullNameFilters = [BuildNamePattern(path)] },

            _ => new RunFilter(),
        };
    }

    public static RunFilter FromSelectedItem(TestTreeNode? node, IReadOnlyList<TestTreeNode> path)
    {
        if (node is null)
        {
            return new RunFilter();
        }

        if (node.Kind == TestTreeNodeKind.Test)
        {
            var fullName = node.Test?.FullName ?? node.Name;
            return new RunFilter { FullNameFilters = [fullName] };
        }

        if (node.Kind == TestTreeNodeKind.File && !string.IsNullOrWhiteSpace(node.SourcePath))
        {
            return new RunFilter { TestFiles = [node.SourcePath] };
        }

        var extendedPath = path.Append(node).ToList();
        return new RunFilter { FullNameFilters = [BuildNamePattern(extendedPath)] };
    }

    private static string BuildNamePattern(IReadOnlyList<TestTreeNode> path)
    {
        var parts = path
            .Where(node => node.Kind is TestTreeNodeKind.Describe or TestTreeNodeKind.Context)
            .Select(node => node.Name)
            .ToList();

        if (parts.Count == 0 && path.Count > 0)
        {
            parts.Add(path[^1].Name);
        }

        return "*" + string.Join('*', parts) + "*";
    }
}
