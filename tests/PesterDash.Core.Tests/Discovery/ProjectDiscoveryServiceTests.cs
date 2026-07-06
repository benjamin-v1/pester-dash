using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;

namespace PesterDash.Core.Tests.Discovery;

public class ProjectDiscoveryServiceTests
{
    private readonly ProjectDiscoveryService _sut = new();

    [Fact]
    public void Discover_FindsTestsByFileNamePattern()
    {
        using var root = new TempProject();
        root.WriteFile("src/Install.Tests.ps1", "");
        root.WriteFile("src/Install.ps1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Single(result.TestFiles);
        Assert.EndsWith("Install.Tests.ps1", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.SourceFiles);
        Assert.EndsWith("Install.ps1", result.SourceFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Discover_FindsTestsInTestsDirectory()
    {
        using var root = new TempProject();
        root.WriteFile("tests/Install.ps1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Single(result.TestFiles);
        Assert.EndsWith(Path.Combine("tests", "Install.ps1"), result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Discover_FindsTestsInTestDirectory_CaseInsensitive()
    {
        using var root = new TempProject();
        root.WriteFile("Test/Helper.ps1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Single(result.TestFiles);
    }

    [Fact]
    public void Discover_IgnoresDefaultDirectories()
    {
        using var root = new TempProject();
        root.WriteFile("bin/Build.Tests.ps1", "");
        root.WriteFile("obj/Build.Tests.ps1", "");
        root.WriteFile(".git/hooks/pre-commit.ps1", "");
        root.WriteFile("node_modules/pkg/test.ps1", "");
        root.WriteFile("src/Real.Tests.ps1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Single(result.TestFiles);
        Assert.Contains("Real.Tests.ps1", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Discover_IgnoresConfiguredDirectories()
    {
        using var root = new TempProject();
        root.WriteFile("docs/Guide.Tests.ps1", "");
        root.WriteFile("src/Real.Tests.ps1", "");

        var options = new PesterDashOptions { IgnoredDirectories = ["docs"] };
        var result = _sut.Discover(root.Path, options);

        Assert.Single(result.TestFiles);
        Assert.Contains("Real.Tests.ps1", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Discover_IncludesPsm1AndPsd1SourceFiles()
    {
        using var root = new TempProject();
        root.WriteFile("src/Module.psm1", "");
        root.WriteFile("src/Module.psd1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Equal(2, result.SourceFiles.Count);
        Assert.Empty(result.TestFiles);
    }

    [Fact]
    public void Discover_ThrowsWhenProjectRootMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() =>
            _sut.Discover(path, new PesterDashOptions()));
    }

    [Fact]
    public void Discover_ReturnsSortedPaths()
    {
        using var root = new TempProject();
        root.WriteFile("tests/Zebra.Tests.ps1", "");
        root.WriteFile("tests/Alpha.Tests.ps1", "");

        var result = _sut.Discover(root.Path, new PesterDashOptions());

        Assert.Equal(2, result.TestFiles.Count);
        Assert.Contains("Alpha", result.TestFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Zebra", result.TestFiles[1], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempProject : IDisposable
    {
        public string Path { get; }

        public TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pesterdash-discovery", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath);
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
