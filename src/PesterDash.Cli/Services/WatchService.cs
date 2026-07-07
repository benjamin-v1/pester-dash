using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Core.ProjectStore;

namespace PesterDash.Cli.Services;

/// <summary>Watches scoped files and signals when a rerun should occur.</summary>
public sealed class WatchService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private int _pendingRuns;

    public bool IsWatching { get; private set; }

    public void Start(ProjectContext context, DiscoveryResult discovery)
    {
        Stop();

        var files = discovery.TestFiles
            .Concat(discovery.SourceFiles)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();

        var directories = files
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var directory in directories)
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnChanged;
            _watchers.Add(watcher);
        }

        IsWatching = files.Count > 0;
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        IsWatching = false;
        Interlocked.Exchange(ref _pendingRuns, 0);
    }

    public bool TryConsumePendingRun() =>
        Interlocked.Exchange(ref _pendingRuns, 0) > 0;

    public void Dispose() => Stop();

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name is null || e.Name.Contains(".pester-dash", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Interlocked.Increment(ref _pendingRuns);
    }
}
