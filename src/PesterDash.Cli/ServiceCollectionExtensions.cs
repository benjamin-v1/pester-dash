using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Cli;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPesterDash(this IServiceCollection services)
    {
        services.AddSingleton<PesterDashOptions>(_ => new PesterDashOptions());
        services.AddSingleton<IProjectDiscovery, ProjectDiscoveryService>();
        return services;
    }

    public static ProjectContext CreateProjectContext(string projectRoot, PesterDashOptions? overrides = null)
    {
        var root = Path.GetFullPath(projectRoot);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Project root not found: {root}");
        }

        var options = ConfigurationLoader.Load(root);

        if (overrides is not null)
        {
            if (!overrides.Coverage)
            {
                options.Coverage = false;
            }

            if (!string.IsNullOrWhiteSpace(overrides.OutputDirectory)
                && overrides.OutputDirectory != ".artifacts")
            {
                options.OutputDirectory = overrides.OutputDirectory;
            }
        }

        return new ProjectContext
        {
            ProjectRoot = root,
            Options = options,
        };
    }
}
