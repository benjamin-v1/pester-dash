using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Core.Parsing;
using PesterDash.Cli.Services;

namespace PesterDash.Cli;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPesterDash(this IServiceCollection services)
    {
        services.AddSingleton<PesterDashOptions>(_ => new PesterDashOptions());
        services.AddSingleton<IProjectDiscovery, ProjectDiscoveryService>();
        services.AddSingleton<IResultParser, NUnitResultParser>();
        services.AddSingleton<ICoverageParser, CoverageParser>();
        services.AddSingleton<IArtifactService, ArtifactService>();
        services.AddSingleton<IPowerShellRunner, PowerShellRunner>();
        services.AddSingleton<ITestRunner, PesterRunner>();
        services.AddSingleton<ICoverageRunner, CoverageRunner>();
        services.AddSingleton<ScriptAnalyzerRunner>();
        services.AddSingleton<AppWorkflow>();
        services.AddSingleton<IDashboard, DashboardService>();
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

            if (overrides.Debug)
            {
                options.Debug = true;
            }

            if (!string.IsNullOrWhiteSpace(overrides.OutputDirectory)
                && overrides.OutputDirectory != ".pester-dash/results")
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
