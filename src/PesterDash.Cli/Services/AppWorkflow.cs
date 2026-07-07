using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Core.Parsing;
using PesterDash.Core.ProjectStore;
using PesterDash.Cli.Views;
using Spectre.Console;

namespace PesterDash.Cli.Services;

/// <summary>Central orchestration for open, run, scope, and persistence.</summary>
internal sealed class AppWorkflow
{
    private readonly IServiceProvider _services;

    public AppWorkflow(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<(ProjectContext Context, DiscoveryResult Discovery)> PrepareProjectAsync(
        string projectRoot,
        PesterDashOptions? overrides = null,
        CancellationToken cancellationToken = default)
    {
        var context = ServiceCollectionExtensions.CreateProjectContext(projectRoot, overrides);
        ProjectStorePaths.EnsureStore(context.ProjectRoot);

        var discovery = _services.GetRequiredService<IProjectDiscovery>()
            .Discover(context.ProjectRoot, context.Options);

        return (context, discovery);
    }

    public async Task<DashboardData?> LoadLastRunAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LastRunSnapshot.LoadAsync(context.ProjectRoot, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return CreateEmptyDashboard(context, "No previous run. Press F5 to run all tests, S to configure scope.");
        }

        return await snapshot.ToDashboardAsync(
            context,
            _services.GetRequiredService<IResultParser>(),
            _services.GetRequiredService<ICoverageParser>(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int ExitCode, DashboardData? Data)> RunTestsAsync(
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? filter = null,
        IProgress<string>? progress = null,
        TestRunResult? priorResults = null,
        CancellationToken cancellationToken = default)
    {
        var (exitCode, data) = await TestRunWorkflow.ExecuteAsync(
            _services,
            context,
            discovery,
            filter,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (data is not null)
        {
            if (ShouldMergeResults(filter, priorResults, data.TestRun))
            {
                data = WithTestRun(data, TestRunResultMerger.Merge(priorResults!, data.TestRun));
                exitCode = data.TestRun.Failed > 0
                    ? Math.Min(data.TestRun.Failed, context.Options.MaxFailures)
                    : 0;
            }

            await SaveLastRunAsync(context, data, filter, cancellationToken).ConfigureAwait(false);
        }

        return (exitCode, data);
    }

    public async Task<DashboardData?> ResolveScopeInteractivelyAsync(
        ProjectContext context,
        DiscoveryResult discovery,
        bool forcePrompt,
        CancellationToken cancellationToken = default)
    {
        var needsPrompt = forcePrompt || !context.Options.RunScope.IsConfigured;
        if (!needsPrompt)
        {
            return null;
        }

        if (Console.IsInputRedirected)
        {
            return null;
        }

        var selectedScope = await ScopeSelectorView.SelectAsync(
            discovery,
            context.Options.RunScope,
            cancellationToken).ConfigureAwait(false);

        if (selectedScope is null)
        {
            return null;
        }

        context.Options.RunScope = selectedScope;
        ConfigurationLoader.Save(context.ProjectRoot, context.Options);
        return CreateEmptyDashboard(
            context,
            "Scope saved. Press F5 to run tests.");
    }

    public DiscoveryResult ApplyScope(ProjectContext context, DiscoveryResult discovery) =>
        RunScopeApplicator.Apply(discovery, context.Options);

    public async Task SaveLastRunAsync(
        ProjectContext context,
        DashboardData data,
        RunFilter? filter,
        CancellationToken cancellationToken = default)
    {
        var snapshot = LastRunSnapshot.FromDashboard(data, filter);
        await LastRunSnapshot.SaveAsync(context.ProjectRoot, snapshot, cancellationToken).ConfigureAwait(false);
    }

    public static DashboardData CreateEmptyDashboard(ProjectContext context, string? statusMessage = null) =>
        new()
        {
            Context = context,
            TestRun = new TestRunResult
            {
                ResultsFile = string.Empty,
                Total = 0,
                Passed = 0,
                Failed = 0,
                Skipped = 0,
                Duration = TimeSpan.Zero,
                Tests = [],
            },
            StatusMessage = statusMessage,
        };

    public DashboardSession CreateSession(
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? lastFilter = null,
        TestRunResult? initialTestRun = null,
        DashboardData? initialDashboardData = null) =>
        new(_services, this, context, discovery, lastFilter, initialTestRun, initialDashboardData);

    private static bool ShouldMergeResults(RunFilter? filter, TestRunResult? priorResults, TestRunResult incoming) =>
        filter?.IsActive == true
        && priorResults?.Total > 0
        && incoming.Total > 0;

    private static DashboardData WithTestRun(DashboardData data, TestRunResult testRun) =>
        new()
        {
            Context = data.Context,
            TestRun = testRun,
            Coverage = data.Coverage,
            Analyser = data.Analyser,
            RunOutput = data.RunOutput,
            RunLogPath = data.RunLogPath,
            DebugLogPath = data.DebugLogPath,
            ProcessStderr = data.ProcessStderr,
            StatusMessage = data.StatusMessage,
            LastFilterDescription = data.LastFilterDescription,
            LastFilter = data.LastFilter,
            IsWatching = data.IsWatching,
        };
}
