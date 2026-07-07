using PesterDash.Core.Models;

namespace PesterDash.Cli.Views;

internal sealed class TestNavigator
{
    private readonly TestTreeNode _root;
    private readonly List<TestTreeNode> _path = [];

    public TestNavigator(TestTreeNode root)
    {
        _root = root;
    }

    public bool FailuresOnly { get; set; }

    public string SearchQuery { get; set; } = string.Empty;

    public IReadOnlyList<TestTreeNode> Path => _path;

    public bool CanGoUp => _path.Count > 0;

    public TestTreeNode? CurrentNode => _path.Count > 0 ? _path[^1] : null;

    public string Breadcrumb =>
        _path.Count == 0
            ? "Tests"
            : string.Join(" › ", _path.Select(node => node.Name));

    public string LevelTitle
    {
        get
        {
            if (_path.Count == 0)
            {
                return "Test files";
            }

            return CurrentNode?.Kind switch
            {
                TestTreeNodeKind.File => "Describe blocks",
                TestTreeNodeKind.Describe => HasOnlyTestChildren() ? "Tests" : "Contexts",
                TestTreeNodeKind.Context => "Tests",
                _ => "Items",
            };
        }
    }

    public IReadOnlyList<TestTreeNode> VisibleItems
    {
        get
        {
            var items = (CurrentNode?.Children ?? _root.Children).AsEnumerable();

            if (FailuresOnly)
            {
                items = items.Where(item =>
                    item.Kind == TestTreeNodeKind.Test
                        ? item.Test?.Outcome == TestOutcome.Failed
                        : item.HasFailures);
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                items = items.Where(item =>
                    item.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            return items
                .OrderBy(item => item.Kind == TestTreeNodeKind.Test ? 1 : 0)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public void DrillDown(TestTreeNode node)
    {
        _path.Add(node);
    }

    public void GoUp()
    {
        if (_path.Count > 0)
        {
            _path.RemoveAt(_path.Count - 1);
        }
    }

    public void GoRoot()
    {
        _path.Clear();
    }

    private bool HasOnlyTestChildren() =>
        CurrentNode?.Children.All(child => child.Kind == TestTreeNodeKind.Test) == true;
}
