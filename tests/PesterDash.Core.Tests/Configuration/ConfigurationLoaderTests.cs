using PesterDash.Core.Configuration;

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
        Assert.Equal(".artifacts", options.OutputDirectory);
        Assert.Empty(options.IgnoredDirectories);
    }

    [Fact]
    public void Load_BindsOptions_FromConfigFile()
    {
        var tempDir = CreateTempDirectory();
        var configPath = Path.Combine(tempDir, ConfigurationLoader.ConfigFileName);
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
    public void FindConfigFile_WalksUpDirectoryTree()
    {
        var root = CreateTempDirectory();
        var nested = Path.Combine(root, "src", "module");
        Directory.CreateDirectory(nested);

        var configPath = Path.Combine(root, ConfigurationLoader.ConfigFileName);
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
