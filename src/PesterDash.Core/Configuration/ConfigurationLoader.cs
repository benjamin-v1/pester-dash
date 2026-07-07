using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Core.Configuration;

/// <summary>
/// Loads and saves <see cref="PesterDashOptions"/> from the project store or legacy <c>pesterdash.json</c>.
/// </summary>
public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Loads options from <paramref name="projectRoot"/>.
    /// Prefers <c>.pester-dash/config.json</c>, then legacy <c>pesterdash.json</c>.
    /// </summary>
    public static PesterDashOptions Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        ProjectStorePaths.EnsureStore(root);

        var configPath = ResolveConfigPath(root);
        if (configPath is null)
        {
            return CreateDefaultOptions(root);
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var options = CreateDefaultOptions(root);
        configuration.Bind(options);
        return options;
    }

    /// <summary>Saves options to <c>.pester-dash/config.json</c>.</summary>
    public static void Save(string projectRoot, PesterDashOptions options)
    {
        var root = Path.GetFullPath(projectRoot);
        ProjectStorePaths.EnsureStore(root);
        var configPath = ProjectStorePaths.GetConfigPath(root);
        var json = JsonSerializer.Serialize(options, SerializerOptions);
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Finds config starting at <paramref name="startDirectory"/> and walking up.
    /// </summary>
    public static string? FindConfigFile(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            var storeConfig = Path.Combine(directory.FullName, ProjectStorePaths.StoreDirectoryName, ProjectStorePaths.ConfigFileName);
            if (File.Exists(storeConfig))
            {
                return storeConfig;
            }

            var legacy = Path.Combine(directory.FullName, ProjectStorePaths.LegacyConfigFileName);
            if (File.Exists(legacy))
            {
                return legacy;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>Path to <c>.pester-dash/config.json</c> in the project root.</summary>
    public static string GetProjectConfigPath(string projectRoot) =>
        ProjectStorePaths.GetConfigPath(projectRoot);

    private static string? ResolveConfigPath(string projectRoot)
    {
        var storeConfig = ProjectStorePaths.GetConfigPath(projectRoot);
        if (File.Exists(storeConfig))
        {
            return storeConfig;
        }

        var legacy = ProjectStorePaths.GetLegacyConfigPath(projectRoot);
        if (File.Exists(legacy))
        {
            return legacy;
        }

        return null;
    }

    private static PesterDashOptions CreateDefaultOptions(string projectRoot)
    {
        _ = projectRoot;
        return new PesterDashOptions
        {
            OutputDirectory = $"{ProjectStorePaths.StoreDirectoryName}/{ProjectStorePaths.ResultsDirectoryName}",
        };
    }
}
