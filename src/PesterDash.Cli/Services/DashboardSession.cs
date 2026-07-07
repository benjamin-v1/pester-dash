using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Cli.Views;
using Spectre.Console;

namespace PesterDash.Cli.Services;

internal sealed class DashboardSession : IDashboardSession, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly AppWorkflow _workflow;
    private readonly ProjectContext _context;
    private DiscoveryResult _discovery;
    private readonly WatchService _watchService = new();
    private RunFilter? _lastFilter;
    private TestRunResult _currentTestRun;
    private DashboardData? _lastDashboardData;

    public DashboardSession(
        IServiceProvider services,
        AppWorkflow workflow,
        ProjectContext context,
        DiscoveryResult discovery,
        RunFilter? lastFilter,
        TestRunResult? initialTestRun,
        DashboardData? initialDashboardData = null)
    {
        _services = services;
        _workflow = workflow;
        _context = context;
        _discovery = _workflow.ApplyScope(context, discovery);
        _lastFilter = lastFilter;
        _currentTestRun = initialTestRun ?? EmptyTestRun();
        _lastDashboardData = initialDashboardData;
    }

    public DiscoveryResult Discovery => _discovery;

    public bool IsWatching => _watchService.IsWatching;

    public bool TryConsumeWatchTrigger() => _watchService.TryConsumePendingRun();

    public async Task<DashboardData?> RunAllAsync(CancellationToken cancellationToken = default) =>
        await ExecuteRunAsync(new RunFilter(), "all tests in scope", cancellationToken).ConfigureAwait(false);

    public async Task<DashboardData?> RunScopeAsync(CancellationToken cancellationToken = default) =>
        await ExecuteRunAsync(_lastFilter ?? new RunFilter(), _lastFilter?.Describe() ?? "scope", cancellationToken).ConfigureAwait(false);

    public async Task<DashboardData?> RunSelectionAsync(RunFilter filter, CancellationToken cancellationToken = default) =>
        await ExecuteRunAsync(filter, filter.Describe(), cancellationToken).ConfigureAwait(false);

    public async Task<DashboardData?> RunAnalyserAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.Options.Analyser)
        {
            return CopyWithSessionState(_lastDashboardData ?? CreateAnalyserOnlyData(AnalyserResult.Empty("Analyser is disabled in config.")));
        }

        if (_discovery.SourceFiles.Count == 0)
        {
            return CopyWithSessionState(_lastDashboardData ?? CreateAnalyserOnlyData(AnalyserResult.Empty("No source files in scope.")));
        }

        DashboardData? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Running PSScriptAnalyzer...", async _ =>
            {
                var analyser = _services.GetRequiredService<ScriptAnalyzerRunner>();
                var analyserResult = await analyser.RunAsync(
                    _context,
                    _discovery.SourceFiles,
                    cancellationToken).ConfigureAwait(false);

                var baseData = _lastDashboardData ?? new DashboardData
                {
                    Context = _context,
                    TestRun = _currentTestRun,
                };

                result = new DashboardData
                {
                    Context = baseData.Context,
                    TestRun = baseData.TestRun,
                    Coverage = baseData.Coverage,
                    Analyser = analyserResult,
                    RunOutput = baseData.RunOutput,
                    RunLogPath = baseData.RunLogPath,
                    DebugLogPath = baseData.DebugLogPath,
                    ProcessStderr = baseData.ProcessStderr,
                    StatusMessage = baseData.StatusMessage,
                    LastFilterDescription = baseData.LastFilterDescription ?? _lastFilter?.Describe() ?? "scope",
                    LastFilter = baseData.LastFilter ?? _lastFilter,
                    IsWatching = _watchService.IsWatching,
                };

                await _workflow.SaveLastRunAsync(_context, result, _lastFilter, cancellationToken).ConfigureAwait(false);
            });

        if (result is not null)
        {
            _lastDashboardData = result;
            result = CopyWithSessionState(result);
        }

        return result;
    }

    private DashboardData CreateAnalyserOnlyData(AnalyserResult analyser) =>
        new()
        {
            Context = _context,
            TestRun = _currentTestRun,
            Analyser = analyser,
            IsWatching = _watchService.IsWatching,
        };

    public async Task<DashboardData?> EditScopeAsync(CancellationToken cancellationToken = default)
    {
        var fullDiscovery = _services.GetRequiredService<IProjectDiscovery>()
            .Discover(_context.ProjectRoot, _context.Options);

        var selectedScope = await ScopeSelectorView.SelectAsync(
            fullDiscovery,
            _context.Options.RunScope,
            cancellationToken).ConfigureAwait(false);

        if (selectedScope is null)
        {
            return null;
        }

        _context.Options.RunScope = selectedScope;
        ConfigurationLoader.Save(_context.ProjectRoot, _context.Options);
        _discovery = _workflow.ApplyScope(_context, fullDiscovery);

        if (_watchService.IsWatching)
        {
            _watchService.Start(_context, _discovery);
        }

        _currentTestRun = EmptyTestRun();

        return new DashboardData
        {
            Context = _context,
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
            StatusMessage = "Scope updated. Press F5 to run tests.",
            IsWatching = _watchService.IsWatching,
        };
    }

    public Task<bool> ToggleWatchAsync(CancellationToken cancellationToken = default)
    {
        if (_watchService.IsWatching)
        {
            _watchService.Stop();
        }
        else
        {
            _watchService.Start(_context, _discovery);
        }

        return Task.FromResult(_watchService.IsWatching);
    }

    public void Dispose() => _watchService.Dispose();

    private async Task<DashboardData?> ExecuteRunAsync(
        RunFilter filter,
        string description,
        CancellationToken cancellationToken)
    {
        DashboardData? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Running {description}...", async statusCtx =>
            {
                var progress = new Progress<string>(line =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        statusCtx.Status(Markup.Escape(line));
                    }
                });

                (_, result) = await _workflow.RunTestsAsync(
                    _context,
                    _discovery,
                    filter.IsActive ? filter : null,
                    progress,
                    filter.IsActive && _currentTestRun.Total > 0 ? _currentTestRun : null,
                    cancellationToken).ConfigureAwait(false);
            });

        if (result is not null)
        {
            _currentTestRun = result.TestRun;
            _lastFilter = filter.IsActive ? filter : null;
            _lastDashboardData = result;
            result = CopyWithSessionState(result);
        }

        return result;
    }

    private DashboardData CopyWithSessionState(DashboardData data) =>
        new()
        {
            Context = data.Context,
            TestRun = data.TestRun,
            Coverage = data.Coverage,
            Analyser = data.Analyser,
            RunOutput = data.RunOutput,
            RunLogPath = data.RunLogPath,
            DebugLogPath = data.DebugLogPath,
            ProcessStderr = data.ProcessStderr,
            StatusMessage = data.StatusMessage,
            LastFilterDescription = _lastFilter?.Describe() ?? "scope",
            LastFilter = _lastFilter,
            IsWatching = _watchService.IsWatching,
        };

    private static TestRunResult EmptyTestRun() =>
        new()
        {
            ResultsFile = string.Empty,
            Total = 0,
            Passed = 0,
            Failed = 0,
            Skipped = 0,
            Duration = TimeSpan.Zero,
            Tests = [],
        };
}
