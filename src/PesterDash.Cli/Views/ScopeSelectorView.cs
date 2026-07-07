using PesterDash.Core.Configuration;
using PesterDash.Core.Models;
using PesterDash.Cli.Utilities;
using Spectre.Console;

namespace PesterDash.Cli.Views;

/// <summary>Interactive picker for test and source file run scope.</summary>
internal static class ScopeSelectorView
{
    public static async Task<RunScopeOptions?> SelectAsync(
        DiscoveryResult discovery,
        RunScopeOptions? existingScope,
        CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected)
        {
            return null;
        }

        var state = ScopeSelectorState.Create(discovery, existingScope);

        return await AlternateScreen.RunAsync(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                state.ClampSelection();
                Render(state);

                var keyInfo = AnsiConsole.Console.Input.ReadKey(intercept: true);
                if (keyInfo is null)
                {
                    return null;
                }

                var key = keyInfo.Value;

                switch (key.Key)
                {
                    case ConsoleKey.Tab:
                        state.ActiveTab = state.ActiveTab == ScopeSelectorTab.Tests
                            ? ScopeSelectorTab.Sources
                            : ScopeSelectorTab.Tests;
                        state.SelectedIndex = 0;
                        break;

                    case ConsoleKey.UpArrow:
                        if (state.ActiveFiles.Count > 0)
                        {
                            state.SelectedIndex = state.SelectedIndex == 0
                                ? state.ActiveFiles.Count - 1
                                : state.SelectedIndex - 1;
                        }

                        break;

                    case ConsoleKey.DownArrow:
                        if (state.ActiveFiles.Count > 0)
                        {
                            state.SelectedIndex = (state.SelectedIndex + 1) % state.ActiveFiles.Count;
                        }

                        break;

                    case ConsoleKey.Spacebar:
                        ToggleCurrent(state);
                        break;

                    case ConsoleKey.D:
                        ToggleDirectory(state);
                        break;

                    case ConsoleKey.A:
                        SelectAll(state, selected: true);
                        break;

                    case ConsoleKey.N:
                        SelectAll(state, selected: false);
                        break;

                    case ConsoleKey.Enter:
                        if (state.SelectedTests.Count == 0)
                        {
                            state.FlashMessage = "Select at least one test file.";
                            break;
                        }

                        return state.ToScopeOptions();

                    case ConsoleKey.Escape:
                    case ConsoleKey.Q:
                        return null;

                    default:
                        break;
                }
            }

            return null;
        });
    }

    private static void ToggleCurrent(ScopeSelectorState state)
    {
        if (state.ActiveFiles.Count == 0)
        {
            return;
        }

        var file = state.ActiveFiles[state.SelectedIndex];
        var selection = state.ActiveSelection;

        if (!selection.Add(file))
        {
            selection.Remove(file);
        }
    }

    private static void ToggleDirectory(ScopeSelectorState state)
    {
        if (state.ActiveFiles.Count == 0)
        {
            return;
        }

        var current = state.ActiveFiles[state.SelectedIndex];
        var relative = Path.GetRelativePath(state.ProjectRoot, current);
        var relativeDirectory = Path.GetDirectoryName(relative) ?? string.Empty;

        var filesInDirectory = state.ActiveFiles
            .Where(file =>
            {
                var fileRelative = Path.GetRelativePath(state.ProjectRoot, file);
                var fileDirectory = Path.GetDirectoryName(fileRelative) ?? string.Empty;
                return string.Equals(fileDirectory, relativeDirectory, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var allSelected = filesInDirectory.All(file => state.ActiveSelection.Contains(file));
        var selection = state.ActiveSelection;

        foreach (var file in filesInDirectory)
        {
            if (allSelected)
            {
                selection.Remove(file);
            }
            else
            {
                selection.Add(file);
            }
        }

        state.FlashMessage = allSelected
            ? $"Deselected directory {relativeDirectory}"
            : $"Selected directory {relativeDirectory}";
    }

    private static void SelectAll(ScopeSelectorState state, bool selected)
    {
        var selection = state.ActiveSelection;
        selection.Clear();

        if (selected)
        {
            foreach (var file in state.ActiveFiles)
            {
                selection.Add(file);
            }
        }
    }

    private static void Render(ScopeSelectorState state)
    {
        AnsiConsole.Clear();

        var projectName = Path.GetFileName(state.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = state.ProjectRoot;
        }

        AnsiConsole.MarkupLine($"[bold cyan] PesterDash [/] [grey]│[/] Run scope [grey]│[/] [white]{Markup.Escape(projectName)}[/]");
        AnsiConsole.MarkupLine("[grey]" + new string('─', Math.Max(40, Console.WindowWidth - 2)) + "[/]");
        AnsiConsole.WriteLine();

        RenderTabBar(state);
        AnsiConsole.WriteLine();

        var selectedCount = state.ActiveSelection.Count;
        var totalCount = state.ActiveFiles.Count;
        AnsiConsole.MarkupLine(
            $"[grey]{selectedCount}/{totalCount} selected in this tab  │  " +
            $"[green]{state.SelectedTests.Count}[/] tests  │  [blue]{state.SelectedSources.Count}[/] sources[/]");
        AnsiConsole.WriteLine();

        if (state.ActiveFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files discovered in this category.[/]");
        }
        else
        {
            RenderFileList(state);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[grey]Tab switch tab  ↑↓ move  Space toggle file  d toggle directory  a all  n none  Enter save  q cancel[/]");

        if (!string.IsNullOrWhiteSpace(state.FlashMessage))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]{state.FlashMessage}[/]");
            state.FlashMessage = null;
        }
    }

    private static void RenderTabBar(ScopeSelectorState state)
    {
        var testsLabel = state.ActiveTab == ScopeSelectorTab.Tests
            ? $"[black on cyan] Tests ({state.SelectedTests.Count}/{state.TestFiles.Count}) [/]"
            : $"[grey] Tests ({state.SelectedTests.Count}/{state.TestFiles.Count}) [/]";

        var sourcesLabel = state.ActiveTab == ScopeSelectorTab.Sources
            ? $"[black on cyan] Sources ({state.SelectedSources.Count}/{state.SourceFiles.Count}) [/]"
            : $"[grey] Sources ({state.SelectedSources.Count}/{state.SourceFiles.Count}) [/]";

        AnsiConsole.MarkupLine($"{testsLabel}  {sourcesLabel}");
    }

    private static void RenderFileList(ScopeSelectorState state)
    {
        var maxRows = Math.Max(5, Console.WindowHeight - 14);
        var start = Math.Max(0, state.SelectedIndex - maxRows / 2);
        var end = Math.Min(state.ActiveFiles.Count, start + maxRows);
        start = Math.Max(0, end - maxRows);

        string? currentDirectory = null;

        for (var index = start; index < end; index++)
        {
            var file = state.ActiveFiles[index];
            var relative = Path.GetRelativePath(state.ProjectRoot, file);
            var directory = Path.GetDirectoryName(relative);

            if (!string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                currentDirectory = directory;
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    AnsiConsole.MarkupLine($"[bold]{Markup.Escape(directory)}[/]");
                }
            }

            var fileName = Path.GetFileName(relative);
            var selected = state.ActiveSelection.Contains(file);
            var marker = selected ? "[green]x[/]" : "[grey] [/]";
            var rowPrefix = index == state.SelectedIndex ? ">" : " ";

            if (index == state.SelectedIndex)
            {
                AnsiConsole.MarkupLine($"{rowPrefix} {marker} [white on grey]{Markup.Escape(fileName)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"{rowPrefix} {marker} {Markup.Escape(fileName)}");
            }
        }
    }
}
