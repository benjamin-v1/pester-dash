using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Models;

namespace PesterDash.Cli.Views;

internal enum ScopeSelectorTab
{
    Tests,
    Sources,
}

internal sealed class ScopeSelectorState
{
    public required string ProjectRoot { get; init; }

    public required IReadOnlyList<string> TestFiles { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public required HashSet<string> SelectedTests { get; init; }

    public required HashSet<string> SelectedSources { get; init; }

    public ScopeSelectorTab ActiveTab { get; set; } = ScopeSelectorTab.Tests;

    public int SelectedIndex { get; set; }

    public string? FlashMessage { get; set; }

    public IReadOnlyList<string> ActiveFiles =>
        ActiveTab == ScopeSelectorTab.Tests ? TestFiles : SourceFiles;

    public HashSet<string> ActiveSelection =>
        ActiveTab == ScopeSelectorTab.Tests ? SelectedTests : SelectedSources;

    public static ScopeSelectorState Create(DiscoveryResult discovery, RunScopeOptions? existingScope)
    {
        var selectedTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existingScope?.IsConfigured == true)
        {
            var (tests, sources) = RunScopeApplicator.ResolveSelection(discovery, existingScope);
            foreach (var file in tests)
            {
                selectedTests.Add(file);
            }

            foreach (var file in sources)
            {
                selectedSources.Add(file);
            }
        }
        else
        {
            foreach (var file in RunScopeDefaults.SelectDefaultTestFiles(discovery.TestFiles))
            {
                selectedTests.Add(file);
            }

            foreach (var file in RunScopeDefaults.SelectDefaultSourceFiles(discovery.SourceFiles))
            {
                selectedSources.Add(file);
            }
        }

        return new ScopeSelectorState
        {
            ProjectRoot = discovery.ProjectRoot,
            TestFiles = discovery.TestFiles,
            SourceFiles = discovery.SourceFiles,
            SelectedTests = selectedTests,
            SelectedSources = selectedSources,
        };
    }

    public void ClampSelection()
    {
        if (ActiveFiles.Count == 0)
        {
            SelectedIndex = 0;
            return;
        }

        SelectedIndex = Math.Clamp(SelectedIndex, 0, ActiveFiles.Count - 1);
    }

    public RunScopeOptions ToScopeOptions() =>
        RunScopeApplicator.CreateScopeFromSelection(
            ProjectRoot,
            SelectedTests,
            SelectedSources);
}
