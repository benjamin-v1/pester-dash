using Microsoft.Extensions.Configuration;

namespace PesterDash.Core.Configuration;

/// <summary>
/// Loads <see cref="PesterDashOptions"/> from <c>pesterdash.json</c> near the project root.
/// </summary>
public static class ConfigurationLoader
{
    public const string ConfigFileName = "pesterdash.json";

    /// <summary>
    /// Loads options from <paramref name="projectRoot"/>, walking up to the filesystem root when needed.
    /// </summary>
    public static PesterDashOptions Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var configPath = FindConfigFile(root);

        if (configPath is null)
        {
            return new PesterDashOptions();
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var options = new PesterDashOptions();
        configuration.Bind(options);
        return options;
    }

    /// <summary>
    /// Finds <c>pesterdash.json</c> starting at <paramref name="startDirectory"/> and walking up.
    /// </summary>
    public static string? FindConfigFile(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ConfigFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
