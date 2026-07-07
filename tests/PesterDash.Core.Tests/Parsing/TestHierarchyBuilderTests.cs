using System.Xml.Linq;
using PesterDash.Core.Models;
using PesterDash.Core.Parsing;

namespace PesterDash.Core.Tests.Parsing;

public class TestHierarchyBuilderTests
{
    [Fact]
    public void Build_CreatesFileDescribeTestHierarchy()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestResults.xml");
        var document = XDocument.Load(fixture);

        var tree = TestHierarchyBuilder.Build(document);

        Assert.Equal(TestTreeNodeKind.Root, tree.Kind);
        Assert.Single(tree.Children);

        var file = tree.Children[0];
        Assert.Equal(TestTreeNodeKind.File, file.Kind);
        Assert.Equal("Greeting.Tests.ps1", file.Name);

        Assert.Single(file.Children);
        var describe = file.Children[0];
        Assert.Equal("Get-Greeting", describe.Name);
        Assert.Equal(2, describe.Children.Count);
        Assert.Equal(1, describe.Passed);
        Assert.Equal(1, describe.Skipped);
    }
}
