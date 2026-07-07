using PesterDash.Core.Configuration;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Core.Tests.Configuration;

public class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenConfigFileMissing()
    {
        var tempDir = CreateTempDirectory();

        var options = ConfigurationLoader.Load(tempDir);

        Assert.True(options.Coverage);
        Assert.True(options.Parallel);
        Assert.Equal(100, options.MaxFailures);
        Assert.Equal(".pester-dash/results", options.OutputDirectory);
        Assert.Empty(options.IgnoredDirectories);
    }

    [Fact]
    public void Load_BindsOptions_FromConfigFile()
    {
        var tempDir = CreateTempDirectory();
        ProjectStorePaths.EnsureStore(tempDir);
        var configPath = Path.Combine(tempDir, ProjectStorePaths.StoreDirectoryName, ProjectStorePaths.ConfigFileName);
        File.WriteAllText(configPath, """
            {
              "coverage": false,
              "watch": true,
              "parallel": false,
              "maxFailures": 5,
              "outputDirectory": "out",
              "ignoredDirectories": [ "docs" ]
            }
            """);

        var options = ConfigurationLoader.Load(tempDir);

        Assert.False(options.Coverage);
        Assert.True(options.Watch);
        Assert.False(options.Parallel);
        Assert.Equal(5, options.MaxFailures);
        Assert.Equal("out", options.OutputDirectory);
        Assert.Equal(["docs"], options.IgnoredDirectories);
    }

    [Fact]
    public void Save_WritesConfigToProjectRoot()
    {
        var tempDir = CreateTempDirectory();
        var options = new PesterDashOptions
        {
            Coverage = false,
            RunScope = new RunScopeOptions
            {
                TestFiles = ["tests/Alpha.Tests.ps1"],
                SourceFiles = ["src/Alpha.ps1"],
            },
        };

        ConfigurationLoader.Save(tempDir, options);

        var configPath = Path.Combine(tempDir, ProjectStorePaths.StoreDirectoryName, ProjectStorePaths.ConfigFileName);
        Assert.True(File.Exists(configPath));

        var loaded = ConfigurationLoader.Load(tempDir);
        Assert.False(loaded.Coverage);
        Assert.Equal(["tests/Alpha.Tests.ps1"], loaded.RunScope.TestFiles);
        Assert.Equal(["src/Alpha.ps1"], loaded.RunScope.SourceFiles);
    }

    [Fact]
    public void Load_BindsRunScope_FromConfigFile()
    {
        var tempDir = CreateTempDirectory();
        ProjectStorePaths.EnsureStore(tempDir);
        var configPath = Path.Combine(tempDir, ProjectStorePaths.StoreDirectoryName, ProjectStorePaths.ConfigFileName);
        File.WriteAllText(configPath, """
            {
              "runScope": {
                "testFiles": [ "tests/Foo.Tests.ps1" ],
                "sourceFiles": [ "src/Foo.ps1" ]
              }
            }
            """);

        var options = ConfigurationLoader.Load(tempDir);

        Assert.Equal(["tests/Foo.Tests.ps1"], options.RunScope.TestFiles);
        Assert.Equal(["src/Foo.ps1"], options.RunScope.SourceFiles);
        Assert.True(options.RunScope.IsConfigured);
    }

    [Fact]
    public void FindConfigFile_WalksUpDirectoryTree()
    {
        var root = CreateTempDirectory();
        var nested = Path.Combine(root, "src", "module");
        Directory.CreateDirectory(nested);

        var configPath = Path.Combine(root, ProjectStorePaths.LegacyConfigFileName);
        File.WriteAllText(configPath, "{}");

        var found = ConfigurationLoader.FindConfigFile(nested);

        Assert.Equal(configPath, found);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pesterdash-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
